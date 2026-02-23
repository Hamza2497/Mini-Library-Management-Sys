using System.Text.RegularExpressions;

namespace Library.Api.Services;

public class BookEnrichmentService
{
    private static readonly Dictionary<string, string[]> CategoryKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Programming"] = ["code", "coding", "program", "programming", "developer", "software", "algorithm", "api", "database", "engineering"],
        ["Business"] = ["business", "management", "marketing", "startup", "finance", "leadership", "strategy", "sales", "economics"],
        ["Science"] = ["science", "physics", "chemistry", "biology", "astronomy", "research", "mathematics", "quantum", "neuroscience"],
        ["History"] = ["history", "war", "empire", "ancient", "medieval", "revolution", "civilization", "historical", "biography"],
        ["Fiction"] = ["fiction", "novel", "story", "fantasy", "mystery", "romance", "thriller", "adventure", "magic"]
    };

    private static readonly Dictionary<string, string[]> CategoryTags = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Programming"] = ["programming", "software", "development", "engineering", "coding"],
        ["Business"] = ["business", "leadership", "strategy", "management", "growth"],
        ["Science"] = ["science", "research", "analysis", "discovery", "knowledge"],
        ["History"] = ["history", "timeline", "culture", "society", "legacy"],
        ["Fiction"] = ["fiction", "narrative", "characters", "plot", "imagination"],
        ["Other"] = ["books", "reading", "learning", "insights", "ideas"]
    };

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "from", "that", "this", "into", "your", "you", "are", "was", "were", "book"
    };

    public BookEnrichmentResult Enrich(string title, string author, string? existingDescription)
    {
        var combined = $"{title} {author} {existingDescription}".ToLowerInvariant();
        var category = ResolveCategory(combined);

        var titleTokens = Tokenize(title)
            .Where(t => t.Length > 2 && !StopWords.Contains(t))
            .ToList();

        var tags = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var token in titleTokens)
        {
            AddTag(token, tags, seen);
        }

        foreach (var token in CategoryTags[category])
        {
            AddTag(token, tags, seen);
        }

        var surname = author.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(surname))
        {
            AddTag(surname, tags, seen);
        }

        foreach (var fallback in CategoryTags[category])
        {
            AddTag(fallback, tags, seen);
            if (tags.Count == 5)
            {
                break;
            }
        }

        while (tags.Count < 5)
        {
            AddTag("general", tags, seen);
            if (tags.Count < 5)
            {
                AddTag("reading", tags, seen);
            }
        }

        var description =
            $"\"{title}\" by {author} is a {category}-focused book that covers {tags[0]}, {tags[1]}, and {tags[2]}. " +
            $"Best suited for readers interested in {tags[3]} and {tags[4]}.";

        return new BookEnrichmentResult(category, tags.Take(5).ToArray(), description);
    }

    private static string ResolveCategory(string text)
    {
        foreach (var (category, keywords) in CategoryKeywords)
        {
            if (keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                return category;
            }
        }

        return "Other";
    }

    private static IEnumerable<string> Tokenize(string input)
    {
        return Regex.Split(input.ToLowerInvariant(), "[^a-z0-9]+")
            .Where(t => !string.IsNullOrWhiteSpace(t));
    }

    private static void AddTag(string tag, IList<string> tags, ISet<string> seen)
    {
        if (tags.Count >= 5 || string.IsNullOrWhiteSpace(tag) || !seen.Add(tag))
        {
            return;
        }

        tags.Add(tag.ToLowerInvariant());
    }
}

public record BookEnrichmentResult(string Category, IReadOnlyList<string> Tags, string Description);
