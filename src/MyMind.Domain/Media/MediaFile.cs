namespace MyMind.Domain.Media;

/// <summary>
/// An uploaded image after re-encoding (plan §8.3): always WebP, metadata stripped, at most
/// <see cref="MaxWidth"/> wide, plus a <see cref="SmallWidth"/>-wide variant for srcset when larger.
/// </summary>
public sealed class MediaFile
{
    public const long MaxUploadBytes = 5 * 1024 * 1024;
    public const long MaxPixels = 40_000_000;
    public const long QuotaBytesPerUser = 500L * 1024 * 1024;
    public const int MaxWidth = 1920;
    public const int SmallWidth = 640;
    public const int OriginalNameMaxLength = 255;

    /// <summary>An upload never placed in a saved draft is deleted after this long.</summary>
    public static readonly TimeSpan OrphanGracePeriod = TimeSpan.FromHours(24);

    private MediaFile()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>Path inside the storage, e.g. <c>{user}/{yyyy}/{id}.webp</c>; never a URL.</summary>
    public string StorageKey { get; private set; } = "";

    public string? SmallStorageKey { get; private set; }

    public string ContentType { get; private set; } = "";

    public int Width { get; private set; }

    public int Height { get; private set; }

    /// <summary>Stored bytes of both variants; counts towards the user's quota.</summary>
    public long SizeBytes { get; private set; }

    public string OriginalName { get; private set; } = "";

    public DateTimeOffset CreatedAt { get; private set; }

    public static MediaFile Create(
        Guid id, Guid userId, string storageKey, string? smallStorageKey, int width, int height, long sizeBytes,
        string originalName, DateTimeOffset now) => new()
        {
            Id = id,
            UserId = userId,
            StorageKey = storageKey,
            SmallStorageKey = smallStorageKey,
            ContentType = "image/webp",
            Width = width,
            Height = height,
            SizeBytes = sizeBytes,
            OriginalName = originalName.Length > OriginalNameMaxLength ? originalName[..OriginalNameMaxLength] : originalName,
            CreatedAt = now,
        };

    /// <summary>Storage keys: <c>{userId:N}/{year}/{id:N}.webp</c> and <c>…-640.webp</c> for the small variant.</summary>
    public static (string Main, string Small) KeysFor(Guid userId, Guid id, DateTimeOffset now)
    {
        var stem = $"{userId:N}/{now.Year}/{id:N}";
        return ($"{stem}.webp", $"{stem}-{SmallWidth}.webp");
    }
}

/// <summary>An image used in an article's draft. Links are only added, so restoring an old revision keeps its images.</summary>
public sealed class ArticleMedia
{
    private ArticleMedia()
    {
    }

    public Guid ArticleId { get; private set; }

    public Guid MediaId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static ArticleMedia Link(Guid articleId, Guid mediaId, DateTimeOffset now) =>
        new() { ArticleId = articleId, MediaId = mediaId, CreatedAt = now };
}
