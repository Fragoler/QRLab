using QRLab.Core.Models.Generation;

namespace QRLab.Core.Models.Images;

/// <summary>Immutable, packed code module matrix. A set bit is black; coordinates start at the top left.</summary>
public sealed class BinaryImage
{
    private readonly byte[] _bits;
    public int Width { get; }
    public int Height { get; }
    public ReadOnlyMemory<byte> Bits => _bits;
    public bool this[int x, int y]
    {
        get
        {
            if ((uint)x >= Width || (uint)y >= Height) throw new ArgumentOutOfRangeException();
            var index = y * Width + x;
            return (_bits[index / 8] & (1 << (index % 8))) != 0;
        }
    }

    public BinaryImage(int width, int height, ReadOnlySpan<byte> bits)
    {
        if (width is < 1 or > GenerationLimits.MaximumImageSize || height is < 1 or > GenerationLimits.MaximumImageSize
            || bits.Length != (width * height + 7) / 8) throw new ArgumentException("Некорректный размер битмапы.");
        Width = width;
        Height = height;
        _bits = bits.ToArray();
    }
}
