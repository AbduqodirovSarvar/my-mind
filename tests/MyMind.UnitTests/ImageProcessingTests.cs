using MyMind.Domain.Media;
using MyMind.Infrastructure.Media;
using NetVips;

namespace MyMind.UnitTests;

public class ImageProcessingTests
{
    private readonly NetVipsImageProcessor _processor = new();

    internal static byte[] Jpeg(int width, int height, int? orientation = null)
    {
        using var image = Image.Black(width, height, bands: 3) + 128;
        using var tagged = orientation is { } o ? image.Mutate(m => m.Set(GValue.GIntType, "orientation", o)) : image.Copy();
        return tagged.JpegsaveBuffer();
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, ImageFormat.Jpeg)]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, ImageFormat.Png)]
    [InlineData(new byte[] { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a' }, ImageFormat.Gif)]
    [InlineData(new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P' }, ImageFormat.WebP)]
    [InlineData(new byte[] { (byte)'<', (byte)'s', (byte)'v', (byte)'g' }, ImageFormat.Unknown)]
    [InlineData(new byte[] { (byte)'<', (byte)'?', (byte)'x', (byte)'m', (byte)'l' }, ImageFormat.Unknown)]
    public void Detect_ReadsMagicBytes(byte[] data, ImageFormat expected) =>
        Assert.Equal(expected, ImageFormats.Detect(data));

    [Fact]
    public void Process_ShrinksLargeImages_AndMakesASmallVariant()
    {
        var result = _processor.Process(Jpeg(3000, 2000)).Value;

        Assert.Equal(MediaFile.MaxWidth, result.Width);
        Assert.Equal(1280, result.Height);
        Assert.Equal(ImageFormat.WebP, ImageFormats.Detect(result.Main));
        using var small = Image.NewFromBuffer(result.Small!);
        Assert.Equal(MediaFile.SmallWidth, small.Width);
    }

    [Fact]
    public void Process_KeepsSmallImagesAsTheyAre()
    {
        var result = _processor.Process(Jpeg(400, 300)).Value;

        Assert.Equal((400, 300), (result.Width, result.Height));
        Assert.Null(result.Small);
    }

    [Fact]
    public void Process_AppliesExifOrientation_AndDropsAllMetadata()
    {
        // Orientation 6: stored landscape, displayed rotated 90° (portrait).
        var result = _processor.Process(Jpeg(1200, 800, orientation: 6)).Value;

        Assert.Equal((800, 1200), (result.Width, result.Height));
        using var output = Image.NewFromBuffer(result.Main);
        var fields = output.GetFields();
        Assert.DoesNotContain("exif-data", fields);
        Assert.DoesNotContain("xmp-data", fields);
        Assert.DoesNotContain("orientation", fields);
    }

    [Fact]
    public void Process_RejectsDecompressionBombs_FromTheHeader()
    {
        using var huge = Image.Black(8000, 6000);
        var png = huge.PngsaveBuffer();

        var result = _processor.Process(png);

        Assert.False(result.IsSuccess);
        Assert.Contains("megapiksel", result.Error!.Message);
    }

    [Fact]
    public void Process_RejectsCorruptFiles()
    {
        byte[] corrupt = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 5, 6, 7, 8];

        Assert.False(_processor.Process(corrupt).IsSuccess);
    }
}
