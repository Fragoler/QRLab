using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Images;
using QRLab.Core.Models.Mutations;

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
}
