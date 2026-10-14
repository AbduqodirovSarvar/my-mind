using MyMind.Domain.Common;

namespace MyMind.UnitTests;

public class SlugTests
{
    [Theory]
    [InlineData("DI nima?", "di-nima")]
    [InlineData("Oʻzbekcha gʻoya", "ozbekcha-goya")]
    [InlineData("O'zbek tili qo'llanmasi", "ozbek-tili-qollanmasi")]
    [InlineData("Ўзбек тили ва ҳаёт", "ozbek-tili-va-hayot")]
    [InlineData("Привет, мир!", "privet-mir")]
    [InlineData("  --.NET 10: yangi imkoniyatlar--  ", "net-10-yangi-imkoniyatlar")]
    [InlineData("Café crème", "cafe-creme")]
    public void From_TransliteratesAndJoinsWordsWithHyphens(string text, string expected) =>
        Assert.Equal(expected, Slug.From(text, "fallback"));

    [Theory]
    [InlineData("")]
    [InlineData("!!! ???")]
    [InlineData("中文")]
    public void From_UsesFallback_WhenNothingIsLeft(string text) =>
        Assert.Equal("maqola", Slug.From(text, "maqola"));

    [Fact]
    public void From_CutsLongTitles_AtAWordBoundary()
    {
        var slug = Slug.From(string.Join(' ', Enumerable.Repeat("uzunsoz", 20)), "x");

        Assert.True(slug.Length <= Slug.MaxLength);
        Assert.True(Slug.IsValid(slug));
        Assert.EndsWith("uzunsoz", slug);
    }

    [Fact]
    public void MakeUnique_AppendsTheFirstFreeNumber()
    {
        var taken = new HashSet<string> { "di-nima", "di-nima-2" };

        Assert.Equal("di-nima-3", Slug.MakeUnique("di-nima", taken));
        Assert.Equal("boshqa", Slug.MakeUnique("boshqa", taken));
    }

    [Theory]
    [InlineData("di-nima", true)]
    [InlineData("a", true)]
    [InlineData("Di-nima", false)]
    [InlineData("di--nima", false)]
    [InlineData("-di", false)]
    [InlineData("di_nima", false)]
    [InlineData("", false)]
    public void IsValid_AcceptsOnlyCanonicalSlugs(string slug, bool expected) =>
        Assert.Equal(expected, Slug.IsValid(slug));
}
