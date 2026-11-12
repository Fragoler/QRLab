using QRLab.Core.Models.Images;
using QRLab.Core.Models.Mutations;
using QRLab.Image.Mutations;
using QRLab.Test.Fixtures;
using QRLab.View.ViewModels.Mutations;
using SkiaSharp;
using Xunit;

namespace QRLab.Test;

public sealed class PrintDefectsTests
{
    [Fact]
    public void OnePixelDefectsHonorCountAndDoNotChangeSource()
    {
        var source = Image(128, (_, _) => SKColors.Black);
        var before = source.Data.ToArray();
        var result = new PrintDefectsMutation().Apply(source, new PrintDefectsOptions
        {
            Count = 100, Diameter = 1, Distribution = PrintDefectSizeDistribution.Fixed
        });
        using var pixels = SKBitmap.Decode(result.Data.Span);
        Assert.Equal(100, pixels.Pixels.Count(pixel => pixel == SKColors.White));
        Assert.All(pixels.Pixels, pixel => Assert.True(pixel == SKColors.Black || pixel == SKColors.White));
        Assert.Equal(before, source.Data.ToArray());
        Assert.Equal(source.Width, result.Width);
        Assert.Equal(source.Height, result.Height);
    }

    [Fact]
    public void CirclesOnlyRemoveDarkPixelsAndPreserveLightBackgroundExactly()
    {
        var dark = new SKColor(30, 40, 50);
        var light = new SKColor(200, 210, 220);
        var source = Image(64, (x, y) => x % 16 < 8 && y % 16 < 8 ? dark : light);
        var result = new PrintDefectsMutation().Apply(source, new PrintDefectsOptions { Count = 30, Diameter = 20 });
        using var pixels = SKBitmap.Decode(result.Data.Span);
        var changed = 0;
        for (var y = 0; y < 64; y++)
        for (var x = 0; x < 64; x++)
        {
            var actual = pixels.GetPixel(x, y);
            if (x % 16 >= 8 || y % 16 >= 8) Assert.Equal(light, actual);
            else
            {
                Assert.True(actual == dark || actual == SKColors.White);
                if (actual == SKColors.White) changed++;
            }
        }
        Assert.True(changed > 0);
    }

    [Fact]
    public void ZeroCountAndNoDarkPixelsPreserveSourceAndCountIsLimitedByAvailablePixels()
    {
        var mutation = new PrintDefectsMutation();
        var black = Image(16, (_, _) => SKColors.Black);
        Assert.Same(black, mutation.Apply(black, new PrintDefectsOptions { Count = 0 }));
        foreach (var color in new[] { SKColors.White, new SKColor(180, 190, 200) })
        {
            var light = Image(16, (_, _) => color);
            Assert.Same(light, mutation.Apply(light, new PrintDefectsOptions()));
        }
        var sparse = Image(16, (x, y) => x == 5 && y is >= 3 and <= 7 ? SKColors.Black : SKColors.White);
        var result = mutation.Apply(sparse, new PrintDefectsOptions { Count = 100, Diameter = 1, Distribution = PrintDefectSizeDistribution.Fixed });
        using var pixels = SKBitmap.Decode(result.Data.Span);
        Assert.All(pixels.Pixels, pixel => Assert.Equal(SKColors.White, pixel));
        using var single = SKBitmap.Decode(mutation.Apply(Image(1, (_, _) => SKColors.Black), new PrintDefectsOptions()).Data.Span);
        Assert.Equal(SKColors.White, single.GetPixel(0, 0));
    }

    [Theory]
    [InlineData(PrintDefectSizeDistribution.Fixed)]
    [InlineData(PrintDefectSizeDistribution.Uniform)]
    [InlineData(PrintDefectSizeDistribution.Normal)]
    public void SeedMakesEachDistributionDeterministic(PrintDefectSizeDistribution distribution)
    {
        var source = Image(128, (_, _) => SKColors.Black);
        var mutation = new PrintDefectsMutation();
        var options = new PrintDefectsOptions { Count = 30, Diameter = 6, Distribution = distribution, Seed = 15 };
        var first = mutation.Apply(source, options);
        Assert.Equal(first.Data.ToArray(), mutation.Apply(source, options).Data.ToArray());
        Assert.False(first.Data.Span.SequenceEqual(mutation.Apply(source, options with { Seed = 16 }).Data.Span));
    }

    [Fact]
    public void FixedSizeIgnoresVariationAndZeroVariationProducesSameDiameterForAllDistributions()
    {
        var source = Image(128, (_, _) => SKColors.Black);
        var mutation = new PrintDefectsMutation();
        var options = new PrintDefectsOptions { Count = 25, Diameter = 9, Distribution = PrintDefectSizeDistribution.Fixed };
        var fixedResult = mutation.Apply(source, options).Data.ToArray();
        Assert.Equal(fixedResult, mutation.Apply(source, options with { SizeVariationPercent = 100 }).Data.ToArray());
        foreach (var distribution in Enum.GetValues<PrintDefectSizeDistribution>())
            Assert.Equal(fixedResult, mutation.Apply(source, options with { Distribution = distribution, SizeVariationPercent = 0 }).Data.ToArray());
        var uniform = mutation.Apply(source, options with { Distribution = PrintDefectSizeDistribution.Uniform, SizeVariationPercent = 80 });
        var normal = mutation.Apply(source, options with { Distribution = PrintDefectSizeDistribution.Normal, SizeVariationPercent = 80 });
        Assert.False(fixedResult.AsSpan().SequenceEqual(uniform.Data.Span));
        Assert.False(fixedResult.AsSpan().SequenceEqual(normal.Data.Span));
        Assert.False(uniform.Data.Span.SequenceEqual(normal.Data.Span));
    }

    [Fact]
    public void InvalidParametersAreRejectedBeforeApplying()
    {
        var defaults = new PrintDefectsOptions();
        foreach (var options in new[]
        {
            defaults with { Count = -1 }, defaults with { Count = 10001 },
            defaults with { Diameter = 0 }, defaults with { Diameter = 257 },
            defaults with { Diameter = double.NaN }, defaults with { Diameter = double.PositiveInfinity },
            defaults with { SizeVariationPercent = -1 }, defaults with { SizeVariationPercent = 101 },
            defaults with { SizeVariationPercent = double.NaN }, defaults with { SizeVariationPercent = double.PositiveInfinity },
            defaults with { Distribution = (PrintDefectSizeDistribution)99 }
        }) Assert.Throws<ArgumentException>(options.Validate);
        var settings = new PrintDefectsSettingsViewModel(defaults) { Seed = 1.5m };
        Assert.Throws<ArgumentException>(settings.ToOptions);
    }

    [Fact]
    public async Task EditingDefectsRebuildsFollowingFramesAndSettingsSurviveStorageAndUndo()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"QRLab.PrintDefects-{Guid.NewGuid():N}");
        try
        {
            using var vm = TestApplicationFactory.CreateMainWindow();
            vm.Generation.Settings.Count = 2;
            vm.Generation.GenerateCommand.Execute(null);
            var editor = vm.Mutations;
            editor.AddCommand.Execute(MutationKind.PrintDefects);
            var first = editor.SelectedOperation;
            editor.AddCommand.Execute(MutationKind.GaussianBlur);
            var tail = editor.Operations[1].Image;
            editor.SelectedOperation = first;
            var settings = Assert.IsType<PrintDefectsSettingsViewModel>(editor.SelectedSettings);
            settings.Count = 80; settings.Diameter = 5; settings.Distribution = PrintDefectSizeDistribution.Normal;
            settings.SizeVariationPercent = 30; settings.Seed = 123;
            var options = Assert.IsType<PrintDefectsOptions>(editor.SelectedOperation!.Options);
            Assert.Equal(80, options.Count); Assert.Equal(5, options.Diameter);
            Assert.Equal(PrintDefectSizeDistribution.Normal, options.Distribution);
            Assert.Equal(30, options.SizeVariationPercent); Assert.Equal(123, options.Seed);
            Assert.NotSame(tail, editor.Operations[1].Image);
            var valid = editor.PreviewImage;
            settings.Diameter = double.NaN;
            Assert.Same(valid, editor.PreviewImage); Assert.True(vm.Workspace.HasError);
            settings.Diameter = 5;
            await editor.ApplyCommand.ExecuteAsync(null);
            Assert.All(vm.Workspace.Samples, sample => Assert.Equal(options, sample.Operations[0].Options));
            var samples = vm.Workspace.Samples.Select(sample => sample.Preview with { Frames = sample.State.Mutations.Frames }).ToArray();
            var storage = TestApplicationFactory.CreateStorage();
            storage.Save(samples, new(folder));
            var loaded = storage.Load(folder);
            Assert.All(loaded.Samples, sample => Assert.Equal(options, sample.Frames[0].Options));
            Assert.Equal(samples[0].Frames[0].Image.Data.ToArray(), loaded.Samples[0].Frames[0].Image.Data.ToArray());
            editor.UndoCommand.Execute(null); editor.UndoCommand.Execute(null); editor.RedoCommand.Execute(null);
            Assert.Equal(options, editor.SelectedSettings!.ToOptions());
            var previous = Assert.IsType<PrintDefectsSettingsViewModel>(editor.SelectedSettings).Seed;
            Assert.IsType<PrintDefectsSettingsViewModel>(editor.SelectedSettings).RandomizeCommand.Execute(null);
            Assert.NotEqual(previous, Assert.IsType<PrintDefectsOptions>(editor.SelectedOperation!.Options).Seed);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
    }

    private static RasterImage Image(int size, Func<int, int, SKColor> color)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Opaque));
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++) bitmap.SetPixel(x, y, color(x, y));
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return new RasterImage(png.AsSpan());
    }
}
