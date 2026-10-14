using MyMind.Application.Common;
using MyMind.Domain.Media;
using NetVips;

namespace MyMind.Infrastructure.Media;

/// <summary>
/// libvips via NetVips (MIT). Every upload is decoded and re-encoded, which drops anything hidden
/// in the original file; EXIF (including GPS) is not kept (plan §7.3, §8.3).
/// </summary>
public sealed class NetVipsImageProcessor : IImageProcessor
{
    private const int Quality = 80;

    static NetVipsImageProcessor()
    {
        // Each upload is processed once; libvips' operation cache would only hold memory.
        Cache.Max = 0;
    }

    public Result<ProcessedImage> Process(byte[] data)
    {
        try
        {
            // Reading the header only: reject decompression bombs before decoding any pixels.
            using (var header = Image.NewFromBuffer(data, access: Enums.Access.Sequential, failOn: Enums.FailOn.Error))
            {
                if ((long)header.Width * header.Height > MediaFile.MaxPixels)
                {
                    return Error.Validation($"Rasm juda katta: {MediaFile.MaxPixels / 1_000_000} megapikseldan oshmasligi kerak.", "file");
                }
            }

            var main = Encode(data, MediaFile.MaxWidth, out var width, out var height);
            var small = width > MediaFile.SmallWidth ? Encode(data, MediaFile.SmallWidth, out _, out _) : null;

            return new ProcessedImage(main, width, height, small);
        }
        catch (VipsException)
        {
            return Error.Validation("Rasmni o'qib bo'lmadi: fayl buzilgan yoki formati qo'llab-quvvatlanmaydi.", "file");
        }
    }

    /// <summary>Shrinks to at most <paramref name="maxWidth"/> (never enlarges), honouring EXIF orientation.</summary>
    private static byte[] Encode(byte[] data, int maxWidth, out int width, out int height)
    {
        using var image = Image.ThumbnailBuffer(data, maxWidth, height: 100_000, size: Enums.Size.Down, failOn: Enums.FailOn.Error);
        width = image.Width;
        height = image.Height;
        return image.WebpsaveBuffer(q: Quality, keep: Enums.ForeignKeep.None);
    }
}
