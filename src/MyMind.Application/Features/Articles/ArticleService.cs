using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyMind.Application.Common;
using MyMind.Domain;
using MyMind.Domain.Articles;
using MyMind.Domain.Common;
using MyMind.Domain.Media;

namespace MyMind.Application.Features.Articles;

/// <summary>The signed-in author's own articles. Every query is scoped to <see cref="ICurrentUser"/>.</summary>
public sealed class ArticleService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IArticleRenderer renderer,
    IUserDirectory users,
    IFileStorage storage,
    IOptions<PublishingOptions> publishing,
    TimeProvider clock)
{
    public const int MaxPageSize = 50;
    private const string NotFoundMessage = "Maqola topilmadi.";
    private const string DefaultTitle = "Nomsiz maqola";
    private const string SlugFallback = "maqola";

    public async Task<PagedResponse<ArticleListItem>> ListAsync(ArticleStatus? status, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var userId = currentUser.Id;

        var query = db.Articles.AsNoTracking().Where(a => a.UserId == userId);
        if (status is { } s)
        {
            query = query.Where(a => a.Status == s);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ArticleListItem(a.Id, a.CategoryId, a.Slug, a.Status, a.DraftTitle, a.PublishedAt, a.UpdatedAt))
            .ToListAsync(ct);

        return new PagedResponse<ArticleListItem>(items, total, page, pageSize);
    }

    public async Task<Result<ArticleDetailResponse>> GetAsync(Guid id, CancellationToken ct)
    {
        var article = await FindOwnedAsync(id, ct);
        return article is null ? Error.NotFound(NotFoundMessage) : ToDetail(article);
    }

    public async Task<Result<ArticleDetailResponse>> CreateAsync(CreateArticleRequest request, CancellationToken ct)
    {
        var userId = currentUser.Id;
        if (!await OwnsCategoryAsync(request.CategoryId, ct))
        {
            return new Error(ErrorKind.NotFound, "Kategoriya topilmadi.", "categoryId");
        }

        var title = string.IsNullOrWhiteSpace(request.Title) ? DefaultTitle : request.Title;
        var slug = await UniqueSlugAsync(Slug.From(title, SlugFallback), exceptArticleId: null, ct);
        var article = Article.CreateDraft(userId, request.CategoryId, title, slug, clock.GetUtcNow());

        db.Articles.Add(article);
        await db.SaveChangesAsync(ct);

        return ToDetail(article);
    }

    /// <summary>Autosave. Requires the version the client last saw, so two tabs cannot overwrite each other.</summary>
    public async Task<Result<ArticleDetailResponse>> SaveDraftAsync(
        Guid id, SaveDraftRequest request, uint? expectedVersion, CancellationToken ct)
    {
        if (expectedVersion is null)
        {
            return new Error(ErrorKind.PreconditionRequired, "So'rovda If-Match sarlavhasi bo'lishi kerak.");
        }

        var article = await FindOwnedAsync(id, ct);
        if (article is null)
        {
            return Error.NotFound(NotFoundMessage);
        }

        if (article.Version != expectedVersion)
        {
            return StaleCopy();
        }

        var now = clock.GetUtcNow();
        article.SaveDraft(request.Title, request.Content.GetRawText(), now);
        await SnapshotAutosaveIfDueAsync(article, now, ct);
        await LinkImagesAsync(article, now, ct);

        return await SaveAsync(article, ct);
    }

    public async Task<Result<ArticleDetailResponse>> UpdateMetaAsync(
        Guid id, UpdateArticleMetaRequest request, uint? expectedVersion, CancellationToken ct)
    {
        var article = await FindOwnedAsync(id, ct);
        if (article is null)
        {
            return Error.NotFound(NotFoundMessage);
        }

        if (expectedVersion is not null && article.Version != expectedVersion)
        {
            return StaleCopy();
        }

        if (request.CategoryId != article.CategoryId && !await OwnsCategoryAsync(request.CategoryId, ct))
        {
            return new Error(ErrorKind.NotFound, "Kategoriya topilmadi.", "categoryId");
        }

        var now = clock.GetUtcNow();
        if (request.Slug != article.Slug)
        {
            var userId = currentUser.Id;
            if (await db.Articles.AnyAsync(a => a.UserId == userId && a.Slug == request.Slug && a.Id != id, ct))
            {
                return Error.Conflict("Bu slug boshqa maqolangizda band.", "slug");
            }

            // The new slug may be an old one of some article: the live article wins from now on.
            await db.ArticleRedirects.Where(r => r.UserId == userId && r.OldSlug == request.Slug).ExecuteDeleteAsync(ct);

            if (article.WasEverPublished && !await db.ArticleRedirects.AnyAsync(r => r.UserId == userId && r.OldSlug == article.Slug, ct))
            {
                db.ArticleRedirects.Add(ArticleRedirect.Create(article, article.Slug, now));
            }

            article.ChangeSlug(request.Slug, now);
        }

        article.MoveTo(request.CategoryId, now);
        article.SetExcerpt(request.Excerpt, now);

        return await SaveAsync(article, ct);
    }

    public async Task<Result<ArticleDetailResponse>> PublishAsync(Guid id, uint? expectedVersion, CancellationToken ct)
    {
        var article = await FindOwnedAsync(id, ct);
        if (article is null)
        {
            return Error.NotFound(NotFoundMessage);
        }

        if (expectedVersion is not null && article.Version != expectedVersion)
        {
            return StaleCopy();
        }

        if (publishing.Value.RequireConfirmedEmail && !await users.IsEmailConfirmedAsync(currentUser.Id, ct))
        {
            return new Error(ErrorKind.Forbidden, "Maqola e'lon qilish uchun avval emailingizni tasdiqlang.");
        }

        var rendered = renderer.Render(article.DraftJson);
        if (!rendered.IsSuccess)
        {
            return rendered.Error!;
        }

        var now = clock.GetUtcNow();
        try
        {
            article.Publish(rendered.Value, now);
        }
        catch (DomainException e)
        {
            return Error.Validation(e.Message);
        }

        db.ArticleRevisions.Add(ArticleRevision.Snapshot(article, RevisionKind.Publish, now));

        return await SaveAsync(article, ct);
    }

    public async Task<Result<ArticleDetailResponse>> UnpublishAsync(Guid id, CancellationToken ct)
    {
        var article = await FindOwnedAsync(id, ct);
        if (article is null)
        {
            return Error.NotFound(NotFoundMessage);
        }

        try
        {
            article.Unpublish(clock.GetUtcNow());
        }
        catch (DomainException e)
        {
            return Error.Conflict(e.Message);
        }

        return await SaveAsync(article, ct);
    }

    public async Task<Result<Success>> DeleteAsync(Guid id, CancellationToken ct)
    {
        var article = await FindOwnedAsync(id, ct);
        if (article is null)
        {
            return Error.NotFound(NotFoundMessage);
        }

        article.Delete(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return new Success();
    }

    public async Task<Result<IReadOnlyList<RevisionResponse>>> ListRevisionsAsync(Guid id, CancellationToken ct)
    {
        if (await FindOwnedAsync(id, ct) is null)
        {
            return Error.NotFound(NotFoundMessage);
        }

        var revisions = await db.ArticleRevisions.AsNoTracking()
            .Where(r => r.ArticleId == id)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new RevisionResponse(r.Id, r.Kind, r.Title, r.CreatedAt))
            .ToListAsync(ct);

        return Result<IReadOnlyList<RevisionResponse>>.From(revisions);
    }

    /// <summary>Copies a revision into the draft; the published copy stays until the next publish.</summary>
    public async Task<Result<ArticleDetailResponse>> RestoreRevisionAsync(Guid id, Guid revisionId, CancellationToken ct)
    {
        var article = await FindOwnedAsync(id, ct);
        var revision = article is null
            ? null
            : await db.ArticleRevisions.FirstOrDefaultAsync(r => r.Id == revisionId && r.ArticleId == id, ct);
        if (article is null || revision is null)
        {
            return Error.NotFound("Versiya topilmadi.");
        }

        var now = clock.GetUtcNow();
        article.SaveDraft(revision.Title, revision.ContentJson, now);
        await LinkImagesAsync(article, now, ct);
        return await SaveAsync(article, ct);
    }

    private Task<Article?> FindOwnedAsync(Guid id, CancellationToken ct)
    {
        var userId = currentUser.Id;
        return db.Articles.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);
    }

    private Task<bool> OwnsCategoryAsync(Guid categoryId, CancellationToken ct)
    {
        var userId = currentUser.Id;
        return db.Categories.AnyAsync(c => c.Id == categoryId && c.UserId == userId, ct);
    }

    private async Task<string> UniqueSlugAsync(string slug, Guid? exceptArticleId, CancellationToken ct)
    {
        var userId = currentUser.Id;
        var taken = await db.Articles
            .Where(a => a.UserId == userId && a.Id != exceptArticleId && a.Slug.StartsWith(slug))
            .Select(a => a.Slug)
            .ToListAsync(ct);

        return Slug.MakeUnique(slug, taken.ToHashSet());
    }

    private async Task SnapshotAutosaveIfDueAsync(Article article, DateTimeOffset now, CancellationToken ct)
    {
        var lastAutosave = await db.ArticleRevisions
            .Where(r => r.ArticleId == article.Id && r.Kind == RevisionKind.Autosave)
            .MaxAsync(r => (DateTimeOffset?)r.CreatedAt, ct);

        if (lastAutosave is not null && now - lastAutosave < ArticleRevision.AutosaveInterval)
        {
            return;
        }

        db.ArticleRevisions.Add(ArticleRevision.Snapshot(article, RevisionKind.Autosave, now));

        var expired = await db.ArticleRevisions
            .Where(r => r.ArticleId == article.Id && r.Kind == RevisionKind.Autosave)
            .OrderByDescending(r => r.CreatedAt)
            .Skip(ArticleRevision.AutosavesKept - 1)
            .Select(r => r.Id)
            .ToListAsync(ct);
        if (expired.Count > 0)
        {
            await db.ArticleRevisions.Where(r => expired.Contains(r.Id)).ExecuteDeleteAsync(ct);
        }
    }

    /// <summary>Marks the author's own uploads used in the draft as in use, so cleanup keeps them.</summary>
    private async Task LinkImagesAsync(Article article, DateTimeOffset now, CancellationToken ct)
    {
        var keys = TipTapDocument.ImageSources(article.DraftJson)
            .Select(storage.KeyFromPublicUrl)
            .OfType<string>()
            .Distinct()
            .ToList();
        if (keys.Count == 0)
        {
            return;
        }

        var userId = currentUser.Id;
        var unlinked = await db.Media
            .Where(m => m.UserId == userId && keys.Contains(m.StorageKey))
            .Where(m => !db.ArticleMedia.Any(l => l.ArticleId == article.Id && l.MediaId == m.Id))
            .Select(m => m.Id)
            .ToListAsync(ct);

        db.ArticleMedia.AddRange(unlinked.Select(mediaId => ArticleMedia.Link(article.Id, mediaId, now)));
    }

    private async Task<Result<ArticleDetailResponse>> SaveAsync(Article article, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request changed the row between our read and write.
            return StaleCopy();
        }

        return ToDetail(article);
    }

    private static Error StaleCopy() =>
        new(ErrorKind.PreconditionFailed, "Maqola boshqa oynada o'zgartirilgan. Sahifani yangilang.");

    private static ArticleDetailResponse ToDetail(Article a)
    {
        using var draft = JsonDocument.Parse(a.DraftJson);
        var hasUnpublishedChanges = a.Status == ArticleStatus.Published
            && (a.DraftTitle != a.Title || a.DraftJson != a.ContentJson);

        return new ArticleDetailResponse(
            a.Id, a.CategoryId, a.Slug, a.Status, a.DraftTitle, draft.RootElement.Clone(), a.DraftUpdatedAt,
            a.Title, a.Excerpt, a.HasCustomExcerpt, a.PublishedAt, hasUnpublishedChanges, a.UpdatedAt, a.Version);
    }
}
