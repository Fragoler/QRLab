using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Images;
using QRLab.Core.Models.Recognition;
using QRLab.Core.Models.Samples;
using ZXing;
using ZXing.Common;

namespace QRLab.Image.Recognition.Inspection;

/// <summary>
/// Combines each format's analysis with the same comparison and report pipeline.
/// </summary>
internal sealed class CategorizedCodeInspector(ICodeInspector inspector, IInspectionReportBuilder reports) : CodeInspector
{
    public override bool Supports(GenerationOptions settings) => inspector.Supports(settings);
    public override CodeLayout CreateLayout(BinaryImage original, GenerationOptions settings) => inspector.CreateLayout(original, settings);
    public override CodeInspection Inspect(SamplePreview sample, BitMatrix raster, Result? decoded, BinaryImage? detected) =>
        reports.Build(inspector.Inspect(sample, raster, decoded, detected), sample.OriginalBitmap!, decoded);
}
