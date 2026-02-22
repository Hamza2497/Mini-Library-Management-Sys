using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Library.Api.Data;

public class LibraryDbContextFactory : IDesignTimeDbContextFactory<LibraryDbContext>
{
    public LibraryDbContext CreateDbContext(string[] args)
    {
        var workingDir = Directory.GetCurrentDirectory();
        var apiDir = Directory.Exists(Path.Combine(workingDir, "Library.Api"))
            ? Path.Combine(workingDir, "Library.Api")
            : workingDir;
        var dbPath = Path.Combine(apiDir, "library.db");
        var optionsBuilder = new DbContextOptionsBuilder<LibraryDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

        return new LibraryDbContext(optionsBuilder.Options);
    }
}
