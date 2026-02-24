using System.Net.Http.Json;
using System.Text.Json;

namespace Library.Api.Services;

/// <summary>
/// Enriches a book record with category, tags, and description using the Gemini API.
/// Requires 'Gemini:ApiKey' in configuration or 'GEMINI_API_KEY' environment variable.
/// </summary>
public class BookEnrichmentService
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;

    public BookEnrichmentService(HttpClient http, IConfiguration config)
    {
        _http = http;

        var raw = config["Gemini:ApiKey"]
                  ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                "Gemini API key is required. Set 'Gemini:ApiKey' in configuration or 'GEMINI_API_KEY' environment variable.");
        }

        _apiKey = raw.Trim();
    }

    // ── Public API ──────────────────────────────────────────────────────────

    public async Task<BookEnrichmentResult> EnrichAsync(string title, string author)
    {
        try
        {
            Console.WriteLine($"[ENRICH] Calling Gemini API for '{title}'");
            var result = await CallGeminiAsync(title, author);
            Console.WriteLine($"[ENRICH] Gemini succeeded: {result.Category}, tags={string.Join(",", result.Tags)}");
            return result;
        }
        catch (Exception ex)
        {
            // API call failed — propagate the error instead of falling back
            Console.WriteLine($"[ENRICH] Gemini failed ({ex.GetType().Name}): {ex.Message}");
            throw;
        }
    }

    // ── Gemini API call ───────────────────────────────────────────────────────

    private async Task<BookEnrichmentResult> CallGeminiAsync(string title, string author)
    {
        var prompt =
            $"JSON only:\n" +
            $"Book: \"{title}\" by {author}\n" +
            "{\"category\":\"Fiction|Non-Fiction|Science|History|Business|Technology|Philosophy|Other\",\"tags\":[\"tag1\",\"tag2\",\"tag3\",\"tag4\",\"tag5\"],\"description\":\"1-2 sentences\"}";

        var body = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            },
            generationConfig = new { maxOutputTokens = 220, temperature = 0 }
        };

        var url = $"https://generativelanguage.googleapis.com/v1/models/gemini-2.5-flash-lite:generateContent?key={_apiKey}";

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Content = JsonContent.Create(body);

        using var res = await _http.SendAsync(req);
        res.EnsureSuccessStatusCode();

        using var apiDoc = await JsonDocument.ParseAsync(
            await res.Content.ReadAsStreamAsync());

        var rawText = apiDoc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? "";

        return ParseGeminiResponse(rawText, title, author);
    }

    private static BookEnrichmentResult ParseGeminiResponse(string text, string title, string author)
    {
        // Strip optional markdown code fences if present
        text = text.Trim();
        if (text.StartsWith("```"))
        {
            var nl = text.IndexOf('\n');
            text = nl >= 0 ? text[(nl + 1)..] : text[3..];
        }
        if (text.EndsWith("```"))
            text = text[..text.LastIndexOf("```")].TrimEnd();
        text = text.Trim();

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;

        var category = root.TryGetProperty("category", out var c)
            ? (c.GetString() ?? "Other") : "Other";

        var tags = root.TryGetProperty("tags", out var t)
            ? t.EnumerateArray()
                .Select(x => x.GetString() ?? "")
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Take(5)
                .ToArray()
            : [];

        var description = root.TryGetProperty("description", out var d)
            ? (d.GetString() ?? "") : "";

        return new BookEnrichmentResult(category, tags, description);
    }

}

public record BookEnrichmentResult(string Category, IReadOnlyList<string> Tags, string Description);
