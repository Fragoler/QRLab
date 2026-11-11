using System.Numerics;
using QRLab.Core.Models.Images;
using QRLab.Core.Models.Recognition;
using ZXing.QrCode.Internal;
using QrVersion = ZXing.QrCode.Internal.Version;

namespace QRLab.Image.Recognition.Inspection;

internal sealed class QrStructure
{
    public QrVersion Version { get; }
    public CodeLayout Layout { get; }
    
    public QrStructure(int dimension)
    {
        Version = QrVersion.getVersionForNumber((dimension - 17) / 4);
        var cells = Enumerable.Repeat(new ModuleCell(ModuleRole.Data), dimension * dimension).ToArray();
        void Region(int left, int top, int width, int height, ModuleRole role)
        {
            for (var y = top; y < top + height; y++)
                for (var x = left; x < left + width; x++) cells[y * dimension + x] = new(role);
        }
        Region(0, 0, 8, 8, ModuleRole.Separator);
        Region(dimension - 8, 0, 8, 8, ModuleRole.Separator);
        Region(0, dimension - 8, 8, 8, ModuleRole.Separator);
        Region(0, 0, 7, 7, ModuleRole.Finder);
        Region(dimension - 7, 0, 7, 7, ModuleRole.Finder);
        Region(0, dimension - 7, 7, 7, ModuleRole.Finder);
        Region(6, 8, 1, dimension - 16, ModuleRole.Timing);
        Region(8, 6, dimension - 16, 1, ModuleRole.Timing);
        var centers = Version.AlignmentPatternCenters;
        for (var y = 0; y < centers.Length; y++)
            for (var x = 0; x < centers.Length; x++)
                if (!((x == 0 && y == 0) || (x == centers.Length - 1 && y == 0) || (x == 0 && y == centers.Length - 1)))
                    Region(centers[x] - 2, centers[y] - 2, 5, 5, ModuleRole.Alignment);
        for (var i = 0; i < 9; i++)
            if (i != 6) { Region(i, 8, 1, 1, ModuleRole.Format); Region(8, i, 1, 1, ModuleRole.Format); }
        Region(dimension - 8, 8, 8, 1, ModuleRole.Format);
        Region(8, dimension - 7, 1, 7, ModuleRole.Format);
        Region(8, dimension - 8, 1, 1, ModuleRole.Fixed);
        if (Version.VersionNumber >= 7)
        {
            Region(dimension - 11, 0, 3, 6, ModuleRole.Version);
            Region(0, dimension - 11, 6, 3, ModuleRole.Version);
        }
        var bit = 0;
        var up = true;
        for (var right = dimension - 1; right > 0; right -= 2)
        {
            if (right == 6) right--;
            for (var step = 0; step < dimension; step++)
            {
                var y = up ? dimension - 1 - step : step;
                for (var dx = 0; dx < 2; dx++)
                {
                    var index = y * dimension + right - dx;
                    if (cells[index].Role != ModuleRole.Data) continue;
                    cells[index] = bit < Version.TotalCodewords * 8 ? new(ModuleRole.Data, bit / 8, bit % 8) : new(ModuleRole.Remainder);
                    bit++;
                }
            }
            up = !up;
        }
        Layout = new(dimension, dimension, cells);
    }

    public static (ErrorCorrectionLevel Level, int Mask)? ReadFormat(BinaryImage image)
    {
        var first = 0; var second = 0; var n = image.Width;
        void A(int x, int y) => first = (first << 1) | (image[x, y] ? 1 : 0);
        void B(int x, int y) => second = (second << 1) | (image[x, y] ? 1 : 0);
        for (var i = 0; i < 6; i++) A(i, 8);
        A(7, 8); A(8, 8); A(8, 7);
        for (var y = 5; y >= 0; y--) A(8, y);
        for (var y = n - 1; y >= n - 7; y--) B(8, y);
        for (var x = n - 8; x < n; x++) B(x, 8);
        var best = 16; var bestValue = -1;
        for (var value = 0; value < 32; value++)
        {
            var remainder = value << 10;
            for (var bit = 14; bit >= 10; bit--)
                if ((remainder & (1 << bit)) != 0) remainder ^= 0x537 << (bit - 10);
            var encoded = ((value << 10) | remainder) ^ 0x5412;
            var distance = Math.Min(BitOperations.PopCount((uint)(first ^ encoded)), BitOperations.PopCount((uint)(second ^ encoded)));
            if (distance < best) { best = distance; bestValue = value; }
        }
        return best <= 3 ? (ErrorCorrectionLevel.forBits(bestValue >> 3), bestValue & 7) : null;
    }

    public (int Block, int Offset)[] Interleaving(ErrorCorrectionLevel level, out int[] dataCounts, out int parity)
    {
        var ec = Version.getECBlocksForLevel(level);
        dataCounts = ec.getECBlocks().SelectMany(group => Enumerable.Repeat(group.DataCodewords, group.Count)).ToArray();
        parity = ec.ECCodewordsPerBlock;
        var map = new List<(int, int)>();
        for (var offset = 0; offset < dataCounts.Max(); offset++)
            for (var block = 0; block < dataCounts.Length; block++)
                if (offset < dataCounts[block]) map.Add((block, offset));
        for (var offset = 0; offset < parity; offset++)
            for (var block = 0; block < dataCounts.Length; block++) map.Add((block, dataCounts[block] + offset));
        return map.ToArray();
    }

    public static bool Masked(int mask, int x, int y) => mask switch
    {
        0 => (x + y) % 2 == 0, 1 => y % 2 == 0, 2 => x % 3 == 0, 3 => (x + y) % 3 == 0,
        4 => (y / 2 + x / 3) % 2 == 0, 5 => x * y % 2 + x * y % 3 == 0,
        6 => (x * y % 2 + x * y % 3) % 2 == 0, 7 => ((x + y) % 2 + x * y % 3) % 2 == 0,
        _ => throw new ArgumentOutOfRangeException(nameof(mask))
    };
}
