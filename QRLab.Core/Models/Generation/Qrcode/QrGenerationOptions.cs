namespace QRLab.Core.Models.Generation.Qrcode;

public sealed record QrGenerationOptions : GenerationOptions
{
    public int Version { get; init; } = GenerationDefaults.AutomaticQrVersion;
    public QrCorrectionLevel Correction { get; init; } = GenerationDefaults.Correction;
    public int MaskPattern { get; init; } = GenerationDefaults.AutomaticQrMask;
    public override CodeKind Kind => CodeKind.QrCode;
    public override string SamplePrefix => "QR";

    public override void Validate()
    {
        base.Validate();
        if (Version < GenerationDefaults.AutomaticQrVersion || Version > GenerationLimits.MaximumQrVersion)
            throw new ArgumentException($"Класс размера QR должен быть от 1 до {GenerationLimits.MaximumQrVersion} (0 — авто).");
        if (!Enum.IsDefined(Correction)) throw new ArgumentException("Неизвестный уровень коррекции QR.");
        if (MaskPattern is < GenerationDefaults.AutomaticQrMask or > 7)
            throw new ArgumentException("Маска QR должна быть от 000 до 111 (−1 — авто).");
    }
}
