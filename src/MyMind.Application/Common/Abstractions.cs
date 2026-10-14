using Microsoft.EntityFrameworkCore;
using MyMind.Domain.Articles;
using MyMind.Domain.Categories;
using MyMind.Domain.Media;

namespace MyMind.Application.Common;

public interface IAppDbContext
{
    DbSet<Category> Categories { get; }

    DbSet<Article> Articles { get; }

    DbSet<ArticleRevision> ArticleRevisions { get; }

    DbSet<ArticleRedirect> ArticleRedirects { get; }

    DbSet<MediaFile> Media { get; }

    DbSet<ArticleMedia> ArticleMedia { get; }

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

/// <summary>Where uploaded files live: a local volume now, S3-compatible storage later (plan §8.3).</summary>
public interface IFileStorage
{
    Task SaveAsync(string key, byte[] content, string contentType, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);

    /// <summary>The URL readers load the file from, e.g. <c>/uploads/{key}</c>.</summary>
    string PublicUrl(string key);

    /// <summary>The storage key behind one of our public URLs, or null for any other URL.</summary>
    string? KeyFromPublicUrl(string url);
}

/// <param name="Small">A narrower copy for srcset; null when the image is already small.</param>
public sealed record ProcessedImage(byte[] Main, int Width, int Height, byte[]? Small);

public interface IImageProcessor
{
    /// <summary>Decodes, auto-rotates, downsizes and re-encodes to WebP without metadata.</summary>
    Result<ProcessedImage> Process(byte[] data);
}

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
