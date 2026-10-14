using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMind.Application.Common;
using MyMind.Domain.Articles;
using MyMind.Domain.Media;

namespace MyMind.Application.Features.Media;

/// <summary>
/// Periodic housekeeping (plan §5.1, §8.3): removes soft-deleted articles after 30 days and
/// uploads that never made it into a saved draft after 24 hours, together with their files.
/// </summary>
public sealed class ContentCleanupService(
    IAppDbContext db,
    IFileStorage storage,
    TimeProvider clock,
    ILogger<ContentCleanupService> logger)
{
    private const int BatchSize = 200;

    public async Task<(int Articles, int Media)> RunAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();

        // Revisions, redirects and media links go with the article (ON DELETE CASCADE).
        var purgeBefore = now - Article.PurgeAfter;
        var articles = await db.Articles.IgnoreQueryFilters()
            .Where(a => a.DeletedAt != null && a.DeletedAt < purgeBefore)
            .ExecuteDeleteAsync(ct);

        var orphanBefore = now - MediaFile.OrphanGracePeriod;
        var media = 0;
        while (!ct.IsCancellationRequested)
        {
            var orphans = await db.Media
                .Where(m => m.CreatedAt < orphanBefore && !db.ArticleMedia.Any(l => l.MediaId == m.Id))
                .OrderBy(m => m.CreatedAt)
                .Take(BatchSize)
                .ToListAsync(ct);
            if (orphans.Count == 0)
            {
                break;
            }

            foreach (var orphan in orphans)
            {
                await storage.DeleteAsync(orphan.StorageKey, ct);
                if (orphan.SmallStorageKey is not null)
                {
                    await storage.DeleteAsync(orphan.SmallStorageKey, ct);
                }
            }

            db.Media.RemoveRange(orphans);
            await db.SaveChangesAsync(ct);
            media += orphans.Count;
        }

        if (articles > 0 || media > 0)
        {
            logger.LogInformation("Cleanup removed {Articles} deleted articles and {Media} unused uploads", articles, media);
        }

        return (articles, media);
    }
}
