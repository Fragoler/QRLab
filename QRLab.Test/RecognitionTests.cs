using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Generation.Barcode;
using QRLab.Core.Models.Generation.Qrcode;
using QRLab.Core.Models.Images;
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
        Assert.Equal(Status.Failed, failure.Status);
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
        vm.Reading.Results.Filter = RecognitionFilter.Failed;
        Assert.Same(vm.Workspace.SelectedSample, Assert.Single(vm.Reading.Results.Items));
        vm.Reading.Results.Search = "no-match";
        Assert.Empty(vm.Reading.Results.Items);
    }
}
