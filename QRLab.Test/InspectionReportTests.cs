using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Generation.Barcode;
using QRLab.Core.Models.Generation.Qrcode;
using QRLab.Core.Models.Mutations;
using QRLab.Core.Models.Recognition;
using QRLab.Core.Models.Samples;
using QRLab.Image.Generation;
using QRLab.Image.Recognition;
using Xunit;

namespace QRLab.Test;

public sealed class InspectionReportTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(40)]
    public async Task QrVersionsProduceOneCategorizedReport(int version)
    {
        var sample = Create(new QrGenerationOptions { Version = version });
        var result = await Read(sample);
        Assert.Equal(Status.Success, result.Status);
        var report = result.Inspection!;
        Assert.Equal(0, report.Modules!.Changed);
        Assert.Contains(report.Parameters, parameter => parameter.Category == InspectionCategory.Symbol
            && parameter.Name == "Версия QR" && parameter.Value == version.ToString());
        Assert.Contains(report.Parameters, parameter => parameter.Category == InspectionCategory.Correction
            && parameter.Name == "Уровень коррекции" && parameter.Value == "H");
        Assert.All(report.Sections, section => Assert.All(section.Parameters, parameter => Assert.Equal(section.Category, parameter.Category)));
        Assert.Equal(report.Parameters, report.Sections.SelectMany(section => section.Parameters));
        Assert.All(report.Sections, section => Assert.Equal(section.Parameters.Count, section.Parameters.Select(parameter => parameter.Name).Distinct().Count()));
    }

    [Theory]
    [InlineData(BarcodeType.Ean13)]
    [InlineData(BarcodeType.Code128)]
    [InlineData(BarcodeType.Pdf417)]
    public async Task BarcodeFormatsUseTheSameReportPipeline(BarcodeType type)
    {
        var sample = Create(BarcodeGenerationOptions.Create(type));
        var result = await Read(sample);
        Assert.Equal(Status.Success, result.Status);
        var report = result.Inspection!;
        foreach (var category in new[] { InspectionCategory.Symbol, InspectionCategory.Modules, InspectionCategory.Codewords, InspectionCategory.Correction, InspectionCategory.Zones })
            Assert.Contains(report.Sections, section => section.Category == category);
        Assert.Equal(0, report.Modules!.Changed);
        Assert.Equal(report.Modules.Changed, report.Regions.Sum(region => region.Changed));
        Assert.DoesNotContain(report.Parameters, parameter => parameter.Name == "Версия QR");
        Assert.Equal(type == BarcodeType.Pdf417, report.Sections.Any(section => section.Category == InspectionCategory.Blocks));
    }

    [Fact]
    public async Task UndetectedQrUsesOneReferenceBasisForModulesAndCodewords()
    {
        var sample = Create(new QrGenerationOptions { Version = 1 });
        var frames = QRLab.Test.Fixtures.TestApplicationFactory.CreateMutations().Apply(sample.Image,
            [new SaltAndPepperOptions { DensityPercent = 100, SaltPercent = 0 }]);
        var result = await Read(sample with { Frames = frames });
        Assert.Equal(Status.NotFound, result.Status);
        var report = result.Inspection!;
        Assert.True(report.IsReferenceEstimate);
        Assert.Equal(report.Modules!.Changed, report.Regions.Sum(region => region.Changed));
        Assert.Contains(report.Parameters, parameter => parameter.Name == "Основа сравнения" && parameter.Value.Contains("оценка"));
        Assert.DoesNotContain(report.Parameters, parameter => parameter.Name == "Исправлено кодовых слов");
    }

    private static SamplePreview Create(GenerationOptions options) => Assert.Single(new CodeGenerator().CreateSamples(options with
        { Count = 1, Template = options is Ean13GenerationOptions ? "590123412345" : "HELLO", Width = 768, Height = 512 }));

    private static async Task<RecognitionPreview> Read(SamplePreview sample) => Assert.Single(await new ZxingCodeRecognizer().RecognizeAsync(
        [sample], new(10000), TestContext.Current.CancellationToken));
}
