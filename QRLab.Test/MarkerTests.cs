using QRLab.Core.Models.Images;
using QRLab.Core.Models.Mutations;
using QRLab.Image.Mutations;
using QRLab.Test.Fixtures;
using QRLab.View.ViewModels.Mutations;
using SkiaSharp;
using Xunit;

namespace QRLab.Test;

public sealed class MarkerTests
{
    [Fact]
    public void MarkerDrawsSeparateStrokesAndDotsWithRequestedColorAndThickness()
    {
        var source = WhiteImage(100);
        var before = source.Data.ToArray();
        var options = new MarkerOptions
        {
            Color = "#123456", Thickness = 10,
            Strokes = [new([new(10, 50), new(90, 50)]), new([new(20, 20)])]
        };
        var result = new MarkerMutation().Apply(source, options);
        using var pixels = SKBitmap.Decode(result.Data.Span);
        var color = new SKColor(0x12, 0x34, 0x56);
        Assert.Equal(color, pixels.GetPixel(50, 50));
        Assert.Equal(color, pixels.GetPixel(50, 53));
        Assert.Equal(SKColors.White, pixels.GetPixel(50, 58));
        Assert.Equal(color, pixels.GetPixel(20, 20));
        Assert.Equal(SKColors.White, pixels.GetPixel(15, 35));
        Assert.Equal(before, source.Data.ToArray());
        Assert.Equal(source.Width, result.Width);
        Assert.Equal(source.Height, result.Height);
        var larger = new MarkerMutation().Apply(WhiteImage(200), options);
        using var largePixels = SKBitmap.Decode(larger.Data.Span);
        Assert.Equal(color, largePixels.GetPixel(100, 100));
        Assert.Equal(color, largePixels.GetPixel(40, 40));
    }

    [Fact]
    public void MarkerRejectsInvalidOptionsAndEmptyMarkerPreservesImage()
    {
        var source = WhiteImage(100);
        var mutation = new MarkerMutation();
        Assert.Same(source, mutation.Apply(source, new MarkerOptions()));
        foreach (var options in new MarkerOptions[]
        {
            new() { Color = "#GG0000" }, new() { Color = "red" },
            new() { Thickness = 0 }, new() { Thickness = double.NaN },
            new() { Thickness = 257 }, new() { Thickness = double.PositiveInfinity },
            new() { Strokes = [new([])] }, new() { Strokes = [new([new(-1, 30)])] },
            new() { Strokes = [new([new(50, double.NaN)])] }, new() { Strokes = [new([new(50, 101)])] }
        }) Assert.Throws<ArgumentException>(() => mutation.Apply(source, options));
    }

    [Fact]
    public async Task MarkerEditsRebuildFollowingFramesAndApplyToOtherSamples()
    {
        using var vm = TestApplicationFactory.CreateMainWindow();
        vm.Generation.Settings.Count = 2;
        vm.Generation.GenerateCommand.Execute(null);
        var editor = vm.Mutations;
        editor.AddCommand.Execute(MutationKind.Marker);
        var first = editor.Operations[0];
        editor.AddCommand.Execute(MutationKind.GaussianBlur);
        var oldTail = editor.Operations[1].Image;
        editor.SelectedOperation = first;
        var marker = Assert.IsType<MarkerSettingsViewModel>(editor.SelectedSettings);
        marker.AddStroke([new(20, 30), new(60, 70)]);
        Assert.NotSame(oldTail, editor.Operations[1].Image);
        marker.Color = "#123456";
        marker.Thickness = 12;
        var options = Assert.IsType<MarkerOptions>(editor.Operations[0].Options);
        Assert.Single(options.Strokes);
        Assert.Equal(12, options.Thickness);
        var valid = editor.PreviewImage;
        marker.Color = "#";
        Assert.Same(valid, editor.PreviewImage);
        Assert.True(vm.Workspace.HasError);
        marker.Color = "#123456";
        await editor.ApplyCommand.ExecuteAsync(null);
        Assert.All(vm.Workspace.Samples, sample => Assert.Single(Assert.IsType<MarkerOptions>(sample.Operations[0].Options).Strokes));
        editor.AddCommand.Execute(MutationKind.Marker);
        Assert.Empty(Assert.IsType<MarkerOptions>(editor.SelectedOperation!.Options).Strokes);
        editor.UndoCommand.Execute(null);
        editor.UndoCommand.Execute(null);
        editor.UndoCommand.Execute(null);
        editor.RedoCommand.Execute(null);
        Assert.Single(Assert.IsType<MarkerSettingsViewModel>(editor.SelectedSettings).Strokes);
        Assert.Equal("#123456", Assert.IsType<MarkerSettingsViewModel>(editor.SelectedSettings).Color);
    }

    [Fact]
    public void MarkerStrokesAndImageSurviveSavingAndLoading()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"QRLab.Marker-{Guid.NewGuid():N}");
        try
        {
            using var vm = TestApplicationFactory.CreateMainWindow();
            vm.Generation.Settings.Mode = QRLab.Core.Models.Generation.GenerationMode.Single;
            vm.Generation.GenerateCommand.Execute(null);
            vm.Mutations.AddCommand.Execute(MutationKind.Marker);
            var marker = Assert.IsType<MarkerSettingsViewModel>(vm.Mutations.SelectedSettings);
            marker.AddStroke([new(10, 20), new(70, 80)]);
            var preview = vm.Workspace.SelectedSample!.Preview with
            {
                Frames = vm.Workspace.SelectedSample.State.Mutations.Frames
            };
            var storage = TestApplicationFactory.CreateStorage();
            storage.Save([preview], new(folder));
            var loaded = Assert.Single(storage.Load(folder).Samples);
            var frame = Assert.Single(loaded.Frames);
            var options = Assert.IsType<MarkerOptions>(frame.Options);
            Assert.Equal(marker.Strokes[0].Points, Assert.Single(options.Strokes).Points);
            Assert.Equal(marker.Color, options.Color);
            Assert.Equal(marker.Thickness, options.Thickness);
            Assert.Equal(vm.Mutations.PreviewImage!.Data.ToArray(), frame.Image.Data.ToArray());
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
    }

    private static RasterImage WhiteImage(int size)
    {
        using var surface = SKSurface.Create(new SKImageInfo(size, size));
        surface.Canvas.Clear(SKColors.White);
        using var image = surface.Snapshot();
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return new RasterImage(png.AsSpan());
    }
}
