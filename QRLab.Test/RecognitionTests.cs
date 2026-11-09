using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Generation.Barcode;
using QRLab.Core.Models.Generation.Qrcode;
using QRLab.Core.Models.Images;
using QRLab.Core.Models.Samples;
using QRLab.Core.Interfaces.Recognition;
using QRLab.Core.Services;
using QRLab.Core.Models.Mutations;
using QRLab.Core.Models.Recognition;
using QRLab.Image.Generation;
using QRLab.Image.Recognition;
using QRLab.Test.Fixtures;
using QRLab.View.ViewModels.Reading;
using Xunit;

namespace QRLab.Test;

public sealed class RecognitionTests
{
    [Fact]
    public async Task DistinguishesEveryRecognitionStateAndFiltersEachIndependently()
    {
        using var vm = TestApplicationFactory.CreateMainWindow();
        var samples = new CodeGenerator().CreateSamples(new QrGenerationOptions
            { Template = "HELLO", Version = 1, Count = 6, Width = 512, Height = 512 }).ToArray();
        samples[0] = samples[0] with { Frames = [new(new GaussianBlurOptions(), BitmapComparisonTests.ChangedModuleImage(samples[0], 1))] };
        samples[1] = samples[1] with { Frames = TestApplicationFactory.CreateMutations().Apply(samples[1].Image,
            [new SaltAndPepperOptions { DensityPercent = 100, SaltPercent = 0 }]) };
        samples[2] = samples[2] with { Frames = [new(new GaussianBlurOptions(), BitmapComparisonTests.ChangedModuleImage(samples[2], 12))] };
        samples[3] = samples[3] with { Content = "OTHER" };
        vm.Workspace.State.Replace("Все статусы", samples);
        await new RecognitionService(vm.Workspace.State, new StatusScenarioRecognizer()).RecognizeAsync(new(10000), TestContext.Current.CancellationToken);
        Assert.Equal(new Status?[] { Status.Success, Status.NotFound, Status.Failed, Status.Mismatch, Status.Timeout, null },
            vm.Workspace.Samples.Select(sample => sample.Result?.Status));
        Assert.Null(vm.Workspace.Samples[1].State.FinalBitmap);
        Assert.NotNull(vm.Workspace.Samples[2].State.FinalBitmap);
        Assert.True(vm.Workspace.Samples[3].IsMismatch);
        Assert.False(vm.Workspace.Samples[3].IsFailure);
        var summary = vm.Reading.Results;
        Assert.Equal(1, summary.Success);
        Assert.Equal(1, summary.NotFound);
        Assert.Equal(1, summary.Undecoded);
        Assert.Equal(1, summary.Mismatch);
        Assert.Equal(1, summary.Timeout);
        Assert.Equal(1, summary.NotRecognized);
        Assert.True(summary.CorrectedCodewords > 0);
        Assert.Equal(1, summary.SamplesWithCorrection);
        foreach (var (filter, index) in new[] { (RecognitionFilter.Successful, 0), (RecognitionFilter.NotFound, 1),
                     (RecognitionFilter.Failed, 2), (RecognitionFilter.Mismatch, 3), (RecognitionFilter.Timeout, 4), (RecognitionFilter.NotRecognized, 5) })
        {
            summary.Filter = filter;
            Assert.Same(vm.Workspace.Samples[index], Assert.Single(summary.Items));
        }
        summary.Filter = RecognitionFilter.All;
        Assert.Equal(6, summary.Items.Count);
    }

    private sealed class StatusScenarioRecognizer : ICodeRecognizer
    {
        public async Task<IReadOnlyList<RecognitionPreview>> RecognizeAsync(IReadOnlyList<SamplePreview> samples,
            RecognitionOptions options, CancellationToken cancellationToken = default)
        {
            var actual = await new ZxingCodeRecognizer().RecognizeAsync(samples.Take(4).ToArray(), options, cancellationToken);
            return actual.Append(new RecognitionPreview(samples[4].Number, Status.Timeout, 10000)).ToArray();
        }
    }

    [Fact]
    public async Task ReadsUnmodifiedDefaultBatch()
    {
        var vm = TestApplicationFactory.CreateMainWindow();
        await vm.Reading.RunCommand.ExecuteAsync(null);
        Assert.All(vm.Workspace.Samples, sample =>
        {
            Assert.True(sample.IsSuccess, $"{sample.Name}: {sample.Status}, {sample.Result?.Error}");
            Assert.Equal(sample.Content, sample.Result!.DecodedContent);
        });
    }

    [Theory]
    [InlineData(CodeKind.QrCode, BarcodeType.Code128, "Привет, ZXing", 360, 360)]
    [InlineData(CodeKind.Barcode, BarcodeType.Ean13, "590123412345", 360, 360)]
    [InlineData(CodeKind.Barcode, BarcodeType.Code128, "LAB-012345", 360, 360)]
    [InlineData(CodeKind.Barcode, BarcodeType.Pdf417, "PDF417 текст", 360, 360)]
    [InlineData(CodeKind.QrCode, BarcodeType.Code128, "Привет, ZXing", 768, 512)]
    [InlineData(CodeKind.Barcode, BarcodeType.Ean13, "590123412345", 768, 512)]
    [InlineData(CodeKind.Barcode, BarcodeType.Code128, "LAB-012345", 768, 512)]
    [InlineData(CodeKind.Barcode, BarcodeType.Pdf417, "PDF417 текст", 768, 512)]
    public async Task ReadsActualPngForEachSupportedCode(CodeKind kind, BarcodeType type, string content, int width, int height)
    {
        GenerationOptions settings = kind == CodeKind.QrCode ? new QrGenerationOptions() : BarcodeGenerationOptions.Create(type);
        var sample = Assert.Single(new CodeGenerator().CreateSamples(settings with { Template = content, Count = 1, Width = width, Height = height }));
        var result = Assert.Single(await new ZxingCodeRecognizer().RecognizeAsync([sample], new(10000), TestContext.Current.CancellationToken));
        Assert.Equal(Status.Success, result.Status);
        Assert.Equal(sample.Content, result.DecodedContent);
        Assert.True(result.Milliseconds >= 0);
    }

    [Fact]
    public async Task ReadsLastMutationInsteadOfOriginalOrMetadata()
    {
        var sample = Assert.Single(new CodeGenerator().CreateSamples(new QrGenerationOptions { Template = "actual", Count = 1, Width = 256, Height = 256 }));
        var recognizer = new ZxingCodeRecognizer();
        var mismatch = Assert.Single(await recognizer.RecognizeAsync([sample with { Content = "different" }], new(10000), TestContext.Current.CancellationToken));
        Assert.Equal(Status.Mismatch, mismatch.Status);
        Assert.Equal("actual", mismatch.DecodedContent);
        var frames = TestApplicationFactory.CreateMutations().Apply(sample.Image,
            [new SaltAndPepperOptions { DensityPercent = 100, SaltPercent = 0 }]);
        var failure = Assert.Single(await recognizer.RecognizeAsync([sample with { Frames = frames }], new(10000), TestContext.Current.CancellationToken));
        Assert.Equal(Status.NotFound, failure.Status);
        Assert.Null(failure.DecodedContent);
    }

    [Fact]
    public async Task ReadingFiltersActualResultsAndInvalidatesThemAfterMutation()
    {
        var vm = TestApplicationFactory.CreateMainWindow();
        vm.Generation.Settings.Count = 2; vm.Generation.Settings.ImageWidth = 256; vm.Generation.Settings.ImageHeight = 256;
        vm.Generation.GenerateCommand.Execute(null);
        Assert.Equal(0, vm.Reading.Results.Checked);
        vm.Reading.Settings.Timeout = 10000;
        await vm.Reading.RunCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.Reading.Results.Success);
        Assert.All(vm.Workspace.Samples, sample => Assert.Equal(sample.Content, sample.Result!.DecodedContent));
        vm.Mutations.AddCommand.Execute(MutationKind.GaussianBlur);
        Assert.Equal(1, vm.Reading.Results.Checked);
        vm.Mutations.Noise.DensityPercent = 100; vm.Mutations.Noise.SaltPercent = 0;
        vm.Mutations.AddCommand.Execute(MutationKind.SaltAndPepper);
        await vm.Reading.RunCommand.ExecuteAsync(null);
        Assert.Equal(1, vm.Reading.Results.Success);
        Assert.Equal(1, vm.Reading.Results.Failed);
        vm.Reading.Results.Filter = RecognitionFilter.NotFound;
        Assert.Same(vm.Workspace.SelectedSample, Assert.Single(vm.Reading.Results.Items));
        vm.Reading.Results.Search = "no-match";
        Assert.Empty(vm.Reading.Results.Items);
    }
}
