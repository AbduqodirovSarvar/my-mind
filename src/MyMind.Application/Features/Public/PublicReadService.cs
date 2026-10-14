using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyMind.Application.Common;
using MyMind.Domain.Articles;

namespace MyMind.Application.Features.Public;

/// <summary>Anonymous reads. Only published articles of active users are visible.</summary>
public sealed class PublicReadService(IAppDbContext db, IUserDirectory users)
{
    public async Task<PublicUserResponse?> GetUserAsync(string userName, CancellationToken ct)
    {
        var user = await users.FindPublicAsync(userName, ct);
        return user is null ? null : ToResponse(user);
    }

    /// <summary>The author's categories with their published articles; empty branches are left out.</summary>
    public async Task<TreeResponse?> GetTreeAsync(string userName, CancellationToken ct)
    {
        var user = await users.FindPublicAsync(userName, ct);
        if (user is null)
        {
            return null;
        }

        var categories = await db.Categories.AsNoTracking()
            .Where(c => c.UserId == user.Id)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new { c.Id, c.ParentId, c.Name, c.Slug })
            .ToListAsync(ct);

        var articles = await db.Articles.AsNoTracking()
            .Where(a => a.UserId == user.Id && a.Status == ArticleStatus.Published)
            .OrderBy(a => a.PublishedAt)
            .Select(a => new { a.Id, a.CategoryId, Title = a.Title!, a.Slug })
            .ToListAsync(ct);
        var articlesByCategory = articles.ToLookup(a => a.CategoryId, a => new TreeArticle(a.Id, a.Title, a.Slug));

        IReadOnlyList<TreeCategory> ChildrenOf(Guid? parentId) => categories
            .Where(c => c.ParentId == parentId)
            .Select(c => new TreeCategory(
                c.Id, c.Name, c.Slug, articlesByCategory[c.Id].ToList(), parentId is null ? ChildrenOf(c.Id) : []))
            .Where(c => c.Articles.Count > 0 || c.Children.Count > 0)
            .ToList();

        return new TreeResponse(ToResponse(user), ChildrenOf(null));
    }

    public async Task<PublicArticleLookup?> GetArticleAsync(string userName, string slug, CancellationToken ct)
    {
        var user = await users.FindPublicAsync(userName, ct);
        if (user is null)
        {
            return null;
        }

        var article = await db.Articles.AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == user.Id && a.Slug == slug && a.Status == ArticleStatus.Published, ct);

        if (article is null)
        {
            var movedTo = await db.ArticleRedirects.AsNoTracking()
                .Where(r => r.UserId == user.Id && r.OldSlug == slug)
                .Join(db.Articles.Where(a => a.Status == ArticleStatus.Published), r => r.ArticleId, a => a.Id, (r, a) => a.Slug)
                .FirstOrDefaultAsync(ct);

            return movedTo is null ? null : new PublicArticleLookup(null, movedTo);
        }

        var path = await CategoryPathAsync(article.CategoryId, ct);
        using var toc = JsonDocument.Parse(article.TocJson ?? "[]");

        return new PublicArticleLookup(
            new PublicArticleResponse(
                article.Id, article.Slug, article.Title!, article.ContentHtml!, toc.RootElement.Clone(), article.Excerpt,
                article.ReadingMinutes, article.PublishedAt!.Value, article.UpdatedAt, ToResponse(user), path),
            MovedToSlug: null);
    }

    private async Task<IReadOnlyList<CategoryCrumb>> CategoryPathAsync(Guid categoryId, CancellationToken ct)
    {
        var category = await db.Categories.AsNoTracking().FirstAsync(c => c.Id == categoryId, ct);
        var crumb = new CategoryCrumb(category.Name, category.Slug);
        if (category.ParentId is not { } parentId)
        {
            return [crumb];
        }

        var parent = await db.Categories.AsNoTracking().FirstAsync(c => c.Id == parentId, ct);
        return [new CategoryCrumb(parent.Name, parent.Slug), crumb];
    }

    private static PublicUserResponse ToResponse(PublicUser user) => new(user.UserName, user.DisplayName, user.Bio);
}
