using Microsoft.EntityFrameworkCore;
using MyMind.Application.Common;
using MyMind.Domain.Media;

namespace MyMind.Application.Features.Media;

/// <param name="SmallUrl">A narrower copy for srcset; null when the image is already small.</param>
public sealed record MediaResponse(Guid Id, string Url, string? SmallUrl, int Width, int Height, long SizeBytes);

/// <summary>Image uploads of the signed-in user (plan §8.3).</summary>
public sealed class MediaService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IImageProcessor images,
    IFileStorage storage,
    TimeProvider clock)
{
    public async Task<Result<MediaResponse>> UploadAsync(Stream content, long length, string fileName, CancellationToken ct)
    {
        if (length <= 0)
        {
            return Error.Validation("Fayl bo'sh.", "file");
        }

        if (length > MediaFile.MaxUploadBytes)
        {
            return Error.Validation($"Rasm hajmi {MediaFile.MaxUploadBytes / 1024 / 1024} MB dan oshmasligi kerak.", "file");
        }

        var data = new byte[length];
        await content.ReadExactlyAsync(data, ct);

        if (ImageFormats.Detect(data) == ImageFormat.Unknown)
        {
            return Error.Validation("Faqat JPEG, PNG, GIF yoki WebP rasm yuklash mumkin.", "file");
        }

        var userId = currentUser.Id;
        var used = await db.Media.Where(m => m.UserId == userId).SumAsync(m => (long?)m.SizeBytes, ct) ?? 0;
        if (used >= MediaFile.QuotaBytesPerUser)
        {
            return Error.Validation($"Rasmlar uchun {MediaFile.QuotaBytesPerUser / 1024 / 1024} MB joy to'ldi.", "file");
        }

        var processed = images.Process(data);
        if (!processed.IsSuccess)
        {
            return processed.Error!;
        }

        var image = processed.Value;
        var now = clock.GetUtcNow();
        var id = Guid.CreateVersion7();
        var (mainKey, smallKey) = MediaFile.KeysFor(userId, id, now);

        await storage.SaveAsync(mainKey, image.Main, "image/webp", ct);
        if (image.Small is not null)
        {
            await storage.SaveAsync(smallKey, image.Small, "image/webp", ct);
        }

        var media = MediaFile.Create(
            id, userId, mainKey, image.Small is null ? null : smallKey, image.Width, image.Height,
            image.Main.LongLength + (image.Small?.LongLength ?? 0), Path.GetFileName(fileName), now);
        db.Media.Add(media);
        await db.SaveChangesAsync(ct);

        return new MediaResponse(
            media.Id, storage.PublicUrl(media.StorageKey),
            media.SmallStorageKey is null ? null : storage.PublicUrl(media.SmallStorageKey),
            media.Width, media.Height, media.SizeBytes);
    }
}
