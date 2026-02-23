using Library.Api.Auth;
using Library.Api.Data;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var googleClientId = builder.Configuration["GOOGLE_CLIENT_ID"]
    ?? "308905289637-064rj6fgrqlcebmbh1gg1v2ub20gc93p.apps.googleusercontent.com";
const string uiCorsPolicy = "UiCors";

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Use a Google ID token as Bearer token.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste Google ID token only (without 'Bearer ')."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://accounts.google.com";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = new[] { "https://accounts.google.com", "accounts.google.com" },
            ValidateAudience = true,
            ValidAudience = googleClientId,
            ValidateLifetime = true
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin"));

    options.AddPolicy("LibrarianOrAdmin", policy =>
        policy.RequireRole("Librarian", "Admin"));

    options.AddPolicy("MemberOrAbove", policy =>
        policy.RequireRole("Member", "Librarian", "Admin"));
});
builder.Services.AddTransient<IClaimsTransformation, RoleClaimsTransformation>();
builder.Services.AddCors(options =>
{
    var configuredOrigins = Environment.GetEnvironmentVariable("CORS_ORIGINS")
        ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToList()
        ?? new List<string>();
    configuredOrigins.Add("http://localhost:5173");
    configuredOrigins = configuredOrigins.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    options.AddPolicy(uiCorsPolicy, policy =>
    {
        policy.WithOrigins(configuredOrigins.ToArray())
            .WithHeaders("Authorization", "Content-Type")
            .WithMethods("GET", "POST", "PUT", "DELETE");
    });
});

var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
var usePostgres = !string.IsNullOrWhiteSpace(databaseUrl);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=library.db";

const string dataSourcePrefix = "Data Source=";
if (!usePostgres && connectionString.StartsWith(dataSourcePrefix, StringComparison.OrdinalIgnoreCase))
{
    var dataSource = connectionString[dataSourcePrefix.Length..].Trim();
    if (!Path.IsPathRooted(dataSource))
    {
        var absoluteDbPath = Path.Combine(builder.Environment.ContentRootPath, dataSource);
        connectionString = $"{dataSourcePrefix}{absoluteDbPath}";
    }
}

builder.Services.AddDbContext<LibraryDbContext>(options =>
{
    if (usePostgres)
    {
        options.UseNpgsql(ConvertDatabaseUrlToConnectionString(databaseUrl!));
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors(uiCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

var isEfDesignTime = AppDomain.CurrentDomain.GetAssemblies()
    .Any(a => a.GetName().Name == "Microsoft.EntityFrameworkCore.Design");

if (!isEfDesignTime)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
    db.Database.Migrate();
    app.Run();
}

static string ConvertDatabaseUrlToConnectionString(string databaseUrl)
{
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':', 2);
    var username = Uri.UnescapeDataString(userInfo[0]);
    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
    var database = uri.AbsolutePath.TrimStart('/');

    var sb = new StringBuilder();
    sb.Append($"Host={uri.Host};");
    if (uri.Port > 0)
    {
        sb.Append($"Port={uri.Port};");
    }
    sb.Append($"Database={database};");
    sb.Append($"Username={username};");
    sb.Append($"Password={password};");
    sb.Append("SSL Mode=Require;Trust Server Certificate=true;");
    return sb.ToString();
}
