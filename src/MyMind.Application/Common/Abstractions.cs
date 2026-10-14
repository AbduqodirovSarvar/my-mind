using Microsoft.EntityFrameworkCore;
using MyMind.Domain.Articles;
using MyMind.Domain.Categories;

namespace MyMind.Application.Common;

public interface IAppDbContext
{
    DbSet<Category> Categories { get; }

    DbSet<Article> Articles { get; }

    DbSet<ArticleRevision> ArticleRevisions { get; }

    DbSet<ArticleRedirect> ArticleRedirects { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>The signed-in user of the current request.</summary>
public interface ICurrentUser
{
    /// <summary>Throws when the request is anonymous; use only on endpoints that require auth.</summary>
    Guid Id { get; }
}

public sealed record PublicUser(Guid Id, string UserName, string DisplayName, string? Bio);

/// <summary>Read access to users, which live in the Identity store.</summary>
public interface IUserDirectory
{
    /// <summary>Finds an active (not blocked) user by username, case-insensitively.</summary>
    Task<PublicUser?> FindPublicAsync(string userName, CancellationToken cancellationToken);

    Task<bool> IsEmailConfirmedAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>Turns TipTap JSON into safe HTML plus the derived fields (plan §8.2).</summary>
public interface IArticleRenderer
{
    /// <summary>Fails with a validation error when the document is malformed or uses unknown nodes.</summary>
    Result<RenderedArticle> Render(string documentJson);
}

public sealed class PublishingOptions
{
    public const string Section = "Publishing";

    /// <summary>Plan §7.1: unconfirmed users write drafts but cannot publish. Enabled with email confirmation (phase 5).</summary>
    public bool RequireConfirmedEmail { get; set; }
}

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
