using QRLab.Core.Models.Images;
using ZXing;
using ZXing.QrCode.Internal;

namespace QRLab.Image.Recognition;

/// <summary>
/// Captures the detector's sampled module grid before the decoder unmasks it in place.
/// </summary>
internal sealed class QrModuleReader(Action<BinaryImage> capture) : Reader
{
    public Result? decode(BinaryBitmap image) => decode(image, null);

    public Result? decode(BinaryBitmap image, IDictionary<DecodeHintType, object>? hints)
    {
        var raster = image.BlackMatrix;
        var attempts = hints?.TryGetValue(DecodeHintType.ALSO_INVERTED, out var inverted) == true && inverted is true ? 2 : 1;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (attempt == 1)
            {
                raster = (ZXing.Common.BitMatrix)raster.Clone();
                raster.flip();
            }
            var detected = new Detector(raster).detect(hints);
            if (detected is null) continue;
            capture(ModuleMatrix.Capture(detected.Bits));
            var decoded = new Decoder().decode(detected.Bits, hints);
            if (decoded is null) continue;
            if (decoded.Other is QRCodeDecoderMetaData { IsMirrored: true } mirrored)
                mirrored.applyMirroredCorrection(detected.Points);
            var result = new Result(decoded.Text, decoded.RawBytes, detected.Points, BarcodeFormat.QR_CODE);
            result.putMetadata(ResultMetadataType.ERRORS_CORRECTED, decoded.ErrorsCorrected);
            result.putMetadata(ResultMetadataType.ERROR_CORRECTION_LEVEL, decoded.ECLevel);
            return result;
        }
        return null;
    }

    public void reset() { }
}
