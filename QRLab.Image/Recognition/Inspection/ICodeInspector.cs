using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Images;
using QRLab.Core.Models.Recognition;
using QRLab.Core.Models.Samples;
using ZXing;
using ZXing.Common;

namespace QRLab.Image.Recognition.Inspection;

internal interface ICodeInspector
{
    bool Supports(GenerationOptions settings);
    CodeLayout CreateLayout(BinaryImage original, GenerationOptions settings);
    CodeInspection Inspect(SamplePreview sample, BitMatrix raster, Result? decoded, BinaryImage? detected);
}

internal interface IInspectionReportBuilder
{
    CodeInspection Build(CodeInspection inspection, BinaryImage original, Result? decoded);
}
