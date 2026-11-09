using QRLab.Core.Models.Generation;

namespace QRLab.Image.Storage.Serialization;

internal sealed record StoredSample(int Number, string Name, string Content, GenerationOptions Settings,
    string Image, StoredMutation[] Frames)
{
    public StoredBitmap? OriginalBitmap { get; init; }
    public StoredBitmap? FinalBitmap { get; init; }
}

internal sealed record StoredBitmap(int Width, int Height, string File);
