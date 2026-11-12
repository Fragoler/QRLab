using QRLab.Core.Models.Images;
using QRLab.Core.Models.Mutations;
using QRLab.Image.Mutations;
using QRLab.Test.Fixtures;
using SkiaSharp;
using Xunit;

namespace QRLab.Test;

public sealed class MutationTests
{
    [Theory]
    [InlineData(0, 0.25, 0, 255)]
    [InlineData(0.5, 0.5, 64, 191)]
    [InlineData(0.5, 0.2, 26, 153)]
    [InlineData(0.25, 0.8, 51, 242)]
    [InlineData(1, 0.5, 128, 128)]
    [InlineData(1, 0, 0, 0)]
    [InlineData(1, 1, 255, 255)]
    public void ContrastLinearlyMovesBlackAndWhiteTowardCenter(double reduction, double center, byte black, byte white)
    {
        var source = Image(32, canvas =>
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Black };
            canvas.DrawRect(0, 0, 16, 32, paint);
        });
        var before = source.Data.ToArray();
        var result = new ContrastMutation().Apply(source, new ContrastOptions { Reduction = reduction, Center = center });
        using var pixels = Decode(result);
        Assert.Equal(new SKColor(black, black, black), pixels.GetPixel(0, 0));
        Assert.Equal(new SKColor(white, white, white), pixels.GetPixel(31, 31));
        Assert.Equal(source.Width, result.Width);
        Assert.Equal(source.Height, result.Height);
        Assert.Equal(before, source.Data.ToArray());
        if (reduction == 0) Assert.Same(source, result);
    }

    [Fact]
    public void ContrastTransformsIntermediateColorChannelsLinearly()
    {
        var source = Image(32, canvas => canvas.Clear(new SKColor(20, 100, 220)));
        using var pixels = Decode(new ContrastMutation().Apply(source, new ContrastOptions { Reduction = 0.5, Center = 0.4 }));
        Assert.All(pixels.Pixels, pixel => Assert.Equal(new SKColor(61, 101, 161), pixel));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ContrastRejectsInvalidReductionAndCenter(double value)
    {
        var mutation = new ContrastMutation();
        var source = Image(32, canvas => canvas.Clear(SKColors.White));
        Assert.Throws<ArgumentException>(() => mutation.Apply(source, new ContrastOptions { Reduction = value }));
        Assert.Throws<ArgumentException>(() => mutation.Apply(source, new ContrastOptions { Reduction = 0, Center = value }));
    }

    [Fact]
    public void ContrastEditorRebuildsChainAndRetainsParametersThroughUndoRedoAndSerialization()
    {
        using var vm = TestApplicationFactory.CreateMainWindow();
        var editor = vm.Mutations;
        editor.AddCommand.Execute(MutationKind.Contrast);
        var first = editor.Operations[0];
        editor.AddCommand.Execute(MutationKind.GaussianBlur);
        var last = editor.Operations[1].Image;
        editor.SelectedOperation = first;
        var settings = Assert.IsType<QRLab.View.ViewModels.Mutations.ContrastSettingsViewModel>(editor.SelectedSettings);
        settings.Reduction = 0.75;
        settings.Center = 0.25;
        var options = Assert.IsType<ContrastOptions>(editor.Operations[0].Options);
        Assert.Equal(0.75, options.Reduction);
        Assert.Equal(0.25, options.Center);
        Assert.NotSame(last, editor.Operations[1].Image);
        var valid = editor.PreviewImage;
        settings.Center = double.NaN;
        Assert.Same(valid, editor.PreviewImage);
        Assert.True(vm.Workspace.HasError);
        settings.Center = 0.25;
        Assert.False(vm.Workspace.HasError);
        var json = System.Text.Json.JsonSerializer.Serialize<MutationOptions>(options);
        Assert.Equal(options, System.Text.Json.JsonSerializer.Deserialize<MutationOptions>(json));
        editor.UndoCommand.Execute(null);
        editor.UndoCommand.Execute(null);
        editor.RedoCommand.Execute(null);
        var restored = Assert.IsType<QRLab.View.ViewModels.Mutations.ContrastSettingsViewModel>(editor.SelectedSettings);
        Assert.Equal(0.75, restored.Reduction);
        Assert.Equal(0.25, restored.Center);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [InlineData(100)]
    public void JpegCompressionReturnsDecodedJpegAsPngAndPreservesSource(int quality)
    {
        var source = Image(65, canvas =>
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Black };
            canvas.DrawRect(11, 13, 35, 37, paint);
        });
        var before = source.Data.ToArray();
        var result = new JpegCompressionMutation().Apply(source, new JpegCompressionOptions { Quality = quality });
        using var original = Decode(source);
        using var pixels = Decode(result);
        using var image = SKImage.FromEncodedData(source.Data.Span);
        using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, quality);
        using var expected = SKBitmap.Decode(jpeg);
        Assert.Equal(expected.Pixels, pixels.Pixels);
        Assert.False(original.Pixels.SequenceEqual(pixels.Pixels));
        Assert.Equal(source.Width, result.Width);
        Assert.Equal(source.Height, result.Height);
        Assert.Equal(before, source.Data.ToArray());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void JpegCompressionRejectsInvalidQuality(int quality) =>
        Assert.Throws<ArgumentException>(() => new JpegCompressionMutation().Apply(
            Image(32, c => c.Clear(SKColors.White)), new JpegCompressionOptions { Quality = quality }));

    [Fact]
    public void JpegQualityEditingRebuildsFollowingFramesAndSurvivesUndoRedoAndSerialization()
    {
        using var vm = TestApplicationFactory.CreateMainWindow();
        var editor = vm.Mutations;
        editor.AddCommand.Execute(MutationKind.JpegCompression);
        var first = editor.Operations[0];
        editor.AddCommand.Execute(MutationKind.GaussianBlur);
        var last = editor.Operations[1].Image;
        editor.SelectedOperation = first;
        var settings = Assert.IsType<QRLab.View.ViewModels.Mutations.JpegCompressionSettingsViewModel>(editor.SelectedSettings);
        settings.Quality = 10;
        var options = Assert.IsType<JpegCompressionOptions>(editor.Operations[0].Options);
        Assert.Equal(10, options.Quality);
        Assert.NotSame(last, editor.Operations[1].Image);
        MutationOptions serializedOptions = options;
        var json = System.Text.Json.JsonSerializer.Serialize(serializedOptions);
        Assert.Equal(options, System.Text.Json.JsonSerializer.Deserialize<MutationOptions>(json));
        editor.UndoCommand.Execute(null);
        editor.UndoCommand.Execute(null);
        editor.RedoCommand.Execute(null);
        Assert.Equal(10, Assert.IsType<QRLab.View.ViewModels.Mutations.JpegCompressionSettingsViewModel>(editor.SelectedSettings).Quality);
        Assert.Single(editor.Operations);
    }

    [Fact]
    public void EditorOffersOnlyMutationsThatPreserveTheImageGrid()
    {
        using var vm = TestApplicationFactory.CreateMainWindow();
        Assert.Equal(new[] { MutationKind.GaussianBlur, MutationKind.SaltAndPepper, MutationKind.Occlusion, MutationKind.JpegCompression, MutationKind.Contrast, MutationKind.Marker, MutationKind.Dilation, MutationKind.Erosion, MutationKind.PrintDefects },
            vm.Mutations.AvailableMutations.Select(option => option.Id));
    }

    [Theory]
    [InlineData(OcclusionShape.Rectangle)]
    [InlineData(OcclusionShape.Ellipse)]
    public void OcclusionPaintsShapeAndPreservesSource(OcclusionShape shape)
    {
        var source = Image(100, canvas => canvas.Clear(SKColors.White));
        var before = source.Data.ToArray();
        var result = new OcclusionMutation().Apply(source, new OcclusionOptions
        {
            Shape = shape, Width = 60, Height = 60, Color = "#123456"
        });
        using var pixels = Decode(result);
        Assert.Equal(new SKColor(0x12, 0x34, 0x56), pixels.GetPixel(50, 50));
        Assert.Equal(SKColors.White, pixels.GetPixel(5, 5));
        Assert.Equal(shape == OcclusionShape.Rectangle ? new SKColor(0x12, 0x34, 0x56) : SKColors.White, pixels.GetPixel(22, 22));
        Assert.Equal(source.Width, result.Width); Assert.Equal(source.Height, result.Height);
        Assert.Equal(before, source.Data.ToArray());
    }

    [Fact]
    public void OcclusionHonorsPositionRotationClippingAndZeroSize()
    {
        var source = Image(100, canvas => canvas.Clear(SKColors.White));
        var mutation = new OcclusionMutation();
        var options = new OcclusionOptions { CenterX = 25, CenterY = 50, Width = 40, Height = 10 };
        using var normal = Decode(mutation.Apply(source, options));
        using var rotated = Decode(mutation.Apply(source, options with { Angle = 90 }));
        Assert.Equal(SKColors.Gray, normal.GetPixel(40, 50));
        Assert.Equal(SKColors.White, rotated.GetPixel(40, 50));
        Assert.Equal(SKColors.Gray, rotated.GetPixel(25, 65));
        using var clipped = Decode(mutation.Apply(source, options with { CenterX = 0 }));
        Assert.Equal(SKColors.Gray, clipped.GetPixel(0, 50));
        Assert.Equal(SKColors.White, clipped.GetPixel(25, 50));
        Assert.Same(source, mutation.Apply(source, options with { Width = 0 }));
    }

    [Fact]
    public void OcclusionRejectsInvalidParameters()
    {
        var defaults = new OcclusionOptions();
        foreach (var options in new[]
        {
            defaults with { Color = "red" }, defaults with { Color = "#GG8080" },
            defaults with { Color = "#80808080" }, defaults with { CenterX = double.NaN },
            defaults with { CenterY = 101 }, defaults with { Width = -1 },
            defaults with { Height = double.PositiveInfinity }, defaults with { Angle = 181 },
            defaults with { Shape = (OcclusionShape)99 }
        }) Assert.Throws<ArgumentException>(options.Validate);
    }

    [Fact]
    public void OcclusionEditorRebuildsChainAndPreservesLastValidColor()
    {
        var editor = TestApplicationFactory.CreateMainWindow().Mutations;
        editor.AddCommand.Execute(MutationKind.Occlusion);
        Assert.IsType<QRLab.View.ViewModels.Mutations.OcclusionSettingsViewModel>(editor.SelectedSettings);
        var first = editor.Operations[0];
        editor.AddCommand.Execute(MutationKind.GaussianBlur);
        var last = editor.Operations[1];
        editor.SelectedOperation = first;
        editor.Occlusion.CenterX = 20;
        editor.Occlusion.Shape = OcclusionShape.Ellipse;
        Assert.NotSame(last.Image, editor.Operations[1].Image);
        Assert.Equal(20, Assert.IsType<OcclusionOptions>(editor.Operations[0].Options).CenterX);
        var valid = editor.PreviewImage;
        editor.Occlusion.Color = "#";
        Assert.Same(valid, editor.PreviewImage);
        Assert.True(editor.Workspace.HasError);
        editor.Occlusion.Color = "FF0000";
        Assert.Equal("FF0000", Assert.IsType<OcclusionOptions>(editor.SelectedOperation!.Options).Color);
        editor.UndoCommand.Execute(null);
        editor.RedoCommand.Execute(null);
        Assert.Equal(2, editor.Operations.Count);
    }

    [Fact]
    public void GaussianBlurSpreadsAnImpulseWithoutChangingOriginal()
    {
        var original = Image(65, canvas => { canvas.Clear(SKColors.White); using var paint = new SKPaint { Color = SKColors.Black }; canvas.DrawRect(32, 32, 1, 1, paint); });
        var before = original.Data.ToArray();
        var result = new GaussianBlurMutation().Apply(original, new GaussianBlurOptions { Sigma = 2 });
        Assert.Equal(before, original.Data.ToArray());
        using var pixels = Decode(result);
        Assert.InRange(pixels.GetPixel(32, 32).Red, (byte)1, (byte)254);
        Assert.Equal(pixels.GetPixel(31, 32), pixels.GetPixel(33, 32));
        Assert.Equal(SKColors.White, pixels.GetPixel(0, 0));
    }

    [Fact]
    public void ZeroStrengthOperationsPreserveSourceInstance()
    {
        var original = Image(128, canvas => canvas.Clear(SKColors.Gray));
        Assert.Same(original, new GaussianBlurMutation().Apply(original, new GaussianBlurOptions { Sigma = 0 }));
        Assert.Same(original, new SaltAndPepperMutation().Apply(original, new SaltAndPepperOptions { DensityPercent = 0 }));
    }

    [Fact]
    public void NoiseIsDeterministicAndHonorsDensityAndSaltRatio()
    {
        var original = Image(256, canvas => canvas.Clear(new SKColor(128, 128, 128)));
        var source = original.Data.ToArray();
        var mutation = new SaltAndPepperMutation();
        var options = new SaltAndPepperOptions { DensityPercent = 25, SaltPercent = 40, Seed = 15 };
        var result = mutation.Apply(original, options);
        Assert.Equal(result.Data.ToArray(), mutation.Apply(original, options).Data.ToArray());
        Assert.False(result.Data.Span.SequenceEqual(mutation.Apply(original, options with { Seed = 16 }).Data.Span));
        Assert.Equal(source, original.Data.ToArray());
        using var pixels = Decode(result);
        var colors = pixels.Pixels;
        var white = colors.Count(pixel => pixel.Red == 255);
        var black = colors.Count(pixel => pixel.Red == 0);
        Assert.InRange((white + black) / (double)colors.Length, .23, .27);
        Assert.InRange(white / (double)(white + black), .37, .43);
        Assert.All(colors, pixel => Assert.Equal(255, pixel.Alpha));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 255)]
    public void FullDensityNoiseCanBeAllPepperOrAllSalt(double saltPercent, byte expected)
    {
        var image = Image(128, canvas => canvas.Clear(SKColors.Gray));
        var result = new SaltAndPepperMutation().Apply(image, new SaltAndPepperOptions { DensityPercent = 100, SaltPercent = saltPercent });
        using var pixels = Decode(result);
        Assert.All(pixels.Pixels, pixel => Assert.Equal(expected, pixel.Red));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(51)]
    public void BlurRejectsInvalidSigma(double sigma) =>
        Assert.Throws<ArgumentException>(() => new GaussianBlurMutation().Apply(Image(128, c => c.Clear(SKColors.White)), new GaussianBlurOptions { Sigma = sigma }));

    [Fact]
    public void NewMutationOptionsRejectInvalidParameters()
    {
        Assert.Throws<ArgumentException>(() => new SaltAndPepperOptions { DensityPercent = -1 }.Validate());
        Assert.Throws<ArgumentException>(() => new SaltAndPepperOptions { SaltPercent = 101 }.Validate());
        Assert.Throws<ArgumentException>(() => new SaltAndPepperOptions { DensityPercent = double.NaN }.Validate());
    }

    [Fact]
    public void EditingEarlierFrameRebuildsActualNoiseAndResetKeepsOriginal()
    {
        var vm = TestApplicationFactory.CreateMainWindow();
        vm.Generation.Settings.Mode = QRLab.Core.Models.Generation.GenerationMode.Single;
        vm.Generation.Settings.ImageWidth = 256; vm.Generation.Settings.ImageHeight = 256;
        vm.Generation.GenerateCommand.Execute(null);
        var sample = vm.Workspace.SelectedSample!;
        var original = sample.OriginalImage.Data.ToArray();
        var editor = vm.Mutations;
        editor.AddCommand.Execute(MutationKind.GaussianBlur);
        editor.AddCommand.Execute(MutationKind.SaltAndPepper);
        var before = sample.Image;
        editor.SelectedOperation = editor.Operations[0];
        editor.GaussianBlur.Sigma = 4;
        Assert.NotSame(before, sample.Image);
        Assert.NotSame(editor.Operations[0].Image, editor.Operations[1].Image);
        Assert.Same(editor.Operations[0].Image, editor.PreviewImage);
        editor.ResetCommand.Execute(null);
        Assert.Same(sample.OriginalImage, sample.Image);
        Assert.Equal(original, sample.OriginalImage.Data.ToArray());
    }

    private static RasterImage Image(int size, Action<SKCanvas> draw)
    {
        using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Opaque));
        draw(surface.Canvas);
        using var image = surface.Snapshot();
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return new RasterImage(png.AsSpan());
    }
    private static SKBitmap Decode(RasterImage image)
    {
        using var stream = image.OpenRead();
        return SKBitmap.Decode(stream);
    }
}
