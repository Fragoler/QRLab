namespace QRLab.Core.Models.Generation.Barcode;

public abstract record BarcodeGenerationOptions : GenerationOptions
{
    public override CodeKind Kind => CodeKind.Barcode;
    public override string SamplePrefix => "BAR";
    public abstract BarcodeType BarcodeType { get; }

    public static BarcodeGenerationOptions Create(BarcodeType type) => type switch
    {
        BarcodeType.Pdf417 => new Pdf417GenerationOptions(),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
