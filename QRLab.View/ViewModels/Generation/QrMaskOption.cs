using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Images;
using ZXing.QrCode.Internal;

namespace QRLab.View.ViewModels.Generation;

public sealed record QrMaskOption
{
    public int Value { get; }
    public string Name { get; }
    public BinaryImage? Preview { get; }
    public bool HasPreview => Preview is not null;

    public QrMaskOption(int value)
    {
        if (value is < GenerationDefaults.AutomaticQrMask or > 7)
            throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
        Name = value == GenerationDefaults.AutomaticQrMask ? "Авто" : Convert.ToString(value, 2).PadLeft(3, '0');
        if (value == GenerationDefaults.AutomaticQrMask) return;

        // 24 modules show complete repeats of every QR mask without finder patterns or payload.
        const int size = 24;
        var bits = new byte[(size * size + 7) / 8];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            if (!MaskUtil.getDataMaskBit(value, x, y)) continue;
            var index = y * size + x;
            bits[index / 8] |= (byte)(1 << (index % 8));
        }
        Preview = new BinaryImage(size, size, bits);
    }
}
