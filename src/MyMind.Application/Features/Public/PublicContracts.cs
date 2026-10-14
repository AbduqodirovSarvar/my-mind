using System.Text.Json;

namespace MyMind.Application.Features.Public;

public sealed record PublicUserResponse(string UserName, string DisplayName, string? Bio);

public sealed record TreeArticle(Guid Id, string Title, string Slug);

public sealed record TreeCategory(
    Guid Id,
    string Name,
    string Slug,
    IReadOnlyList<TreeArticle> Articles,
    IReadOnlyList<TreeCategory> Children);

public sealed record TreeResponse(PublicUserResponse User, IReadOnlyList<TreeCategory> Categories);

public sealed record CategoryCrumb(string Name, string Slug);

/// <param name="CategoryPath">Root category first; the canonical URL is /@user/{path slugs}/{slug}.</param>
/// <param name="Toc">Headings for the table of contents: [{"level":2,"text":"…","id":"…"}].</param>
public sealed record PublicArticleResponse(
    Guid Id,
    string Slug,
    string Title,
    string Html,
    JsonElement Toc,
    string? Excerpt,
    int ReadingMinutes,
    DateTimeOffset PublishedAt,
    DateTimeOffset UpdatedAt,
    PublicUserResponse Author,
    IReadOnlyList<CategoryCrumb> CategoryPath);

/// <summary>A public article lookup: the article, or the slug it moved to.</summary>
public sealed record PublicArticleLookup(PublicArticleResponse? Article, string? MovedToSlug);
