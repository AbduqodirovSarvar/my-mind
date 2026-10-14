using System.Text.Json;
using MyMind.Infrastructure.Content;

namespace MyMind.UnitTests;

public class TipTapRendererTests
{
    private readonly TipTapRenderer _renderer = new(new ArticleHtmlSanitizer());

    internal static string Doc(params object[] content) => JsonSerializer.Serialize(new { type = "doc", content });

    internal static object Text(string text, params object[] marks) =>
        marks.Length == 0 ? new { type = "text", text } : new { type = "text", text, marks };

    internal static object Paragraph(params object[] content) => new { type = "paragraph", content };

    internal static object Heading(int level, string text) => new { type = "heading", attrs = new { level }, content = new[] { Text(text) } };

    [Fact]
    public void Render_ProducesHtmlTextAndTableOfContents()
    {
        var result = _renderer.Render(Doc(
            Heading(2, "Kirish"),
            Paragraph(Text("DI — bu "), Text("muhim", new { type = "bold" }), Text(" mavzu.")),
            Heading(3, "Kirish"),
            Heading(4, "Tafsilot")));

        Assert.True(result.IsSuccess);
        var article = result.Value;
        Assert.Equal(
            "<h2 id=\"kirish\">Kirish</h2><p>DI — bu <strong>muhim</strong> mavzu.</p><h3 id=\"kirish-2\">Kirish</h3><h4 id=\"tafsilot\">Tafsilot</h4>",
            article.Html);
        Assert.Equal("Kirish DI — bu muhim mavzu. Kirish Tafsilot", article.Text);

        // H2 and H3 only, with the same ids as in the HTML.
        using var toc = JsonDocument.Parse(article.TocJson);
        Assert.Equal(2, toc.RootElement.GetArrayLength());
        Assert.Equal("kirish-2", toc.RootElement[1].GetProperty("id").GetString());
        Assert.Equal(3, toc.RootElement[1].GetProperty("level").GetInt32());
    }

    [Fact]
    public void Render_MapsH1ToH2_BecauseH1IsTheTitle()
    {
        var html = _renderer.Render(Doc(Heading(1, "Bosh"))).Value.Html;

        Assert.StartsWith("<h2", html);
    }

    [Fact]
    public void Render_KeepsSafeLinks_WithNofollow()
    {
        var html = _renderer.Render(Doc(Paragraph(Text("havola", new { type = "link", attrs = new { href = "https://example.com/a?b=1" } })))).Value.Html;

        Assert.Equal("<p><a href=\"https://example.com/a?b=1\" rel=\"nofollow ugc noopener\">havola</a></p>", html);
    }

    [Fact]
    public void Render_TurnsVideoLinksIntoPlaceholders()
    {
        var html = _renderer.Render(Doc(
            new { type = "youtube", attrs = new { src = "https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=10" } },
            new { type = "vimeo", attrs = new { src = "https://vimeo.com/76979871" } })).Value.Html;

        Assert.Equal(
            "<div class=\"video-embed\" data-provider=\"youtube\" data-video-id=\"dQw4w9WgXcQ\"></div>"
            + "<div class=\"video-embed\" data-provider=\"vimeo\" data-video-id=\"76979871\"></div>",
            html);
    }

    [Fact]
    public void Render_AcceptsUploadedImages()
    {
        var html = _renderer.Render(Doc(new { type = "image", attrs = new { src = "/uploads/u1/2026/a.webp", alt = "Sxema", width = 800, height = 600 } })).Value.Html;

        Assert.Equal("<img src=\"/uploads/u1/2026/a.webp\" alt=\"Sxema\" width=\"800\" height=\"600\" loading=\"lazy\" decoding=\"async\">", html);
    }

    [Fact]
    public void Render_RendersListsTablesAndCode()
    {
        var html = _renderer.Render(Doc(
            new { type = "orderedList", attrs = new { start = 3 }, content = new[] { new { type = "listItem", content = new[] { Paragraph(Text("bir")) } } } },
            new { type = "table", content = new[] { new { type = "tableRow", content = new object[] { new { type = "tableHeader", attrs = new { colspan = 2 }, content = new[] { Paragraph(Text("A")) } } } } } },
            new { type = "codeBlock", attrs = new { language = "csharp" }, content = new[] { Text("var x = a < b;") } })).Value.Html;

        Assert.Equal(
            "<ol start=\"3\"><li><p>bir</p></li></ol>"
            + "<table><tbody><tr><th colspan=\"2\"><p>A</p></th></tr></tbody></table>"
            + "<pre><code data-language=\"csharp\">var x = a &lt; b;</code></pre>",
            html);
    }

    [Fact]
    public void Render_MakesAShortExcerptAndReadingTime()
    {
        var words = string.Join(' ', Enumerable.Repeat("soz", 450));

        var article = _renderer.Render(Doc(Paragraph(Text(words)))).Value;

        Assert.True(article.Excerpt.Length <= 161);
        Assert.EndsWith("…", article.Excerpt);
        Assert.Equal(3, article.ReadingMinutes);
    }

    [Theory]
    [InlineData("""{"type":"doc","content":[{"type":"mystery"}]}""")]
    [InlineData("""{"type":"paragraph"}""")]
    [InlineData("""{"type":"doc","content":"text"}""")]
    [InlineData("not json")]
    public void Render_RejectsUnknownOrMalformedDocuments(string json) =>
        Assert.False(_renderer.Render(json).IsSuccess);

    [Fact]
    public void Render_RejectsImagesFromOtherSites()
    {
        var result = _renderer.Render(Doc(new { type = "image", attrs = new { src = "https://evil.example/x.png" } }));

        Assert.False(result.IsSuccess);
    }
}
