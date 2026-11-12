using QRLab.Core.Models.Generation.Barcode;
using QRLab.Core.Models.Generation.Qrcode;

namespace QRLab.Core.Models.Generation;

public static class GenerationDefaults
{
    public const int RandomLength = 16;
    public const string Template = "LAB-2026-{num}";
    public const string NumberPlaceholder = "{num}";
    public const string NumberFormat = "0000";
    public const int FirstNumber = 1;
    public const int Count = 120;
    public const int SingleCount = 1;
    public const int Width = 360;
    public const int Height = 360;
    public const int QuietZone = 2;
    public const int AutomaticQrVersion = 0;
    public const int AutomaticQrMask = -1;
    public const QrCorrectionLevel Correction = QrCorrectionLevel.High;
    public const BarcodeType Barcode = BarcodeType.Code128;
    public const GenerationMode Mode = GenerationMode.Batch;
    public const CodeKind Kind = CodeKind.QrCode;
    public const ContentSource Source = ContentSource.NumberedTemplate;
    public const string NewSetName = "Набор";
}
