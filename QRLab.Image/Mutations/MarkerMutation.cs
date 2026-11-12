using QRLab.Core.Models.Images;
using QRLab.Core.Models.Mutations;
using SkiaSharp;

namespace QRLab.Image.Mutations;

public sealed class MarkerMutation : ImageMutation<MarkerOptions>
{
    public override MutationKind Kind => MutationKind.Marker;

    protected override RasterImage Apply(RasterImage source, MarkerOptions options)
    {
        if (options.Strokes.Count == 0) return source;
        using var image = SKImage.FromEncodedData(source.Data.Span)
            ?? throw new ArgumentException("Не удалось прочитать исходное изображение.");
        using var surface = SKSurface.Create(new SKImageInfo(source.Width, source.Height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        surface.Canvas.Clear(SKColors.White);
        surface.Canvas.DrawImage(image, 0, 0);
        using var paint = new SKPaint
        {
            Color = SKColor.Parse("#" + options.Color.TrimStart('#')), IsAntialias = true,
            Style = SKPaintStyle.Stroke, StrokeWidth = (float)options.Thickness,
            StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round
        };
        foreach (var stroke in options.Strokes)
        {
            SKPoint Position(MarkerPoint point) => new((float)(point.X * source.Width / 100), (float)(point.Y * source.Height / 100));
            var first = Position(stroke.Points[0]);
            if (stroke.Points.Count == 1)
            {
                surface.Canvas.DrawPoint(first, paint);
                continue;
            }
            using var path = new SKPath();
            path.MoveTo(first);
            foreach (var point in stroke.Points.Skip(1)) path.LineTo(Position(point));
            surface.Canvas.DrawPath(path, paint);
        }
        using var result = surface.Snapshot();
        using var png = result.Encode(SKEncodedImageFormat.Png, 100);
        return new RasterImage(png.AsSpan());
    }
}
