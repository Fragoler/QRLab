using QRLab.Core.Models.Images;
using QRLab.Core.Models.Mutations;
using QRLab.Image.Mutations.PrintDefects;
using SkiaSharp;

namespace QRLab.Image.Mutations;

public sealed class PrintDefectsMutation : ImageMutation<PrintDefectsOptions>
{
    public override MutationKind Kind => MutationKind.PrintDefects;

    protected override RasterImage Apply(RasterImage source, PrintDefectsOptions options)
    {
        if (options.Count == 0) return source;
        using var image = SKImage.FromEncodedData(source.Data.Span)
            ?? throw new ArgumentException("Не удалось прочитать исходное изображение.");
        using var surface = SKSurface.Create(new SKImageInfo(source.Width, source.Height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        surface.Canvas.Clear(SKColors.White);
        surface.Canvas.DrawImage(image, 0, 0);
        surface.Canvas.Flush();
        byte[] original;
        int rowBytes;
        using (var pixels = surface.PeekPixels())
        {
            original = pixels.GetPixelSpan().ToArray();
            rowBytes = pixels.RowBytes;
        }
        var random = new Random(options.Seed);
        var centers = new int[Math.Min(options.Count, source.Width * source.Height)];
        var darkCount = 0;
        // Reservoir sampling picks unique dark centers without allocating a list for every dark pixel.
        for (var y = 0; y < source.Height; y++)
        for (var x = 0; x < source.Width; x++)
        {
            if (!IsDark(original, y * rowBytes + x * 4)) continue;
            var slot = darkCount < centers.Length ? darkCount : random.Next(darkCount + 1);
            if (slot < centers.Length) centers[slot] = y * source.Width + x;
            darkCount++;
        }
        if (darkCount == 0) return source;
        var sampler = PrintDefectSizeSampler.For(options.Distribution);
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = false };
        for (var i = 0; i < Math.Min(centers.Length, darkCount); i++)
        {
            var diameter = sampler.Sample(random, options.Diameter, options.SizeVariationPercent);
            surface.Canvas.DrawCircle(centers[i] % source.Width + 0.5f, centers[i] / source.Width + 0.5f, (float)(diameter / 2), paint);
        }
        surface.Canvas.Flush();
        using (var pixels = surface.PeekPixels())
        {
            var result = pixels.GetPixelSpan();
            // Clip circles to the original dark foreground; preserve every light pixel exactly.
            for (var y = 0; y < source.Height; y++)
            for (var x = 0; x < source.Width; x++)
            {
                var offset = y * rowBytes + x * 4;
                if (!IsDark(original, offset)) original.AsSpan(offset, 4).CopyTo(result.Slice(offset, 4));
            }
        }
        using var output = surface.Snapshot();
        using var png = output.Encode(SKEncodedImageFormat.Png, 100);
        return new RasterImage(png.AsSpan());
    }

    private static bool IsDark(byte[] pixels, int offset) =>
        77 * pixels[offset + 2] + 150 * pixels[offset + 1] + 29 * pixels[offset] < 128 * 256;
}
