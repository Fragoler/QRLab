using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Images;
using QRLab.Core.Models.Mutations;
using QRLab.Core.Models.Recognition;

namespace QRLab.Core.Models.Samples;

public sealed record SamplePreview(
    int Number, 
    string Name,
    string Content, 
    RasterImage Image,
    IReadOnlyList<MutationFrame> Frames, 
    GenerationOptions Settings
    )
{
    public BinaryImage? OriginalBitmap { get; init; }
    public BinaryImage? FinalBitmap { get; init; }
    public CodeLayout? Layout { get; init; }
}
