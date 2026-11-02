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
        Assert.True(editor.IsOcclusion);
        var first = editor.Operations[0];
        editor.AddCommand.Execute(MutationKind.GaussianBlur);
        var last = editor.Operations[1];
        editor.SelectedOperation = first;
        editor.AreaCenterX = 20;
        editor.AreaShape = OcclusionShape.Ellipse;
        Assert.NotSame(last.Image, editor.Operations[1].Image);
        Assert.Equal(20, Assert.IsType<OcclusionOptions>(editor.Operations[0].Options).CenterX);
        var valid = editor.PreviewImage;
        editor.AreaColor = "#";
        Assert.Same(valid, editor.PreviewImage);
        Assert.True(editor.Workspace.HasError);
        editor.AreaColor = "FF0000";
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
        Assert.Same(original, new PerspectiveMutation().Apply(original, new PerspectiveMutationOptions { AngleX = 0, AngleY = 0 }));
    }

    [Fact]
    public void PerspectiveProducesATrapezoidAndKeepsCanvasAndSource()
    {
        var original = Image(160, canvas =>
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Black };
            canvas.DrawRect(20, 20, 120, 120, paint);
        });
        var source = original.Data.ToArray();
        var mutation = new PerspectiveMutation();
        var settings = new PerspectiveMutationOptions { AngleX = 35, AngleY = 0, Distance = 1.5, Interpolation = ImageInterpolation.Nearest };
        var result = mutation.Apply(original, settings);
        Assert.Equal(source, original.Data.ToArray());
        Assert.Equal(original.Width, result.Width); Assert.Equal(original.Height, result.Height);
        using var pixels = Decode(result);
        var top = Enumerable.Range(0, 160).Count(x => pixels.GetPixel(x, 45).Red < 128);
        var bottom = Enumerable.Range(0, 160).Count(x => pixels.GetPixel(x, 115).Red < 128);
        Assert.True(top > bottom, $"Expected perspective taper, top={top}, bottom={bottom}");
        var otherDistance = mutation.Apply(original, settings with { Distance = 5 });
        Assert.False(result.Data.Span.SequenceEqual(otherDistance.Data.Span));
        var tiltedY = mutation.Apply(original, settings with { AngleY = 20 });
        Assert.False(result.Data.Span.SequenceEqual(tiltedY.Data.Span));
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
        Assert.Throws<ArgumentException>(() => new PerspectiveMutationOptions { AngleX = 61 }.Validate());
        Assert.Throws<ArgumentException>(() => new PerspectiveMutationOptions { Distance = .5 }.Validate());
        Assert.Throws<ArgumentException>(() => new SaltAndPepperOptions { DensityPercent = -1 }.Validate());
        Assert.Throws<ArgumentException>(() => new SaltAndPepperOptions { SaltPercent = 101 }.Validate());
        Assert.Throws<ArgumentException>(() => new SaltAndPepperOptions { DensityPercent = double.NaN }.Validate());
    }

    [Fact]
    public void EditingEarlierFrameRebuildsActualNoiseAndResetKeepsOriginal()
    {
        var vm = TestApplicationFactory.CreateMainWindow();
        vm.Generation.Mode = QRLab.Core.Models.Generation.GenerationMode.Single;
        vm.Generation.ImageWidth = 256; vm.Generation.ImageHeight = 256;
        vm.Generation.GenerateCommand.Execute(null);
        var sample = vm.Workspace.SelectedSample!;
        var original = sample.OriginalImage.Data.ToArray();
        var editor = vm.Mutations;
        editor.AddCommand.Execute(MutationKind.GaussianBlur);
        editor.AddCommand.Execute(MutationKind.SaltAndPepper);
        var before = sample.Image;
        editor.SelectedOperation = editor.Operations[0];
        editor.Blur = 4;
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
