using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Generation.Barcode;
using QRLab.Core.Models.Generation.Qrcode;
using QRLab.Core.Models.Images;
using QRLab.Core.Models.Mutations;
using QRLab.Image.Generation;
using QRLab.Image.Storage;
using QRLab.Test.Fixtures;
using SkiaSharp;
using Xunit;
using ZXing;
using ZXing.Common;

namespace QRLab.Test;

public sealed class GenerationTests : IDisposable
{
    private readonly CodeGenerator _generator = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"QRLab.Tests-{Guid.NewGuid():N}");

    [Theory]
    [InlineData(CodeKind.QrCode, BarcodeType.Code128, "Привет, QRLab!", BarcodeFormat.QR_CODE)]
    [InlineData(CodeKind.Barcode, BarcodeType.Ean13, "590123412345", BarcodeFormat.EAN_13)]
    [InlineData(CodeKind.Barcode, BarcodeType.Ean13, "5901234123457", BarcodeFormat.EAN_13)]
    [InlineData(CodeKind.Barcode, BarcodeType.Code128, "LAB-123456", BarcodeFormat.CODE_128)]
    [InlineData(CodeKind.Barcode, BarcodeType.Pdf417, "QRLab PDF417", BarcodeFormat.PDF_417)]
    [InlineData(CodeKind.Barcode, BarcodeType.Pdf417, "Проверка PDF417", BarcodeFormat.PDF_417)]
    public void GeneratedRastersDecodeToOriginalContent(CodeKind kind, BarcodeType type, string content, BarcodeFormat expectedFormat)
    {
        GenerationOptions settings = kind == CodeKind.QrCode ? new QrGenerationOptions() : BarcodeGenerationOptions.Create(type);
        var sample = Assert.Single(_generator.CreateSamples(settings with { Template = content, Count = 1 }));
        Assert.Empty(sample.Frames);
        Assert.Equal(settings.Width, sample.Image.Width);
        Assert.Equal(settings.Height, sample.Image.Height);
        using var stream = sample.Image.OpenRead();
        using var bitmap = SKBitmap.Decode(stream);
        var source = new RGBLuminanceSource(bitmap.Bytes, bitmap.Width, bitmap.Height, RGBLuminanceSource.BitmapFormat.BGRA32);
        var reader = new BarcodeReaderGeneric { Options = new DecodingOptions { TryHarder = true, PossibleFormats = [expectedFormat] } };
        var result = reader.Decode(source);
        Assert.NotNull(result);
        Assert.Equal(sample.Content, result.Text);
        Assert.Equal(expectedFormat, result.BarcodeFormat);
    }

    [Theory]
    [InlineData("abc1234567890")]
    [InlineData("123")]
    [InlineData("5901234123458")]
    [InlineData("５９０１２３４１２３４５７")]
    [InlineData("590123412345 ")]
    public void Ean13RejectsInvalidPayloads(string content)
    {
        var error = Assert.Throws<ArgumentException>(() => _generator.CreateSamples(new Ean13GenerationOptions { Template = content, Count = 1 }));
        Assert.Contains("EAN-13", error.Message);
    }

    [Fact]
    public void FixedQrVersionChangesRasterAndRejectsOverflow()
    {
        var settings = new QrGenerationOptions { Template = "A", Count = 1 };
        var automatic = Assert.Single(_generator.CreateSamples(settings));
        var fixedSize = Assert.Single(_generator.CreateSamples(settings with { Version = 5 }));
        Assert.False(automatic.Image.Data.Span.SequenceEqual(fixedSize.Image.Data.Span));
        Assert.Throws<ArgumentException>(() => _generator.CreateSamples(settings with { Template = new string('X', 100), Version = 1 }));
        Assert.Throws<ArgumentException>(() => _generator.CreateSamples(settings with { Version = 41 }));
        Assert.Throws<ArgumentException>(() => _generator.CreateSamples(settings with { Width = 128, Height = 128, Version = 40 }));
    }

    [Fact]
    public void InvalidBatchLeavesPreviousSetAndSelectionIntact()
    {
        var workspace = TestApplicationFactory.CreateWorkspace();
        var selected = workspace.SelectedSample;
        var originals = workspace.Samples.ToArray();
        Assert.False(workspace.Generate(new Ean13GenerationOptions { Template = "00000000{num}", FirstNumber = 9999, Count = 2 }));
        Assert.Equal(originals, workspace.Samples);
        Assert.Same(selected, workspace.SelectedSample);
        Assert.True(workspace.HasError);
    }

    [Fact]
    public void SavingCopiesExistingPngBytesAndRestoresFramesWithoutProcessing()
    {
        var original = Assert.Single(_generator.CreateSamples(new QrGenerationOptions
        {
            Template = "SET-{num}", FirstNumber = 12, Count = 1, Width = 512, Height = 384, Version = 3
        }));
        var frames = TestApplicationFactory.CreateMutations().Apply(original.Image,
            [new GaussianBlurOptions { Sigma = 2 }, new SaltAndPepperOptions(), new PerspectiveMutationOptions(),
             new OcclusionOptions { Shape = OcclusionShape.Rectangle, Color = "#123456", CenterX = 30, Angle = 45 }]);
        var sample = original with { Frames = frames };
        var folder = Path.Combine(_root, "Мой набор");
        var storage = TestApplicationFactory.CreateStorage();
        storage.Save([sample], new(folder));
        Assert.Equal(original.Image.Data.ToArray(), File.ReadAllBytes(Path.Combine(folder, "sample-0001.png")));
        Assert.Equal(frames[0].Image.Data.ToArray(), File.ReadAllBytes(Path.Combine(folder, "sample-0001-frame-0001.png")));
        Assert.Equal(5, Directory.GetFiles(folder, "*.png").Length);
        var loaded = Assert.Single(storage.Load(folder).Samples);
        Assert.Equal(sample.Settings, loaded.Settings);
        Assert.Equal(sample.Image.Data.ToArray(), loaded.Image.Data.ToArray());
        Assert.Equal(sample.Frames.Count, loaded.Frames.Count);
        for (var i = 0; i < frames.Count; i++)
        {
            Assert.Equal(frames[i].Options, loaded.Frames[i].Options);
            Assert.Equal(frames[i].Image.Data.ToArray(), loaded.Frames[i].Image.Data.ToArray());
        }
        Assert.NotSame(loaded.Frames[0].Image, loaded.Frames[1].Image);
        var vm = TestApplicationFactory.CreateMainWindow();
        vm.Generation.LoadDirectory = folder;
        vm.Generation.LoadFolderCommand.Execute(null);
        Assert.Equal("Мой набор", vm.Workspace.SetName);
        Assert.Equal(3, vm.Generation.QrVersion);
        Assert.Equal(512, vm.Generation.ImageWidth);
        Assert.Equal("SET-0012", vm.Workspace.SelectedSample!.Content);
        Assert.Equal(frames[^1].Image.Data.ToArray(), vm.Workspace.SelectedSample.Image.Data.ToArray());
        Assert.Equal(4, vm.Mutations.Operations.Count);
        var renamed = Path.Combine(_root, "Другое имя");
        Directory.Move(folder, renamed);
        vm.Generation.LoadDirectory = renamed;
        vm.Generation.LoadFolderCommand.Execute(null);
        Assert.Equal("Другое имя", vm.Workspace.SetName);
    }

    [Theory]
    [InlineData(BarcodeType.Ean13, "590123412345")]
    [InlineData(BarcodeType.Code128, "ABC123")]
    [InlineData(BarcodeType.Pdf417, "Текст")]
    public void SavedBarcodeRestoresConcreteModelAndTypedSelection(BarcodeType type, string content)
    {
        var settings = BarcodeGenerationOptions.Create(type) with { Template = content, Count = 1, Width = 512, Height = 384 };
        var folder = Path.Combine(_root, "barcode");
        TestApplicationFactory.CreateStorage().Save(_generator.CreateSamples(settings), new(folder));
        var vm = TestApplicationFactory.CreateMainWindow();
        vm.Generation.LoadDirectory = folder;
        vm.Generation.LoadFolderCommand.Execute(null);
        Assert.False(vm.Workspace.HasError);
        Assert.Equal(CodeKind.Barcode, vm.Generation.Format);
        Assert.Equal(type, vm.Generation.BarcodeType);
        Assert.Equal(settings, vm.Workspace.SelectedSample!.Preview.Settings);
    }

    [Fact]
    public void UiDefaultsMatchCoreModelsAndTextSourceDoesNotExpandTemplates()
    {
        var vm = TestApplicationFactory.CreateMainWindow();
        var defaults = new QrGenerationOptions();
        Assert.Equal(defaults.Template, vm.Generation.Template);
        Assert.Equal(defaults.Count, vm.Generation.Count);
        Assert.Equal(defaults.Width, vm.Generation.ImageWidth);
        Assert.Equal(defaults.Correction, vm.Generation.Correction);
        vm.Generation.Source = ContentSource.Text;
        vm.Generation.Mode = GenerationMode.Single;
        vm.Generation.GenerateCommand.Execute(null);
        Assert.Equal(defaults.Template, Assert.Single(vm.Workspace.Samples).Content);
        vm.Generation.ImageWidth = null;
        var previous = vm.Workspace.SelectedSample;
        vm.Generation.GenerateCommand.Execute(null);
        Assert.Same(previous, vm.Workspace.SelectedSample);
        Assert.True(vm.Workspace.HasError);
    }

    [Fact]
    public async Task SavingUsesFolderNameAndNeverOverwritesExistingFiles()
    {
        var workspace = TestApplicationFactory.CreateWorkspace();
        workspace.Generate(new QrGenerationOptions { Template = "saved", Count = 1, Width = 256, Height = 256 });
        var folder = Path.Combine(_root, "Набор 42");
        Assert.True(await workspace.ExportAsync(new(folder)));
        Assert.Equal("Набор 42", workspace.SetName);
        var manifest = File.ReadAllText(Path.Combine(folder, SampleSetStorage.ManifestName));
        Assert.False(await workspace.ExportAsync(new(folder)));
        Assert.Equal(manifest, File.ReadAllText(Path.Combine(folder, SampleSetStorage.ManifestName)));
    }

    [Fact]
    public void CorruptMissingAndOldSetsDoNotReplaceCurrentSamples()
    {
        var workspace = TestApplicationFactory.CreateWorkspace();
        var selected = workspace.SelectedSample;
        Directory.CreateDirectory(_root);
        Assert.Null(workspace.LoadFolder(_root));
        File.WriteAllText(Path.Combine(_root, SampleSetStorage.ManifestName), "{broken");
        Assert.Null(workspace.LoadFolder(_root));
        File.WriteAllText(Path.Combine(_root, SampleSetStorage.ManifestName), """{"Version":2,"Samples":[]}""");
        Assert.Null(workspace.LoadFolder(_root));
        Assert.Same(selected, workspace.SelectedSample);
        Assert.Equal(GenerationDefaults.Count, workspace.Samples.Count);
        Assert.True(workspace.HasError);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
