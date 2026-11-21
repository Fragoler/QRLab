using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Generation.Barcode;
using QRLab.Core.Models.Generation.Qrcode;
using QRLab.Core.Models.Images;
using QRLab.Core.Models.Mutations;
using QRLab.Core.Models.Recognition;
using QRLab.Core.Models.Samples;
using QRLab.Image.Generation;
using QRLab.Image.Generation.Encoders;
using QRLab.Image.Recognition;
using Xunit;

namespace QRLab.Test;

public sealed class CodeInspectionTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 25)]
    [InlineData(7, 150)]
    [InlineData(40, 1150)]
    public async Task QrLayoutAccountsForEveryCodewordAndServiceZone(int version, int alignmentCells)
    {
        var sample = Create(new QrGenerationOptions { Version = version });
        var layout = sample.Layout!;
        Assert.Equal(147, layout.Cells.Count(cell => cell.Role == ModuleRole.Finder));
        Assert.Equal(alignmentCells, layout.Cells.Count(cell => cell.Role == ModuleRole.Alignment));
        Assert.Equal(30, layout.Cells.Count(cell => cell.Role == ModuleRole.Format));
        Assert.Equal(version >= 7 ? 36 : 0, layout.Cells.Count(cell => cell.Role == ModuleRole.Version));
        Assert.Equal(ZXing.QrCode.Internal.Version.getVersionForNumber(version).TotalCodewords * 8, layout.Cells.Count(cell => cell.Codeword >= 0));
        Assert.All(layout.Cells.Where(cell => cell.Codeword >= 0).GroupBy(cell => cell.Codeword), word => Assert.Equal(8, word.Count()));
        var result = await Read(sample);
        Assert.Equal(0, result.Inspection!.DamagedCodewords);
        Assert.NotNull(result.FinalBitmap);
    }

    [Theory]
    [InlineData(QrCorrectionLevel.Low, 3)]
    [InlineData(QrCorrectionLevel.Medium, 5)]
    [InlineData(QrCorrectionLevel.Quartile, 6)]
    [InlineData(QrCorrectionLevel.High, 8)]
    public async Task QrRestoresDamagedCodewordsAndReportsActualLevelCapacity(QrCorrectionLevel level, int capacity)
    {
        var sample = Create(new QrGenerationOptions { Version = 1, Correction = level });
        var result = await Read(Change(sample, cell => cell.Codeword == 0 && cell.Bit == 0));
        Assert.Equal(Status.Success, result.Status);
        Assert.Equal(1, result.Inspection!.DamagedCodewords);
        Assert.Equal(capacity, result.Inspection.CorrectionCapacity);
        Assert.NotNull(result.FinalBitmap);
        Assert.Equal(1, result.CorrectedCodewords);
    }

    [Fact]
    public async Task QrDetectsOverloadedIndividualBlockBelowTheSummedBudget()
    {
        var sample = Create(new QrGenerationOptions { Version = 3, Correction = QrCorrectionLevel.Quartile });
        var result = await Read(Change(sample, cell => cell.Codeword is >= 0 and < 20 && cell.Codeword % 2 == 0 && cell.Bit == 0));
        Assert.Equal(Status.Failed, result.Status);
        var report = result.Inspection!;
        Assert.Equal(10, report.DamagedCodewords);
        Assert.True(report.DamagedCodewords < report.CorrectionCapacity);
        Assert.True(report.Blocks[0].ExceedsCapacity);
        Assert.False(report.Blocks[1].ExceedsCapacity);
        Assert.Null(report.Restored);
    }

    [Theory]
    [InlineData(BarcodeType.Pdf417)]
    public async Task EveryBarcodeHasObservedAndReconstructedMatrices(BarcodeType type)
    {
        var sample = Create(BarcodeGenerationOptions.Create(type) with { Template = "HELLO" });
        var result = await Read(sample);
        Assert.Equal(Status.Success, result.Status);
        var report = Assert.IsType<CodeInspection>(result.Inspection);
        Assert.NotNull(result.FinalBitmap);
        Assert.Empty(report.Blocks);
        Assert.Equal(sample.OriginalBitmap!.Bits.ToArray(), report.Observed!.Bits.ToArray());
        Assert.Equal(type == BarcodeType.Pdf417 ? 4 : 0, report.CorrectionCapacity);
    }

    [Fact]
    public async Task Pdf417RestoresDamagedSymbol()
    {
        var sample = Create(new Pdf417GenerationOptions());
        var result = await Read(Change(sample, cell => cell.Codeword == 2 && cell.Bit == 8));
        Assert.Equal(Status.Success, result.Status);
        Assert.Empty(result.Inspection!.Blocks);
        Assert.NotNull(result.FinalBitmap);
    }

    [Fact]
    public async Task Pdf417LocalStripeDamageReportsModulesWithoutEstimatingWordErrors()
    {
        var sample = Create(new Pdf417GenerationOptions());
        var layout = sample.Layout!;
        var firstScanlines = layout.Cells.Select((cell, index) => (cell, Y: index / layout.Width))
            .Where(item => item.cell.Codeword >= 0).GroupBy(item => item.cell.Codeword)
            .ToDictionary(group => group.Key, group => group.Min(item => item.Y));
        var matrix = new Pdf417Encoder().Encode(sample.Content, sample.Settings);
        for (var y = 0; y < layout.Height; y++)
            for (var x = 0; x < layout.Width; x++)
            {
                var cell = layout[x, y];
                if (cell.Codeword >= 0 && cell.Bit == 8 && y == firstScanlines[cell.Codeword]) matrix.flip(x, y);
            }
        var result = await Read(sample with
        {
            Frames = [new(new GaussianBlurOptions(), new CodeImageRenderer().Render(matrix, sample.Settings))]
        });
        Assert.Equal(Status.Success, result.Status);
        Assert.True(result.Inspection!.Modules!.Changed > result.Inspection.CorrectionCapacity);
        Assert.Empty(result.Inspection.Blocks);
        Assert.Contains("Код прочитан", result.Inspection.Conclusion);
        Assert.DoesNotContain("оценка", result.Inspection.Conclusion);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(8)]
    public async Task Pdf417CorrectionLevelIsEncodedAndReported(int level)
    {
        var sample = Create(new Pdf417GenerationOptions { CorrectionLevel = level });
        var result = await Read(sample);
        Assert.Equal(Status.Success, result.Status);
        Assert.Equal(1 << level, result.Inspection!.CorrectionCapacity);
        Assert.Equal(0, result.Inspection.DamagedCodewords);
        Assert.Contains(result.Inspection.Parameters, p => p.Name == "Уровень коррекции" && p.Value == level.ToString());
    }

    [Theory]
    [InlineData(3, QrCorrectionLevel.Quartile)]
    [InlineData(5, QrCorrectionLevel.High)]
    [InlineData(7, QrCorrectionLevel.Low)]
    public async Task QrBlockLayoutIncludesInterleavedDataAndParity(int version, QrCorrectionLevel level)
    {
        var sample = Create(new QrGenerationOptions { Version = version, Correction = level });
        var report = (await Read(sample)).Inspection!;
        var layout = sample.Layout!;
        Assert.All(layout.Cells.Where(cell => cell.Codeword < 0), cell => Assert.Equal(-1, cell.Block));
        var words = layout.Cells.Where(cell => cell.Codeword >= 0).GroupBy(cell => cell.Codeword)
            .OrderBy(group => group.Key).ToArray();
        Assert.All(words, word => Assert.Single(word.Select(cell => cell.Block).Distinct()));
        Assert.All(report.Blocks, block => Assert.Equal(block.TotalWords,
            words.Count(word => word.First().Block == block.Number - 1)));
        // The first word of each block is adjacent in the interleaved stream.
        Assert.Equal(Enumerable.Range(0, report.Blocks.Count), words.Take(report.Blocks.Count).Select(word => word.First().Block));
        Assert.Equal(layout.Cells.Select(cell => cell.Block), report.Layout.Cells.Select(cell => cell.Block));
    }

    [Fact]
    public async Task Pdf417DoesNotEstimateWordDamageOrClaimCorrectionLimitExceeded()
    {
        var sample = Create(new Pdf417GenerationOptions());
        Assert.All(sample.Layout!.Cells.Where(cell => cell.Codeword >= 0), cell => Assert.Equal(0, cell.Block));
        var result = await Read(Change(sample, cell => cell.Codeword is >= 0 and < 5 && cell.Bit == 8));
        var report = result.Inspection!;
        Assert.Empty(report.Blocks);
        Assert.True(report.Modules!.Changed > 0);
        Assert.Equal(4, report.CorrectionCapacity);
        Assert.DoesNotContain(report.Parameters, parameter => parameter.Name.Contains("Повреждено", StringComparison.OrdinalIgnoreCase)
            || parameter.Name.Contains("оценк", StringComparison.OrdinalIgnoreCase)
            || parameter.Name.Contains("Лимит превышен", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(report.Parameters, parameter => parameter.Name.Contains("стирани", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("стирани", report.Conclusion, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task LostQrFinderIsReportedAsReferenceEstimateRatherThanDecoderEvidence()
    {
        var sample = Create(new QrGenerationOptions { Version = 1 });
        var result = await Read(Change(sample, cell => cell.Role == ModuleRole.Finder));
        Assert.Equal(Status.NotFound, result.Status);
        Assert.Null(result.FinalBitmap);
        Assert.True(result.Inspection!.IsReferenceEstimate);
        Assert.Contains(result.Inspection.Regions, region => region.Name == "Поисковые узоры" && region.Changed > 0);
        Assert.Contains("Точную причину", result.Inspection.Conclusion);
    }

    private static SamplePreview Create(GenerationOptions options) => Assert.Single(new CodeGenerator().CreateSamples(options with
        { Template = "HELLO", Count = 1, Width = 768, Height = 512 }));

    private static Task<RecognitionPreview> Read(SamplePreview sample) => ReadCore(sample);
    private static async Task<RecognitionPreview> ReadCore(SamplePreview sample) => Assert.Single(await new ZxingCodeRecognizer().RecognizeAsync(
        [sample], new(10000), TestContext.Current.CancellationToken));

    private static SamplePreview Change(SamplePreview sample, Func<ModuleCell, bool> flip)
    {
        CodeEncoder encoder = sample.Settings.Kind == CodeKind.QrCode ? new QrCodeEncoder() : new Pdf417Encoder();
        var matrix = encoder.Encode(sample.Content, sample.Settings);
        var border = sample.Settings.Kind == CodeKind.QrCode ? sample.Settings.QuietZone : 0;
        var layout = sample.Layout!;
        for (var y = 0; y < layout.Height; y++)
            for (var x = 0; x < layout.Width; x++) if (flip(layout[x, y])) matrix.flip(x + border, y + border);
        return sample with { Frames = [new(new GaussianBlurOptions(), new CodeImageRenderer().Render(matrix, sample.Settings))] };
    }
}
