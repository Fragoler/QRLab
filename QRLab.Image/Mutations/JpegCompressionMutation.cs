using QRLab.Core.Models.Images;
using QRLab.Core.Models.Mutations;
using SkiaSharp;

namespace QRLab.Image.Mutations;

public sealed class JpegCompressionMutation : ImageMutation<JpegCompressionOptions>
{
    public override MutationKind Kind => MutationKind.JpegCompression;

    protected override RasterImage Apply(RasterImage source, JpegCompressionOptions options)
    {
        using var image = SKImage.FromEncodedData(source.Data.Span)
            ?? throw new ArgumentException("Не удалось прочитать исходное изображение.");
        using var surface = SKSurface.Create(new SKImageInfo(source.Width, source.Height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        surface.Canvas.Clear(SKColors.White);
        surface.Canvas.DrawImage(image, 0, 0);
        using var flattened = surface.Snapshot();
        using var jpeg = flattened.Encode(SKEncodedImageFormat.Jpeg, options.Quality)
            ?? throw new ArgumentException("Не удалось сжать изображение в JPEG.");
        using var decoded = SKImage.FromEncodedData(jpeg)
            ?? throw new ArgumentException("Не удалось прочитать JPEG-изображение.");
        using var png = decoded.Encode(SKEncodedImageFormat.Png, 100);
        return new RasterImage(png.AsSpan());
    }
}
