using QRLab.Core.Models.Generation.Qrcode;
using QRLab.Core.Models.Images;
using QRLab.Core.Models.Mutations;
using QRLab.Core.Models.Recognition;
using QRLab.Core.Models.Samples;
using QRLab.Core.Models.Storage;
using QRLab.Image.Generation;
using QRLab.Image.Generation.Encoders;
using QRLab.Image.Recognition;
using QRLab.Image.Storage;
using QRLab.Test.Fixtures;
using QRLab.View.ViewModels;
using Xunit;

namespace QRLab.Test;

public sealed class BitmapComparisonTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task EnhancedSearchCapturesCanonicalModulesForInvertedOrRotatedQr(bool rotate, bool invert)
    {
        var sample = Assert.Single(new CodeGenerator().CreateSamples(new QrGenerationOptions { Template = "HELLO", Count = 1, Width = 256, Height = 256 }));
        using var source = SkiaSharp.SKBitmap.Decode(sample.Image.Data.ToArray());
        using var changed = new SkiaSharp.SKBitmap(source.Width, source.Height);
        for (var y = 0; y < source.Height; y++)
            for (var x = 0; x < source.Width; x++)
            {
                var color = source.GetPixel(rotate ? y : x, rotate ? source.Width - 1 - x : y);
                changed.SetPixel(x, y, invert ? new SkiaSharp.SKColor((byte)(255 - color.Red), (byte)(255 - color.Green), (byte)(255 - color.Blue)) : color);
            }
        using var image = SkiaSharp.SKImage.FromBitmap(changed);
        using var png = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        var mutated = sample with { Frames = [new(new GaussianBlurOptions(), new RasterImage(png.AsSpan()))] };
        var result = Assert.Single(await new ZxingCodeRecognizer().RecognizeAsync([mutated], new(10000, true), TestContext.Current.CancellationToken));
        Assert.Equal(Status.Success, result.Status);
        Assert.Equal(0, new BitmapComparison(sample.OriginalBitmap!, result.FinalBitmap!).Changed);
    }

    [Fact]
    public async Task DecoderUsesPreparedFinalBitmapAndSupportsInvertedSearch()
    {
        var generator = new CodeGenerator();
        var sample = Assert.Single(generator.CreateSamples(new QrGenerationOptions { Template = "FIRST", Count = 1, Width = 256, Height = 256 }));
        var recognizer = new ZxingCodeRecognizer();
        var changed = sample with { Frames = [new(new GaussianBlurOptions(), ChangedModuleImage(sample, 1))] };
        var result = Assert.Single(await recognizer.RecognizeAsync([changed], new(10000), TestContext.Current.CancellationToken));
        Assert.Equal(Status.Success, result.Status);
        Assert.Equal("FIRST", result.DecodedContent);
        Assert.Equal(1, new BitmapComparison(sample.OriginalBitmap!, result.FinalBitmap!).Changed);
        Assert.True(result.CorrectedCodewords > 0);
        Assert.Equal("H", result.ErrorCorrectionLevel);
        // A cached result must not bypass detection of the actual image in a new run.
        var clean = Assert.Single(await recognizer.RecognizeAsync([sample with { FinalBitmap = result.FinalBitmap }], new(10000, true), TestContext.Current.CancellationToken));
        Assert.Equal(0, new BitmapComparison(sample.OriginalBitmap!, clean.FinalBitmap!).Changed);
        Assert.Equal(0, clean.CorrectedCodewords);
    }

    [Theory]
    [InlineData(256, 256, 0)]
    [InlineData(768, 512, 4)]
    public async Task EncoderAndDetectorMatricesContainModulesIndependentOfPngSize(int width, int height, int quietZone)
    {
        var settings = new QrGenerationOptions { Template = "HELLO", Version = 1, Count = 1, Width = width, Height = height, QuietZone = quietZone };
        var sample = Assert.Single(new CodeGenerator().CreateSamples(settings));
        Assert.Equal(21, sample.OriginalBitmap!.Width);
        Assert.Equal(21, sample.OriginalBitmap.Height);
        var encoded = new QrCodeEncoder().Encode(sample.Content, settings);
        for (var y = 0; y < 21; y++)
            for (var x = 0; x < 21; x++) Assert.Equal(encoded[x + quietZone, y + quietZone], sample.OriginalBitmap[x, y]);
        var result = Assert.Single(await new ZxingCodeRecognizer().RecognizeAsync([sample], new(10000), TestContext.Current.CancellationToken));
        Assert.Equal(Status.Success, result.Status);
        Assert.Equal(0, new BitmapComparison(sample.OriginalBitmap, result.FinalBitmap!).Changed);
    }

    [Fact]
    public async Task KeepsDetectedModulesWhenErrorCorrectionFails()
    {
        var sample = Assert.Single(new CodeGenerator().CreateSamples(new QrGenerationOptions { Template = "HELLO", Version = 1, Count = 1, Width = 512, Height = 512 }));
        var changed = sample with { Frames = [new(new GaussianBlurOptions(), ChangedModuleImage(sample, 12))] };
        var result = Assert.Single(await new ZxingCodeRecognizer().RecognizeAsync([changed], new(10000), TestContext.Current.CancellationToken));
        Assert.Equal(Status.Failed, result.Status);
        Assert.NotNull(result.FinalBitmap);
        Assert.Equal(144, new BitmapComparison(sample.OriginalBitmap!, result.FinalBitmap).Changed);
    }

    internal static RasterImage ChangedModuleImage(SamplePreview sample, int blockSize)
    {
        var modules = sample.OriginalBitmap!;
        var border = sample.Settings.QuietZone;
        var matrix = new ZXing.Common.BitMatrix(modules.Width + border * 2, modules.Height + border * 2);
        for (var y = 0; y < modules.Height; y++)
            for (var x = 0; x < modules.Width; x++)
                matrix[x + border, y + border] = modules[x, y] ^ (x >= modules.Width - blockSize && y >= modules.Height - blockSize);
        return new CodeImageRenderer().Render(matrix, sample.Settings);
    }

    [Fact]
    public void CountsPixelTransitionsAndProtectsMatrixFromInputChanges()
    {
        byte[] data = [0b0011];
        var original = new BinaryImage(2, 2, data);
        data[0] = 0;
        var final = new BinaryImage(2, 2, [0b0101]);
        var comparison = new BitmapComparison(original, final);
        Assert.True(original[1, 0]);
        Assert.Equal(4, comparison.Total);
        Assert.Equal(2, comparison.Changed);
        Assert.Equal(1, comparison.AddedBlack);
        Assert.Equal(1, comparison.RemovedBlack);
        Assert.Equal(2, comparison.Unchanged);
        Assert.Throws<ArgumentException>(() => new BitmapComparison(original, new BinaryImage(1, 1, [0])));
        Assert.Throws<ArgumentOutOfRangeException>(() => original[2, 0]);
    }

    [Fact]
    public async Task PreservesBitmapsWhenRecognitionFailsAndTracksMutationInvalidation()
    {
        using var vm = TestApplicationFactory.CreateMainWindow();
        vm.Generation.Settings.Count = 1;
        vm.Generation.Settings.ImageWidth = vm.Generation.Settings.ImageHeight = 256;
        vm.Generation.GenerateCommand.Execute(null);
        vm.Reading.Settings.Timeout = 10000;
        await vm.Reading.RunCommand.ExecuteAsync(null);
        var sample = vm.Workspace.SelectedSample!;
        var original = sample.State.OriginalBitmap;
        Assert.NotNull(original);
        Assert.Equal(original.Bits.ToArray(), sample.State.FinalBitmap!.Bits.ToArray());
        Assert.Equal(0, vm.Comparison.Comparison!.Changed);
        vm.Mutations.Noise.DensityPercent = 100;
        vm.Mutations.Noise.SaltPercent = 0;
        vm.Mutations.AddCommand.Execute(MutationKind.SaltAndPepper);
        Assert.Same(original, sample.State.OriginalBitmap);
        Assert.Null(sample.State.FinalBitmap);
        Assert.False(vm.Comparison.HasBitmaps);
        await vm.Reading.RunCommand.ExecuteAsync(null);
        Assert.Equal(Status.NotFound, sample.Result!.Status);
        Assert.False(vm.Comparison.HasBitmaps);
        Assert.Null(sample.State.FinalBitmap);
        Assert.Contains("не смог выделить", vm.Comparison.FinalPlaceholder);
        vm.Mutations.UndoCommand.Execute(null);
        Assert.Null(sample.State.FinalBitmap);
    }

    [Fact]
    public async Task StoredBitmapsRoundTripWithoutPngAndLegacySetsStillLoad()
    {
        var sample = Assert.Single(new CodeGenerator().CreateSamples(new QrGenerationOptions { Count = 1, Width = 256, Height = 256 }));
        var result = Assert.Single(await new ZxingCodeRecognizer().RecognizeAsync([sample], new(10000), TestContext.Current.CancellationToken));
        var directory = Path.Combine(Path.GetTempPath(), $"QRLab-Bitmaps-{Guid.NewGuid():N}");
        try
        {
            var storage = new SampleSetStorage();
            storage.Save([sample with { OriginalBitmap = result.OriginalBitmap, FinalBitmap = result.FinalBitmap }], new ExportOptions(directory));
            var loaded = Assert.Single(storage.Load(directory).Samples);
            Assert.Equal(result.OriginalBitmap!.Bits.ToArray(), loaded.OriginalBitmap!.Bits.ToArray());
            Assert.Equal(result.FinalBitmap!.Bits.ToArray(), loaded.FinalBitmap!.Bits.ToArray());
            Assert.Equal(2, Directory.GetFiles(directory, "*.bits").Length);
            var manifest = Path.Combine(directory, SampleSetStorage.ManifestName);
            var legacy = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifest))!;
            legacy["Version"] = 3;
            foreach (var stored in legacy["Samples"]!.AsArray())
            {
                stored!.AsObject().Remove("OriginalBitmap");
                stored.AsObject().Remove("FinalBitmap");
            }
            File.WriteAllText(manifest, legacy.ToJsonString());
            var oldSample = Assert.Single(storage.Load(directory).Samples);
            Assert.Equal(sample.OriginalBitmap!.Bits.ToArray(), oldSample.OriginalBitmap!.Bits.ToArray());
            Assert.Null(oldSample.FinalBitmap);
            var reread = Assert.Single(await new ZxingCodeRecognizer().RecognizeAsync([oldSample], new(10000), TestContext.Current.CancellationToken));
            Assert.Equal(Status.Success, reread.Status);
            Assert.NotNull(reread.FinalBitmap);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
