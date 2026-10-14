using MyMind.Api.Http;

namespace MyMind.IntegrationTests;

public class ETagTests
{
    [Theory]
    [InlineData("\"835\"", 835u)]
    [InlineData("\"835-zstd\"", 835u)] // Caddy, compressed response
    [InlineData("\"835-gzip\"", 835u)]
    [InlineData("W/\"835\"", 835u)] // nginx, compressed response
    [InlineData(" \"835\" ", 835u)]
    public void ParseIfMatch_AcceptsTagsRewrittenByCompressingProxies(string header, uint expected) =>
        Assert.Equal(expected, ETags.ParseIfMatch(header));

    [Theory]
    [InlineData("")]
    [InlineData("*")]
    [InlineData("835")]
    [InlineData("\"\"")]
    [InlineData("\"abc\"")]
    [InlineData("\"-835\"")]
    [InlineData("\"99999999999\"")]
    public void ParseIfMatch_RejectsAnythingElse(string header) =>
        Assert.Null(ETags.ParseIfMatch(header));
}
