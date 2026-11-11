using QRLab.Core.Models.Images;

namespace QRLab.Core.Models.Recognition;

public sealed record RecognitionPreview(int Number, Status Status, double Milliseconds, string? DecodedContent = null, string? Error = null)
{
    public BinaryImage? OriginalBitmap { get; init; }
    public BinaryImage? FinalBitmap { get; init; }
    public int? CorrectedCodewords { get; init; }
    public string? ErrorCorrectionLevel { get; init; }
    public CodeInspection? Inspection { get; init; }
}
