namespace QRLab.Core.Models.Mutations;

public sealed record JpegCompressionOptions : MutationOptions
{
    public override MutationKind Kind => MutationKind.JpegCompression;
    public int Quality { get; init; } = MutationDefaults.JpegQuality;

    public override void Validate()
    {
        if (Quality is < 0 or > 100)
            throw new ArgumentException("Качество JPEG должно быть от 0 до 100.");
    }
}
