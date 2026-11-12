using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Generation.Barcode;
using QRLab.Core.Models.Generation.Qrcode;
using QRLab.Core.Models.Images;
using QRLab.Core.Models.Mutations;
using QRLab.Image.Generation;
using QRLab.Image.Storage;
using QRLab.Image.Generation.Encoders;
using QRLab.View.ViewModels.Generation;
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
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void FixedQrMaskIsEncodedInFormatInformationAndDecodes(int mask)
    {
        var options = new QrGenerationOptions { MaskPattern = mask, Version = 2, QuietZone = 0 };
        var matrix = new QrCodeEncoder().Encode("MASK TEST", options);
        var decoded = new ZXing.QrCode.Internal.Decoder().decode(matrix, null);
        Assert.NotNull(decoded);
        Assert.Equal("MASK TEST", decoded.Text);
        Assert.Equal(mask, Assert.IsType<ZXing.QrCode.Internal.QRCodeDecoderMetaData>(decoded.Other).DataMask);
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(8)]
    public void InvalidQrMaskIsRejected(int mask) =>
        Assert.Throws<ArgumentException>(() => _generator.CreateSamples(new QrGenerationOptions { MaskPattern = mask, Count = 1 }));

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(7)]
    public void QrMaskSurvivesStorageAndSettingsRoundTrip(int mask)
    {
        var options = new QrGenerationOptions { MaskPattern = mask, Count = 1, Template = "MASK" };
        var storage = TestApplicationFactory.CreateStorage();
        var folder = Path.Combine(_root, "mask");
        storage.Save(_generator.CreateSamples(options), new(folder));
        var loaded = Assert.Single(storage.Load(folder).Samples);
        Assert.Equal(options, loaded.Settings);
        var settings = new GenerationSettingsViewModel();
        settings.ApplyOptions(loaded.Settings, 1);
        Assert.Equal(mask, settings.Qr.MaskPattern);
        Assert.Equal(mask, Assert.IsType<QrGenerationOptions>(settings.ToOptions()).MaskPattern);
        settings.Format = CodeKind.Barcode;
        Assert.IsType<Code128GenerationOptions>(settings.ToOptions());
        settings.Format = CodeKind.QrCode;
        Assert.Equal(mask, settings.Qr.MaskPattern);
    }

    [Fact]
    public void OlderQrSettingsDefaultToAutomaticMaskAndMaskChoicesHaveCorrectPatterns()
    {
        var older = System.Text.Json.JsonSerializer.Deserialize<GenerationOptions>("""{"$code":"qr"}""");
        Assert.Equal(GenerationDefaults.AutomaticQrMask, Assert.IsType<QrGenerationOptions>(older).MaskPattern);
        var choices = GenerationChoices.QrMasks;
        Assert.Equal(new[] { "Авто", "000", "001", "010", "011", "100", "101", "110", "111" }, choices.Select(c => c.Name));
        Assert.Null(choices[0].Preview);
        Assert.False(choices[0].HasPreview);
        foreach (var choice in choices.Skip(1))
        {
            var preview = choice.Preview!;
            Assert.True(choice.HasPreview);
            Assert.Equal(24, preview.Width);
            Assert.Equal(24, preview.Height);
            for (var y = 0; y < preview.Height; y++)
            for (var x = 0; x < preview.Width; x++)
            {
                var expected = choice.Value switch
                {
                    0 => (x + y) % 2 == 0,
                    1 => y % 2 == 0,
                    2 => x % 3 == 0,
                    3 => (x + y) % 3 == 0,
                    4 => (y / 2 + x / 3) % 2 == 0,
                    5 => x * y % 2 + x * y % 3 == 0,
                    6 => (x * y % 2 + x * y % 3) % 2 == 0,
                    7 => ((x + y) % 2 + x * y % 3) % 2 == 0,
                    _ => throw new InvalidOperationException()
                };
                Assert.Equal(expected, preview[x, y]);
            }
        }
    }

    [Theory]
    [InlineData("1", 41)]
    [InlineData("A", 25)]
    [InlineData("a", 16)]
    [InlineData("я", 8)]
    [InlineData("😀", 4)]
    public void CapacityMatchesActualQrEncoderBoundary(string character, int expected)
    {
        var options = new QrGenerationOptions { Version = 1, Correction = QrCorrectionLevel.Low };
        var capacity = new CodeCapacityCalculator().Calculate(options, character);
        Assert.Equal(expected, capacity);
        var encoder = new QrCodeEncoder();
        encoder.Encode(string.Concat(Enumerable.Repeat(character, capacity)), options);
        Assert.Throws<WriterException>(() => encoder.Encode(string.Concat(Enumerable.Repeat(character, capacity + 1)), options));
    }

    [Fact]
    public void CapacityAndUsageFollowSettingsAndExpandedBatchNumbers()
    {
        var settings = new GenerationSettingsViewModel
        {
            Source = ContentSource.NumberedTemplate, Template = "a{num}", FirstNumber = 9999, Count = 2
        };
        settings.Qr.Version = 1;
        settings.Qr.Correction = QrCorrectionLevel.Low;
        Assert.Equal("6 / 16", settings.ContentUsage);
        settings.Mode = GenerationMode.Single;
        Assert.Equal("5 / 16", settings.ContentUsage);
        settings.Source = ContentSource.Text;
        settings.Template = "😀";
        Assert.Equal("1 / 4", settings.ContentUsage);
        settings.Qr.Correction = QrCorrectionLevel.High;
        Assert.Equal("1 / 1", settings.ContentUsage);
        settings.Template = "";
        Assert.StartsWith("0 / ", settings.ContentUsage);
    }

    [Fact]
    public void RandomBatchUsesRequestedLengthAndSurvivesStorage()
    {
        var settings = new QrGenerationOptions
        {
            Source = ContentSource.RandomString, Template = "", RandomLength = 23, Count = 3
        };
        var samples = _generator.CreateSamples(settings);
        Assert.All(samples, sample =>
        {
            Assert.Equal(23, sample.Content.Length);
            Assert.All(sample.Content, c => Assert.True(c is >= 'a' and <= 'z' or >= '0' and <= '9'));
        });
        Assert.Equal(3, samples.Select(s => s.Content).Distinct().Count());
        var folder = Path.Combine(_root, "random");
        var storage = TestApplicationFactory.CreateStorage();
        storage.Save(samples, new(folder));
        var loaded = storage.Load(folder);
        Assert.Equal(samples.Select(s => s.Content), loaded.Samples.Select(s => s.Content));
        var vm = new GenerationSettingsViewModel();
        vm.ApplyOptions(loaded.Samples[0].Settings, loaded.Samples.Count);
        Assert.True(vm.IsRandom);
        Assert.Equal(23, vm.RandomLength);
        vm.ApplyOptions(settings, 1);
        Assert.True(vm.IsRandom);
        Assert.Equal(GenerationMode.Batch, vm.Mode);
        vm.Mode = GenerationMode.Single;
        Assert.DoesNotContain(vm.Sources, option => option.Id == ContentSource.RandomString);
        Assert.False(vm.IsRandom);
        Assert.NotEqual(ContentSource.RandomString, vm.ToOptions().Source);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(13)]
    public void RandomEanIncludesValidCheckDigit(int length)
    {
        var samples = _generator.CreateSamples(new Ean13GenerationOptions
        {
            Source = ContentSource.RandomString, RandomLength = length, Count = 2
        });
        Assert.All(samples, sample =>
        {
            Assert.Equal(13, sample.Content.Length);
            Assert.Equal(sample.Content, new Ean13GenerationOptions { Source = ContentSource.Text, Template = sample.Content }.PrepareContent(1));
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10001)]
    public void RandomSourceRejectsInvalidLength(int length) => Assert.Throws<ArgumentException>(() =>
        _generator.CreateSamples(new QrGenerationOptions { Source = ContentSource.RandomString, RandomLength = length }));

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
            [new GaussianBlurOptions { Sigma = 2 }, new SaltAndPepperOptions(),
             new OcclusionOptions { Shape = OcclusionShape.Rectangle, Color = "#123456", CenterX = 30, Angle = 45 }]);
        var sample = original with { Frames = frames };
        var folder = Path.Combine(_root, "Мой набор");
        var storage = TestApplicationFactory.CreateStorage();
        storage.Save([sample], new(folder));
        Assert.Equal(original.Image.Data.ToArray(), File.ReadAllBytes(Path.Combine(folder, "sample-0001.png")));
        Assert.Equal(frames[0].Image.Data.ToArray(), File.ReadAllBytes(Path.Combine(folder, "sample-0001-frame-0001.png")));
        Assert.Equal(4, Directory.GetFiles(folder, "*.png").Length);
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
        Assert.Equal(3, vm.Mutations.Operations.Count);
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
