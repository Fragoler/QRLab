using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Images;
using QRLab.Core.Models.Mutations;
using QRLab.Image.Mutations;
using QRLab.Test.Fixtures;
using QRLab.View.ViewModels.Mutations;
using SkiaSharp;
using Xunit;

namespace QRLab.Test;

public sealed class MorphologyTests
{
    [Fact]
    public void DilationExpandsDarkPixelAndErosionShrinksDarkRectangle()
    {
        var source = Image(15, 11, (x, y) => x == 7 && y == 5 ? SKColors.Black : SKColors.White);
        var before = source.Data.ToArray();
        var result = new DilationMutation().Apply(source, new DilationOptions { RadiusX = 2, RadiusY = 1, Iterations = 2 });
        using var expanded = SKBitmap.Decode(result.Data.Span);
        for (var y = 0; y < 11; y++)
        for (var x = 0; x < 15; x++)
            Assert.Equal(x is >= 3 and <= 11 && y is >= 3 and <= 7 ? SKColors.Black : SKColors.White, expanded.GetPixel(x, y));
        var eroded = new ErosionMutation().Apply(result, new ErosionOptions { RadiusX = 2, RadiusY = 1 });
        using var shrunk = SKBitmap.Decode(eroded.Data.Span);
        for (var y = 0; y < 11; y++)
        for (var x = 0; x < 15; x++)
            Assert.Equal(x is >= 5 and <= 9 && y is >= 4 and <= 6 ? SKColors.Black : SKColors.White, shrunk.GetPixel(x, y));
        Assert.Equal(before, source.Data.ToArray());
        Assert.Equal(source.Width, result.Width);
        Assert.Equal(source.Height, result.Height);
    }

    [Theory]
    [InlineData(true, 0, 0, 1)]
    [InlineData(false, 0, 0, 1)]
    [InlineData(true, 2, 0, 2)]
    [InlineData(false, 2, 0, 2)]
    [InlineData(true, 0, 1, 3)]
    [InlineData(false, 0, 1, 3)]
    [InlineData(true, 1, 2, 2)]
    [InlineData(false, 1, 2, 2)]
    [InlineData(true, 50, 50, 10)]
    [InlineData(false, 50, 50, 10)]
    public void MorphologyMatchesRepeatedNeighborhoodExtremaOnColorImage(bool dilation, int radiusX, int radiusY, int iterations)
    {
        const int width = 7, height = 5;
        var random = new Random(42);
        var source = Image(width, height, (_, _) => new SKColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)));
        var before = source.Data.ToArray();
        MorphologyOptions options = dilation ? new DilationOptions() : new ErosionOptions();
        options = options with { RadiusX = radiusX, RadiusY = radiusY, Iterations = iterations };
        var result = dilation ? new DilationMutation().Apply(source, options) : new ErosionMutation().Apply(source, options);
        using var original = SKBitmap.Decode(source.Data.Span);
        var expected = original.Pixels;
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var next = new SKColor[width * height];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var neighbors = new List<SKColor>();
                for (var yy = Math.Max(0, y - radiusY); yy <= Math.Min(height - 1, y + radiusY); yy++)
                for (var xx = Math.Max(0, x - radiusX); xx <= Math.Min(width - 1, x + radiusX); xx++)
                    neighbors.Add(expected[yy * width + xx]);
                byte Extreme(Func<SKColor, byte> channel) => dilation ? neighbors.Min(channel) : neighbors.Max(channel);
                next[y * width + x] = new SKColor(Extreme(p => p.Red), Extreme(p => p.Green), Extreme(p => p.Blue));
            }
            expected = next;
        }
        using var actual = SKBitmap.Decode(result.Data.Span);
        Assert.Equal(expected, actual.Pixels);
        Assert.Equal(before, source.Data.ToArray());
        if (radiusX == 0 && radiusY == 0) Assert.Same(source, result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MorphologyPreservesUniformImagesIncludingSinglePixelAndBorders(bool dilation)
    {
        foreach (var size in new[] { (1, 1), (1, 7), (9, 1), (7, 5) })
        foreach (var color in new[] { SKColors.Black, SKColors.White, new SKColor(20, 80, 150) })
        {
            var source = Image(size.Item1, size.Item2, (_, _) => color);
            MorphologyOptions options = dilation ? new DilationOptions() : new ErosionOptions();
            options = options with { RadiusX = 50, RadiusY = 50, Iterations = 10 };
            var result = dilation ? new DilationMutation().Apply(source, options) : new ErosionMutation().Apply(source, options);
            using var pixels = SKBitmap.Decode(result.Data.Span);
            Assert.All(pixels.Pixels, pixel => Assert.Equal(color, pixel));
        }
    }

    [Theory]
    [InlineData(-1, 1, 1)]
    [InlineData(51, 1, 1)]
    [InlineData(1, -1, 1)]
    [InlineData(1, 51, 1)]
    [InlineData(1, 1, 0)]
    [InlineData(1, 1, 11)]
    public void MorphologyRejectsInvalidParameters(int x, int y, int iterations)
    {
        Assert.Throws<ArgumentException>(() => new DilationOptions { RadiusX = x, RadiusY = y, Iterations = iterations }.Validate());
        Assert.Throws<ArgumentException>(() => new ErosionOptions { RadiusX = x, RadiusY = y, Iterations = iterations }.Validate());
    }

    [Theory]
    [InlineData(MutationKind.Dilation)]
    [InlineData(MutationKind.Erosion)]
    public async Task MorphologyEditingRebuildsChainAndSurvivesStorageAndUndo(MutationKind kind)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"QRLab.Morphology-{Guid.NewGuid():N}");
        try
        {
            using var vm = TestApplicationFactory.CreateMainWindow();
            vm.Generation.Settings.Count = 2;
            vm.Generation.GenerateCommand.Execute(null);
            var editor = vm.Mutations;
            editor.AddCommand.Execute(kind);
            var first = editor.SelectedOperation;
            editor.AddCommand.Execute(MutationKind.GaussianBlur);
            var tail = editor.Operations[1].Image;
            editor.SelectedOperation = first;
            var settings = Assert.IsAssignableFrom<MorphologySettingsViewModel>(editor.SelectedSettings);
            settings.RadiusX = 2;
            settings.RadiusY = 0;
            settings.Iterations = 3;
            var options = Assert.IsAssignableFrom<MorphologyOptions>(editor.SelectedOperation!.Options);
            Assert.Equal(kind, options.Kind);
            Assert.Equal(2, options.RadiusX);
            Assert.Equal(0, options.RadiusY);
            Assert.Equal(3, options.Iterations);
            Assert.NotSame(tail, editor.Operations[1].Image);
            var valid = editor.PreviewImage;
            settings.RadiusX = -1;
            Assert.Same(valid, editor.PreviewImage);
            Assert.True(vm.Workspace.HasError);
            settings.RadiusX = 2;
            await editor.ApplyCommand.ExecuteAsync(null);
            Assert.All(vm.Workspace.Samples, sample => Assert.Equal(options, sample.Operations[0].Options));
            var samples = vm.Workspace.Samples.Select(sample => sample.Preview with { Frames = sample.State.Mutations.Frames }).ToArray();
            var storage = TestApplicationFactory.CreateStorage();
            storage.Save(samples, new(folder));
            var loaded = storage.Load(folder);
            Assert.All(loaded.Samples, sample => Assert.Equal(options, sample.Frames[0].Options));
            Assert.Equal(samples[0].Frames[1].Image.Data.ToArray(), loaded.Samples[0].Frames[1].Image.Data.ToArray());
            editor.UndoCommand.Execute(null);
            editor.UndoCommand.Execute(null);
            editor.RedoCommand.Execute(null);
            Assert.Equal(options, editor.SelectedSettings!.ToOptions());
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
    }

    private static RasterImage Image(int width, int height, Func<int, int, SKColor> color)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++) bitmap.SetPixel(x, y, color(x, y));
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return new RasterImage(png.AsSpan());
    }
}
