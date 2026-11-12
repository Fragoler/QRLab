using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Generation.Qrcode;
using ZXing;
using ZXing.QrCode.Internal;

namespace QRLab.Image.Generation.Encoders;

public sealed class QrCodeEncoder : CodeEncoder
{
    public override bool Supports(GenerationOptions options) => options is QrGenerationOptions;
    protected override BarcodeFormat GetFormat(GenerationOptions options) => BarcodeFormat.QR_CODE;

    protected override IDictionary<EncodeHintType, object> CreateHints(GenerationOptions options)
    {
        var qr = (QrGenerationOptions)options;
        var hints = base.CreateHints(options);
        hints[EncodeHintType.CHARACTER_SET] = "UTF-8";
        hints[EncodeHintType.ERROR_CORRECTION] = qr.Correction switch
        {
            QrCorrectionLevel.Low => ErrorCorrectionLevel.L,
            QrCorrectionLevel.Medium => ErrorCorrectionLevel.M,
            QrCorrectionLevel.Quartile => ErrorCorrectionLevel.Q,
            QrCorrectionLevel.High => ErrorCorrectionLevel.H,
            _ => throw new ArgumentOutOfRangeException(nameof(options))
        };
        if (qr.Version != GenerationDefaults.AutomaticQrVersion) hints[EncodeHintType.QR_VERSION] = qr.Version;
        if (qr.MaskPattern != GenerationDefaults.AutomaticQrMask) hints[EncodeHintType.QR_MASK_PATTERN] = qr.MaskPattern;
        return hints;
    }
}
