namespace MyMind.Domain.Media;

public enum ImageFormat
{
    Unknown,
    Jpeg,
    Png,
    Gif,
    WebP,
}

/// <summary>Identifies uploads by their magic bytes, never by file name or declared content type (plan §7.3).</summary>
public static class ImageFormats
{
    public static ImageFormat Detect(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return ImageFormat.Jpeg;
        }

        if (data.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return ImageFormat.Png;
        }

        if (data.StartsWith("GIF87a"u8) || data.StartsWith("GIF89a"u8))
        {
            return ImageFormat.Gif;
        }

        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
        {
            return ImageFormat.WebP;
        }

        return ImageFormat.Unknown;
    }
}
