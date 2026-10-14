using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MyMind.Application.Common;
using MyMind.Domain.Articles;
using MyMind.Domain.Common;
using MyMind.Domain.Media;

namespace MyMind.Infrastructure.Content;

/// <summary>
/// Renders a TipTap (ProseMirror) document to HTML on the server (plan §8): only the node types
/// below are accepted, every text and attribute is encoded, URLs are checked, and the result still
/// goes through <see cref="ArticleHtmlSanitizer"/>. The client's own HTML is never trusted.
/// </summary>
public sealed partial class TipTapRenderer(ArticleHtmlSanitizer sanitizer) : IArticleRenderer
{
    private const int MaxDepth = 32;
    private const int MaxNodes = 50_000;
    private const int ExcerptLength = 160;
    private const int WordsPerMinute = 200;

    public Result<RenderedArticle> Render(string documentJson)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(documentJson, new JsonDocumentOptions { MaxDepth = MaxDepth * 4 });
        }
        catch (JsonException)
        {
            return Error.Validation("Maqola kontenti noto'g'ri formatda.");
        }

        using (document)
        {
            var context = new RenderContext();
            try
            {
                if (TypeOf(document.RootElement) != "doc")
                {
                    throw new ContentException("Kontent TipTap hujjati bo'lishi kerak.");
                }

                RenderChildren(document.RootElement, context, depth: 0);
            }
            catch (ContentException e)
            {
                return Error.Validation(e.Message);
            }

            var html = sanitizer.Sanitize(context.Html.ToString());
            var text = Whitespace().Replace(context.Text.ToString(), " ").Trim();
            var words = text.Length == 0 ? 0 : text.Split(' ').Length;
            var toc = JsonSerializer.Serialize(context.Toc, JsonSerializerOptions.Web);

            return new RenderedArticle(
                html, text, toc, MakeExcerpt(text), Math.Max(1, (int)Math.Ceiling(words / (double)WordsPerMinute)));
        }
    }

    private static void RenderChildren(JsonElement node, RenderContext context, int depth)
    {
        if (!node.TryGetProperty("content", out var content))
        {
            return;
        }

        if (content.ValueKind != JsonValueKind.Array)
        {
            throw new ContentException("Maqola kontenti noto'g'ri formatda.");
        }

        foreach (var child in content.EnumerateArray())
        {
            RenderNode(child, context, depth + 1);
        }
    }

    private static void RenderNode(JsonElement node, RenderContext context, int depth)
    {
        if (depth > MaxDepth)
        {
            throw new ContentException("Maqola tuzilmasi juda chuqur.");
        }

        if (++context.NodeCount > MaxNodes)
        {
            throw new ContentException("Maqola juda katta.");
        }

        var type = TypeOf(node);
        switch (type)
        {
            case "paragraph":
                Block(node, context, depth, "<p>", "</p>");
                break;
            case "heading":
                RenderHeading(node, context, depth);
                break;
            case "text":
                RenderText(node, context);
                break;
            case "hardBreak":
                context.Html.Append("<br>");
                context.Text.Append(' ');
                break;
            case "bulletList":
                Block(node, context, depth, "<ul>", "</ul>");
                break;
            case "orderedList":
                var start = IntAttr(node, "start", 1, 1, 100_000);
                Block(node, context, depth, start == 1 ? "<ol>" : $"<ol start=\"{start}\">", "</ol>");
                break;
            case "listItem":
                Block(node, context, depth, "<li>", "</li>");
                break;
            case "blockquote":
                Block(node, context, depth, "<blockquote>", "</blockquote>");
                break;
            case "codeBlock":
                RenderCodeBlock(node, context);
                break;
            case "horizontalRule":
                context.Html.Append("<hr>");
                break;
            case "image":
                RenderImage(node, context);
                break;
            case "table":
                Block(node, context, depth, "<table><tbody>", "</tbody></table>");
                break;
            case "tableRow":
                Block(node, context, depth, "<tr>", "</tr>");
                break;
            case "tableHeader":
            case "tableCell":
                var tag = type == "tableHeader" ? "th" : "td";
                var colspan = IntAttr(node, "colspan", 1, 1, 50);
                var rowspan = IntAttr(node, "rowspan", 1, 1, 50);
                var spans = (colspan > 1 ? $" colspan=\"{colspan}\"" : "") + (rowspan > 1 ? $" rowspan=\"{rowspan}\"" : "");
                Block(node, context, depth, $"<{tag}{spans}>", $"</{tag}>");
                break;
            case "youtube":
                RenderVideo(node, context, "youtube", YouTubeId());
                break;
            case "vimeo":
                RenderVideo(node, context, "vimeo", VimeoId());
                break;
            default:
                throw new ContentException($"Ruxsat etilmagan element: {Truncate(type, 40)}.");
        }
    }

    private static void Block(JsonElement node, RenderContext context, int depth, string open, string close)
    {
        context.Html.Append(open);
        RenderChildren(node, context, depth);
        context.Html.Append(close);
        context.Text.Append('\n');
    }

    private static void RenderHeading(JsonElement node, RenderContext context, int depth)
    {
        // H1 is the article title, so content headings are H2–H4.
        var level = Math.Clamp(IntAttr(node, "level", 2, 1, 6), 2, 4);
        var headingText = new RenderContext();
        RenderChildren(node, headingText, depth);
        var plain = Whitespace().Replace(headingText.Text.ToString(), " ").Trim();

        var id = Slug.MakeUnique(Slug.From(plain, "bolim"), context.HeadingIds);
        context.HeadingIds.Add(id);
        if (level <= 3 && plain.Length > 0)
        {
            context.Toc.Add(new TocEntry(level, plain, id));
        }

        context.NodeCount += headingText.NodeCount;
        context.Html.Append($"<h{level} id=\"{id}\">").Append(headingText.Html).Append($"</h{level}>");
        context.Text.Append(plain).Append('\n');
    }

    private static void RenderText(JsonElement node, RenderContext context)
    {
        var text = node.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "";
        var html = WebUtility.HtmlEncode(text);

        if (node.TryGetProperty("marks", out var marks) && marks.ValueKind == JsonValueKind.Array)
        {
            foreach (var mark in marks.EnumerateArray())
            {
                html = TypeOf(mark) switch
                {
                    "bold" => $"<strong>{html}</strong>",
                    "italic" => $"<em>{html}</em>",
                    "underline" => $"<u>{html}</u>",
                    "strike" => $"<s>{html}</s>",
                    "code" => $"<code>{html}</code>",
                    "link" when SafeLink(StringAttr(mark, "href")) is { } href =>
                        $"<a href=\"{WebUtility.HtmlEncode(href)}\" rel=\"nofollow ugc noopener\">{html}</a>",
                    // Unknown marks and unsafe links lose their formatting but keep the text.
                    _ => html,
                };
            }
        }

        context.Html.Append(html);
        context.Text.Append(text);
    }

    private static void RenderCodeBlock(JsonElement node, RenderContext context)
    {
        var language = StringAttr(node, "language");
        var languageAttr = language is not null && CodeLanguage().IsMatch(language)
            ? $" data-language=\"{language}\""
            : "";

        var code = new StringBuilder();
        if (node.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in content.EnumerateArray())
            {
                if (child.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                {
                    code.Append(t.GetString());
                }
            }
        }

        context.Html.Append($"<pre><code{languageAttr}>").Append(WebUtility.HtmlEncode(code.ToString())).Append("</code></pre>");
        context.Text.Append(code).Append('\n');
    }

    private static void RenderImage(JsonElement node, RenderContext context)
    {
        var src = StringAttr(node, "src");
        if (src is null || !UploadPath().IsMatch(src) || src.Contains("..", StringComparison.Ordinal))
        {
            throw new ContentException("Rasmlar faqat platformaga yuklangan bo'lishi kerak.");
        }

        var alt = WebUtility.HtmlEncode(StringAttr(node, "alt") ?? "");
        var width = IntAttr(node, "width", 0, 1, 10_000);
        var height = IntAttr(node, "height", 0, 1, 10_000);
        var size = width > 0 && height > 0 ? $" width=\"{width}\" height=\"{height}\"" : "";

        // Uploads wider than the small variant have one next to them (MediaFile.KeysFor): phones load that.
        var srcset = width > MediaFile.SmallWidth && src.EndsWith(".webp", StringComparison.Ordinal)
            ? $" srcset=\"{src[..^5]}-{MediaFile.SmallWidth}.webp {MediaFile.SmallWidth}w, {src} {width}w\" sizes=\"(max-width: 768px) 100vw, 768px\""
            : "";

        context.Html.Append($"<img src=\"{src}\"{srcset} alt=\"{alt}\"{size} loading=\"lazy\" decoding=\"async\">");
    }

    /// <summary>
    /// Stores only the provider and video id; the reader page shows a thumbnail and creates the
    /// iframe on click (plan §8.4), so saved HTML never contains an iframe.
    /// </summary>
    private static void RenderVideo(JsonElement node, RenderContext context, string provider, Regex idFromUrl)
    {
        var id = StringAttr(node, "videoId");
        if (id is null && StringAttr(node, "src") is { } src && idFromUrl.Match(src) is { Success: true } match)
        {
            id = match.Groups["id"].Value;
        }

        var valid = provider == "youtube" ? id is not null && YouTubeIdFormat().IsMatch(id) : id is not null && VimeoIdFormat().IsMatch(id);
        if (!valid)
        {
            throw new ContentException("Video havolasi noto'g'ri: faqat YouTube va Vimeo qo'llab-quvvatlanadi.");
        }

        context.Html.Append($"<div class=\"video-embed\" data-provider=\"{provider}\" data-video-id=\"{id}\"></div>");
    }

    private static string? SafeLink(string? href)
    {
        if (string.IsNullOrWhiteSpace(href) || !Uri.TryCreate(href.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.Scheme is "http" or "https" or "mailto" ? uri.AbsoluteUri : null;
    }

    private static string TypeOf(JsonElement node) =>
        node.ValueKind == JsonValueKind.Object
        && node.TryGetProperty("type", out var type)
        && type.ValueKind == JsonValueKind.String
            ? type.GetString() ?? ""
            : throw new ContentException("Maqola kontenti noto'g'ri formatda.");

    private static string? StringAttr(JsonElement node, string name) =>
        node.TryGetProperty("attrs", out var attrs)
        && attrs.ValueKind == JsonValueKind.Object
        && attrs.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int IntAttr(JsonElement node, string name, int fallback, int min, int max)
    {
        if (node.TryGetProperty("attrs", out var attrs)
            && attrs.ValueKind == JsonValueKind.Object
            && attrs.TryGetProperty(name, out var value))
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            {
                return Math.Clamp(number, min, max);
            }

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
            {
                return Math.Clamp(parsed, min, max);
            }
        }

        return fallback;
    }

    private static string MakeExcerpt(string text)
    {
        if (text.Length <= ExcerptLength)
        {
            return text;
        }

        var cut = text[..ExcerptLength];
        var lastSpace = cut.LastIndexOf(' ');
        return (lastSpace > ExcerptLength / 2 ? cut[..lastSpace] : cut).TrimEnd(',', '.', ';', ':') + "…";
    }

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex("^[a-z0-9+#-]{1,20}$")]
    private static partial Regex CodeLanguage();

    [GeneratedRegex("^/uploads/[A-Za-z0-9/_.-]{1,200}$")]
    private static partial Regex UploadPath();

    [GeneratedRegex(@"^https?://(?:www\.|m\.)?(?:youtube\.com/(?:watch\?(?:.*&)?v=|embed/|shorts/)|youtube-nocookie\.com/embed/|youtu\.be/)(?<id>[A-Za-z0-9_-]{11})(?:[?&#].*)?$")]
    private static partial Regex YouTubeId();

    [GeneratedRegex(@"^https?://(?:www\.|player\.)?vimeo\.com/(?:video/)?(?<id>\d{1,12})(?:[/?#].*)?$")]
    private static partial Regex VimeoId();

    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    private static partial Regex YouTubeIdFormat();

    [GeneratedRegex(@"^\d{1,12}$")]
    private static partial Regex VimeoIdFormat();

    private sealed record TocEntry(int Level, string Text, string Id);

    private sealed class RenderContext
    {
        public StringBuilder Html { get; } = new();

        public StringBuilder Text { get; } = new();

        public List<TocEntry> Toc { get; } = [];

        public HashSet<string> HeadingIds { get; } = [];

        public int NodeCount { get; set; }
    }

    private sealed class ContentException(string message) : Exception(message);
}
