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
        using var vm = TestApplicationFactory.CreateMainWindow();
        var workspace = vm.Workspace;
        var selected = workspace.SelectedSample;
        var originals = workspace.Samples.ToArray();
        vm.Generation.Settings.Format = CodeKind.Barcode;
        vm.Generation.Settings.Barcode.Type = BarcodeType.Ean13;
        vm.Generation.Settings.Template = "00000000{num}";
        vm.Generation.Settings.FirstNumber = 9999;
        vm.Generation.Settings.Count = 2;
        vm.Generation.GenerateCommand.Execute(null);
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
        Assert.Equal(3, vm.Generation.Settings.Qr.Version);
        Assert.Equal(512, vm.Generation.Settings.ImageWidth);
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
        Assert.Equal(CodeKind.Barcode, vm.Generation.Settings.Format);
        Assert.Equal(type, vm.Generation.Settings.Barcode.Type);
        Assert.Equal(settings, vm.Workspace.SelectedSample!.Preview.Settings);
    }

    [Fact]
    public void UiDefaultsMatchCoreModelsAndTextSourceDoesNotExpandTemplates()
    {
        var vm = TestApplicationFactory.CreateMainWindow();
        var defaults = new QrGenerationOptions();
        Assert.Equal(defaults.Template, vm.Generation.Settings.Template);
        Assert.Equal(defaults.Count, vm.Generation.Settings.Count);
        Assert.Equal(defaults.Width, vm.Generation.Settings.ImageWidth);
        Assert.Equal(defaults.Correction, vm.Generation.Settings.Qr.Correction);
        vm.Generation.Settings.Source = ContentSource.Text;
        vm.Generation.Settings.Mode = GenerationMode.Single;
        vm.Generation.GenerateCommand.Execute(null);
        Assert.Equal(defaults.Template, Assert.Single(vm.Workspace.Samples).Content);
        vm.Generation.Settings.ImageWidth = null;
        var previous = vm.Workspace.SelectedSample;
        vm.Generation.GenerateCommand.Execute(null);
        Assert.Same(previous, vm.Workspace.SelectedSample);
        Assert.True(vm.Workspace.HasError);
    }

    [Fact]
    public async Task SavingUsesFolderNameAndNeverOverwritesExistingFiles()
    {
        using var vm = TestApplicationFactory.CreateMainWindow();
        var workspace = vm.Workspace;
        vm.Generation.Settings.ApplyOptions(new QrGenerationOptions { Template = "saved", Count = 1, Width = 256, Height = 256 }, 1);
        vm.Generation.GenerateCommand.Execute(null);
        var folder = Path.Combine(_root, "Набор 42");
        vm.Generation.ExportDestination = folder;
        await vm.Generation.ConfirmExportCommand.ExecuteAsync(null);
        Assert.False(workspace.HasError);
        Assert.Equal("Набор 42", workspace.SetName);
        var manifest = File.ReadAllText(Path.Combine(folder, SampleSetStorage.ManifestName));
        await vm.Generation.ConfirmExportCommand.ExecuteAsync(null);
        Assert.True(workspace.HasError);
        Assert.Equal(manifest, File.ReadAllText(Path.Combine(folder, SampleSetStorage.ManifestName)));
    }

    [Fact]
    public void CorruptMissingAndOldSetsDoNotReplaceCurrentSamples()
    {
        using var vm = TestApplicationFactory.CreateMainWindow();
        var workspace = vm.Workspace;
        var selected = workspace.SelectedSample;
        Directory.CreateDirectory(_root);
        vm.Generation.LoadDirectory = _root;
        vm.Generation.LoadFolderCommand.Execute(null);
        Assert.True(workspace.HasError);
        File.WriteAllText(Path.Combine(_root, SampleSetStorage.ManifestName), "{broken");
        vm.Generation.LoadDirectory = _root;
        vm.Generation.LoadFolderCommand.Execute(null);
        Assert.True(workspace.HasError);
        File.WriteAllText(Path.Combine(_root, SampleSetStorage.ManifestName), """{"Version":2,"Samples":[]}""");
        vm.Generation.LoadDirectory = _root;
        vm.Generation.LoadFolderCommand.Execute(null);
        Assert.True(workspace.HasError);
        Assert.Same(selected, workspace.SelectedSample);
        Assert.Equal(GenerationDefaults.Count, workspace.Samples.Count);
        Assert.True(workspace.HasError);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
