using Ganss.Xss;

namespace MyMind.Infrastructure.Content;

/// <summary>
/// Second line of defence (plan §7.3): the renderer already emits only safe HTML; this allow-list
/// sanitizer guarantees it even if a renderer bug slips something through.
/// </summary>
public sealed class ArticleHtmlSanitizer
{
    private readonly HtmlSanitizer _sanitizer;

    public ArticleHtmlSanitizer()
    {
        _sanitizer = new HtmlSanitizer(new HtmlSanitizerOptions
        {
            AllowedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "p", "h2", "h3", "h4", "strong", "em", "u", "s", "code", "a", "br", "ul", "ol", "li",
                "blockquote", "pre", "hr", "img", "table", "tbody", "tr", "th", "td", "div",
            },
            AllowedAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "href", "rel", "id", "start", "class", "src", "alt", "width", "height", "loading", "decoding",
                "colspan", "rowspan", "data-provider", "data-video-id", "data-language",
            },
            AllowedSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "http", "https", "mailto" },
            UriAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "href", "src" },
            AllowedCssProperties = new HashSet<string>(),
        });

        // Only the class the renderer itself emits.
        _sanitizer.AllowedClasses.Add("video-embed");
    }

    public string Sanitize(string html) => _sanitizer.Sanitize(html);
}
