namespace MyMind.Domain.Articles;

public enum ArticleStatus
{
    Draft = 0,
    Published = 1,
    Unpublished = 2,

    /// <summary>Hidden by a moderator; the author cannot publish it again.</summary>
    Blocked = 3,
}

/// <summary>
/// An article keeps two versions side by side (plan §5.2): the draft that autosave writes to,
/// and the published copy readers see. Publishing copies the draft over the published copy.
/// </summary>
public sealed class Article
{
    public const int TitleMaxLength = 200;
    public const int ExcerptMaxLength = 300;
    public const string EmptyDocument = """{"type":"doc","content":[]}""";

    private Article()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid CategoryId { get; private set; }

    /// <summary>Unique per author among non-deleted articles; the public URL is /@user/…/slug.</summary>
    public string Slug { get; private set; } = "";

    public ArticleStatus Status { get; private set; }

    // Draft (edited by the author, never shown publicly).
    public string DraftTitle { get; private set; } = "";

    public string DraftJson { get; private set; } = EmptyDocument;

    public DateTimeOffset DraftUpdatedAt { get; private set; }

    // Published copy.
    public string? Title { get; private set; }

    public string? ContentJson { get; private set; }

    public string? ContentHtml { get; private set; }

    public string? ContentText { get; private set; }

    public string? TocJson { get; private set; }

    public string? Excerpt { get; private set; }

    public bool HasCustomExcerpt { get; private set; }

    public int ReadingMinutes { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>Optimistic concurrency token (PostgreSQL xmin), exposed to clients as the ETag.</summary>
    public uint Version { get; private set; }

    public bool WasEverPublished => PublishedAt is not null;

    public static Article CreateDraft(Guid userId, Guid categoryId, string title, string slug, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = userId,
        CategoryId = categoryId,
        Slug = slug,
        Status = ArticleStatus.Draft,
        DraftTitle = title.Trim(),
        DraftJson = EmptyDocument,
        DraftUpdatedAt = now,
        CreatedAt = now,
        UpdatedAt = now,
    };

    public void SaveDraft(string title, string json, DateTimeOffset now)
    {
        DraftTitle = title.Trim();
        DraftJson = json;
        DraftUpdatedAt = now;
        UpdatedAt = now;
    }

    public void Publish(RenderedArticle rendered, DateTimeOffset now)
    {
        if (Status == ArticleStatus.Blocked)
        {
            throw new DomainException("Maqola moderator tomonidan bloklangan va e'lon qilinmaydi.");
        }

        if (string.IsNullOrWhiteSpace(DraftTitle))
        {
            throw new DomainException("E'lon qilish uchun maqolaga sarlavha bering.");
        }

        Title = DraftTitle;
        ContentJson = DraftJson;
        ContentHtml = rendered.Html;
        ContentText = rendered.Text;
        TocJson = rendered.TocJson;
        ReadingMinutes = rendered.ReadingMinutes;
        if (!HasCustomExcerpt)
        {
            Excerpt = rendered.Excerpt;
        }

        Status = ArticleStatus.Published;
        PublishedAt ??= now;
        UpdatedAt = now;
    }

    public void Unpublish(DateTimeOffset now)
    {
        if (Status != ArticleStatus.Published)
        {
            throw new DomainException("Maqola e'lon qilinmagan.");
        }

        Status = ArticleStatus.Unpublished;
        UpdatedAt = now;
    }

    public void MoveTo(Guid categoryId, DateTimeOffset now)
    {
        CategoryId = categoryId;
        UpdatedAt = now;
    }

    public void ChangeSlug(string slug, DateTimeOffset now)
    {
        Slug = slug;
        UpdatedAt = now;
    }

    /// <summary>Sets a hand-written excerpt; <c>null</c> goes back to the one generated on publish.</summary>
    public void SetExcerpt(string? excerpt, DateTimeOffset now)
    {
        HasCustomExcerpt = !string.IsNullOrWhiteSpace(excerpt);
        Excerpt = HasCustomExcerpt ? excerpt!.Trim() : null;
        UpdatedAt = now;
    }

    public void Delete(DateTimeOffset now)
    {
        DeletedAt = now;
        UpdatedAt = now;
    }
}

/// <summary>What the server derives from the draft JSON when publishing.</summary>
public sealed record RenderedArticle(string Html, string Text, string TocJson, string Excerpt, int ReadingMinutes);
