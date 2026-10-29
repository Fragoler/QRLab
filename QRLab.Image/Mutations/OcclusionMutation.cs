using QRLab.Core.Models.Images;
using QRLab.Core.Models.Mutations;
using SkiaSharp;

namespace QRLab.Image.Mutations;

public sealed class OcclusionMutation : ImageMutation<OcclusionOptions>
{
    public override MutationKind Kind => MutationKind.Occlusion;

    protected override RasterImage Apply(RasterImage source, OcclusionOptions options)
    {
        if (options.Width == 0 || options.Height == 0) return source;
        using var image = SKImage.FromEncodedData(source.Data.Span);
        using var surface = SKSurface.Create(new SKImageInfo(source.Width, source.Height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        var canvas = surface.Canvas;
        canvas.DrawImage(image, 0, 0);
        canvas.Translate((float)(source.Width * options.CenterX / 100), (float)(source.Height * options.CenterY / 100));
        canvas.RotateDegrees((float)options.Angle);
        var width = (float)(source.Width * options.Width / 100);
        var height = (float)(source.Height * options.Height / 100);
        var bounds = SKRect.Create(-width / 2, -height / 2, width, height);
        using var paint = new SKPaint { Color = SKColor.Parse("#" + options.Color.TrimStart('#')), IsAntialias = true };
        switch (options.Shape)
        {
            case OcclusionShape.Rectangle: canvas.DrawRect(bounds, paint); break;
            case OcclusionShape.Ellipse: canvas.DrawOval(bounds, paint); break;
        }
        using var result = surface.Snapshot();
        using var png = result.Encode(SKEncodedImageFormat.Png, 100);
        return new RasterImage(png.AsSpan());
    }
}
