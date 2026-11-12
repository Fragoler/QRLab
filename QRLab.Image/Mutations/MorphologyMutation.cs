using QRLab.Core.Models.Images;
using QRLab.Core.Models.Mutations;
using SkiaSharp;

namespace QRLab.Image.Mutations;

public abstract class MorphologyMutation<TOptions> : ImageMutation<TOptions> where TOptions : MorphologyOptions
{
    protected abstract bool Prefer(byte candidate, byte current);

    protected sealed override RasterImage Apply(RasterImage source, TOptions options)
    {
        if (options.RadiusX == 0 && options.RadiusY == 0) return source;
        using var image = SKImage.FromEncodedData(source.Data.Span)
            ?? throw new ArgumentException("Не удалось прочитать исходное изображение.");
        using var surface = SKSurface.Create(new SKImageInfo(source.Width, source.Height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        surface.Canvas.Clear(SKColors.White);
        surface.Canvas.DrawImage(image, 0, 0);
        surface.Canvas.Flush();
        using var pixels = surface.PeekPixels();
        var data = pixels.GetPixelSpan().ToArray();
        var scratch = new byte[data.Length];
        var queue = new int[Math.Max(source.Width, source.Height)];
        // Repeating a rectangular min/max kernel is equivalent to adding its radii.
        // Two sliding-window passes keep the cost linear even for large kernels.
        Filter(data, scratch, source.Height, source.Width, pixels.RowBytes, 4,
            Math.Min(source.Width - 1, options.RadiusX * options.Iterations), queue);
        Filter(scratch, data, source.Width, source.Height, 4, pixels.RowBytes,
            Math.Min(source.Height - 1, options.RadiusY * options.Iterations), queue);
        data.AsSpan().CopyTo(pixels.GetPixelSpan());
        using var result = surface.Snapshot();
        using var png = result.Encode(SKEncodedImageFormat.Png, 100);
        return new RasterImage(png.AsSpan());
    }

    private void Filter(byte[] source, byte[] destination, int lineCount, int length, int lineStride, int pixelStride, int radius, int[] queue)
    {
        for (var line = 0; line < lineCount; line++)
        for (var channel = 0; channel < 3; channel++)
        {
            var origin = line * lineStride + channel;
            var head = 0; var tail = 0; var next = 0;
            for (var position = 0; position < length; position++)
            {
                var end = Math.Min(length - 1, position + radius);
                while (next <= end)
                {
                    var candidate = source[origin + next * pixelStride];
                    while (tail > head && Prefer(candidate, source[origin + queue[tail - 1] * pixelStride])) tail--;
                    queue[tail++] = next++;
                }
                // Clipping the neighborhood equals repeating edge pixels for min/max.
                var start = Math.Max(0, position - radius);
                while (queue[head] < start) head++;
                destination[origin + position * pixelStride] = source[origin + queue[head] * pixelStride];
            }
        }
    }
}
