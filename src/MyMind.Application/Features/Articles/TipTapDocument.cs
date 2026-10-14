using System.Text.Json;

namespace MyMind.Application.Features.Articles;

/// <summary>Read-only helpers over a TipTap document's JSON.</summary>
public static class TipTapDocument
{
    /// <summary>The <c>src</c> of every image node, in document order.</summary>
    public static IReadOnlyList<string> ImageSources(string documentJson)
    {
        var sources = new List<string>();
        try
        {
            using var document = JsonDocument.Parse(documentJson);
            Collect(document.RootElement, sources, depth: 0);
        }
        catch (JsonException)
        {
            // Invalid JSON is rejected on publish; there is simply nothing to link here.
        }

        return sources;
    }

    private static void Collect(JsonElement node, List<string> sources, int depth)
    {
        if (depth > 64 || node.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (node.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() == "image"
            && node.TryGetProperty("attrs", out var attrs) && attrs.ValueKind == JsonValueKind.Object
            && attrs.TryGetProperty("src", out var src) && src.ValueKind == JsonValueKind.String)
        {
            sources.Add(src.GetString()!);
        }

        if (node.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in content.EnumerateArray())
            {
                Collect(child, sources, depth + 1);
            }
        }
    }
}
