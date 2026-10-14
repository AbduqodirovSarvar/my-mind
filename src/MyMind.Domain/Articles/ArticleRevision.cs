namespace MyMind.Domain.Articles;

public enum RevisionKind
{
    Autosave = 0,
    Publish = 1,
}

/// <summary>A snapshot of the draft. Every publish is kept; autosaves are pruned to the latest ones.</summary>
public sealed class ArticleRevision
{
    public const int AutosavesKept = 50;

    /// <summary>Autosave runs every few seconds; a snapshot is taken at most this often.</summary>
    public static readonly TimeSpan AutosaveInterval = TimeSpan.FromMinutes(5);

    private ArticleRevision()
    {
    }

    public Guid Id { get; private set; }

    public Guid ArticleId { get; private set; }

    public RevisionKind Kind { get; private set; }

    public string Title { get; private set; } = "";

    public string ContentJson { get; private set; } = "";

    public DateTimeOffset CreatedAt { get; private set; }

    public static ArticleRevision Snapshot(Article article, RevisionKind kind, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        ArticleId = article.Id,
        Kind = kind,
        Title = article.DraftTitle,
        ContentJson = article.DraftJson,
        CreatedAt = now,
    };
}

/// <summary>An old slug of an article; public requests for it are answered with a 301.</summary>
public sealed class ArticleRedirect
{
    private ArticleRedirect()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string OldSlug { get; private set; } = "";

    public Guid ArticleId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static ArticleRedirect Create(Article article, string oldSlug, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = article.UserId,
        OldSlug = oldSlug,
        ArticleId = article.Id,
        CreatedAt = now,
    };
}
