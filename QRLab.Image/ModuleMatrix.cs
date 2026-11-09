using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Images;
using ZXing.Common;

namespace QRLab.Image;

internal static class ModuleMatrix
{
    public static BinaryImage Capture(BitMatrix matrix, int border = 0)
    {
        var width = matrix.Width - 2 * border;
        var height = matrix.Height - 2 * border;
        var bits = new byte[(width * height + 7) / 8];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                if (matrix[x + border, y + border]) bits[(y * width + x) / 8] |= (byte)(1 << ((y * width + x) % 8));
        return new(width, height, bits);
    }

    // MultiFormatWriter adds a quiet zone around QR modules; the detector returns modules only.
    public static BinaryImage FromEncoder(BitMatrix matrix, GenerationOptions settings) =>
        Capture(matrix, settings.Kind == CodeKind.QrCode ? settings.QuietZone : 0);
}
