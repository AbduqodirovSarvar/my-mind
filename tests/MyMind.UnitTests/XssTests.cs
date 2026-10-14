using AngleSharp.Html.Parser;
using MyMind.Infrastructure.Content;
using static MyMind.UnitTests.TipTapRendererTests;

namespace MyMind.UnitTests;

/// <summary>
/// Phase 2 gate (plan §13.1): every payload either is rejected or renders to HTML a browser
/// cannot execute. Checked by parsing the output, not by string matching.
/// </summary>
public class XssTests
{
    private static readonly HashSet<string> ForbiddenElements =
        ["script", "iframe", "frame", "object", "embed", "svg", "math", "style", "link", "meta", "base", "form", "input", "button", "textarea", "template"];

    private readonly TipTapRenderer _renderer = new(new ArticleHtmlSanitizer());

    public static TheoryData<string, string> Payloads => new()
    {
        { "script in text", Doc(Paragraph(Text("<script>alert(1)</script>"))) },
        { "img onerror in text", Doc(Paragraph(Text("<img src=x onerror=alert(1)>"))) },
        { "svg onload in heading", Doc(Heading(2, "<svg onload=alert(1)>")) },
        { "javascript link", Doc(Paragraph(Text("x", Link("javascript:alert(1)")))) },
        { "mixed-case javascript link", Doc(Paragraph(Text("x", Link("JaVaScRiPt:alert(1)")))) },
        { "javascript link with whitespace", Doc(Paragraph(Text("x", Link(" \tjavascript:alert(1)")))) },
        { "encoded javascript link", Doc(Paragraph(Text("x", Link("java&#115;cript:alert(1)")))) },
        { "data link", Doc(Paragraph(Text("x", Link("data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==")))) },
        { "vbscript link", Doc(Paragraph(Text("x", Link("vbscript:msgbox(1)")))) },
        { "attribute breakout in href", Doc(Paragraph(Text("x", Link("https://ok.example/\" onmouseover=\"alert(1)")))) },
        { "attribute breakout in alt", Doc(new { type = "image", attrs = new { src = "/uploads/a.webp", alt = "\" onerror=\"alert(1)" } }) },
        { "attribute breakout in code language", Doc(new { type = "codeBlock", attrs = new { language = "x\" onmouseover=\"alert(1)" }, content = new[] { Text("x") } }) },
        { "script in code block", Doc(new { type = "codeBlock", content = new[] { Text("</code><script>alert(1)</script>") } }) },
        { "attribute breakout in list start", Doc(new { type = "orderedList", attrs = new { start = "2\" onclick=\"alert(1)" }, content = new[] { new { type = "listItem", content = new[] { Paragraph(Text("x")) } } } }) },
        { "attribute breakout in colspan", Doc(new { type = "table", content = new[] { new { type = "tableRow", content = new[] { new { type = "tableCell", attrs = new { colspan = "2\" onclick=\"alert(1)" }, content = new[] { Paragraph(Text("x")) } } } } } }) },
        { "attribute breakout in heading level", Doc(new { type = "heading", attrs = new { level = "<script>" }, content = new[] { Text("x") } }) },
        { "javascript image", Doc(new { type = "image", attrs = new { src = "javascript:alert(1)" } }) },
        { "path traversal image", Doc(new { type = "image", attrs = new { src = "/uploads/../../etc/passwd" } }) },
        { "javascript video", Doc(new { type = "youtube", attrs = new { src = "javascript:alert(1)" } }) },
        { "breakout video id", Doc(new { type = "youtube", attrs = new { videoId = "abc\"><script>alert(1)</script>" } }) },
        { "raw html node", Doc(new { type = "html", attrs = new { html = "<script>alert(1)</script>" } }) },
        { "iframe node", Doc(new { type = "iframe", attrs = new { src = "https://evil.example" } }) },
        { "unknown mark with style", Doc(Paragraph(Text("x", new { type = "textStyle", attrs = new { style = "background:url(javascript:alert(1))" } }))) },
    };

    [Theory]
    [MemberData(nameof(Payloads))]
    public void Payload_IsRejectedOrNeutralized(string name, string document)
    {
        var result = _renderer.Render(document);
        if (!result.IsSuccess)
        {
            return;
        }

        var dom = new HtmlParser().ParseDocument($"<body>{result.Value.Html}</body>");
        foreach (var element in dom.Body!.QuerySelectorAll("*"))
        {
            var tag = element.LocalName;
            Assert.False(ForbiddenElements.Contains(tag), $"{name}: <{tag}> survived");

            foreach (var attribute in element.Attributes)
            {
                Assert.False(attribute.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase), $"{name}: {attribute.Name} on <{tag}>");
                Assert.False(attribute.Name.Equals("style", StringComparison.OrdinalIgnoreCase), $"{name}: style on <{tag}>");

                if (attribute.Name is "href" or "src")
                {
                    var value = attribute.Value.Trim();
                    var isSafe = value.StartsWith("https://", StringComparison.Ordinal)
                        || value.StartsWith("http://", StringComparison.Ordinal)
                        || value.StartsWith("mailto:", StringComparison.Ordinal)
                        || value.StartsWith("/uploads/", StringComparison.Ordinal);
                    Assert.True(isSafe, $"{name}: {attribute.Name}=\"{value}\"");
                }
            }
        }
    }

    [Fact]
    public void Sanitizer_StripsDangerousMarkup_EvenIfARendererBugLetItThrough()
    {
        var html = new ArticleHtmlSanitizer().Sanitize(
            "<p onclick=\"x()\">a</p><script>alert(1)</script><iframe src=\"https://evil.example\"></iframe>"
            + "<a href=\"javascript:alert(1)\">b</a><div class=\"evil video-embed\" style=\"x\">c</div>");

        Assert.Equal("<p>a</p><a>b</a><div class=\"video-embed\">c</div>", html);
    }

    private static object Link(string href) => new { type = "link", attrs = new { href } };
}
