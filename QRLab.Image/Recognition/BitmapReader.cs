using QRLab.Core.Models.Images;
using SkiaSharp;
using ZXing;

namespace QRLab.Image.Recognition;

internal static class BitmapReader
{
    public static LuminanceSource ReadLuminance(RasterImage source)
    {
        using var image = SKImage.FromEncodedData(source.Data.Span)
            ?? throw new ArgumentException("Не удалось прочитать изображение.");
        using var surface = SKSurface.Create(new SKImageInfo(source.Width, source.Height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        surface.Canvas.Clear(SKColors.White);
        surface.Canvas.DrawImage(image, 0, 0);
        using var pixels = surface.PeekPixels();
        var bytes = new byte[source.Width * source.Height * 4];
        var data = pixels.GetPixelSpan();
        for (var y = 0; y < source.Height; y++)
            data.Slice(y * pixels.RowBytes, source.Width * 4).CopyTo(bytes.AsSpan(y * source.Width * 4));
        return new RGBLuminanceSource(bytes, source.Width, source.Height, RGBLuminanceSource.BitmapFormat.BGRA32);
    }
}
