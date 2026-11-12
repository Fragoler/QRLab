using QRLab.Core.Models.Images;
using QRLab.Core.Models.Mutations;
using SkiaSharp;

namespace QRLab.Image.Mutations;

public sealed class ContrastMutation : ImageMutation<ContrastOptions>
{
    public override MutationKind Kind => MutationKind.Contrast;

    protected override RasterImage Apply(RasterImage source, ContrastOptions options)
    {
        if (options.Reduction == 0) return source;

        using var image = SKImage.FromEncodedData(source.Data.Span)
            ?? throw new ArgumentException("Не удалось прочитать исходное изображение.");
        using var surface = SKSurface.Create(new SKImageInfo(source.Width, source.Height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        surface.Canvas.Clear(SKColors.White);
        surface.Canvas.DrawImage(image, 0, 0);
        surface.Canvas.Flush();
        
        using var pixels = surface.PeekPixels();
        var data = pixels.GetPixelSpan();
        var scale = 1 - options.Reduction;
        var offset = 255 * options.Center * options.Reduction;
        
        for (var y = 0; y < source.Height; y++)
        for (var x = 0; x < source.Width; x++)
        {
            var start = y * pixels.RowBytes + x * 4;
            for (var channel = 0; channel < 3; channel++)
                data[start + channel] = (byte)Math.Clamp(
                    Math.Round(data[start + channel] * scale + offset, MidpointRounding.AwayFromZero), 0, 255);
        }
        
        using var result = surface.Snapshot();
        using var png = result.Encode(SKEncodedImageFormat.Png, 100);
        return new RasterImage(png.AsSpan());
    }
}
