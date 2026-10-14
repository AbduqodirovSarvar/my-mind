using MyMind.Domain.Categories;

namespace MyMind.UnitTests;

public class CategoryTreeTests
{
    private static readonly Guid Backend = Guid.NewGuid();
    private static readonly Guid Dotnet = Guid.NewGuid();
    private static readonly Guid Frontend = Guid.NewGuid();

    [Fact]
    public void Validate_AcceptsTwoLevels()
    {
        var tree = new[] { (Backend, (Guid?)null, "backend"), (Dotnet, (Guid?)Backend, "dotnet"), (Frontend, (Guid?)null, "frontend") };

        Assert.Null(CategoryTree.Validate(tree));
    }

    [Fact]
    public void Validate_RejectsAThirdLevel()
    {
        var tree = new[] { (Backend, (Guid?)null, "backend"), (Dotnet, (Guid?)Backend, "dotnet"), (Frontend, (Guid?)Dotnet, "frontend") };

        Assert.NotNull(CategoryTree.Validate(tree));
    }

    [Fact]
    public void Validate_RejectsAMissingOrSelfParent()
    {
        Assert.NotNull(CategoryTree.Validate([(Backend, Guid.NewGuid(), "backend")]));
        Assert.NotNull(CategoryTree.Validate([(Backend, Backend, "backend")]));
    }

    [Fact]
    public void Validate_RejectsDuplicateSlugsAmongSiblings_IncludingRoots()
    {
        Assert.NotNull(CategoryTree.Validate([(Backend, null, "x"), (Frontend, null, "x")]));
        Assert.NotNull(CategoryTree.Validate([(Backend, null, "b"), (Dotnet, Backend, "x"), (Frontend, Backend, "x")]));
    }

    [Fact]
    public void Validate_AllowsTheSameSlugUnderDifferentParents()
    {
        var tree = new[] { (Backend, (Guid?)null, "backend"), (Frontend, (Guid?)null, "frontend"), (Dotnet, (Guid?)Backend, "intro"), (Guid.NewGuid(), (Guid?)Frontend, "intro") };

        Assert.Null(CategoryTree.Validate(tree));
    }
}
