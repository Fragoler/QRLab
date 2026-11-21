using System.Reflection;
using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Recognition;
using QRLab.Image.Generation;
using QRLab.Image.Recognition;
using ZXing.PDF417;
using Xunit;

namespace QRLab.Test;

public sealed class Pdf417StructureTests
{
    [Theory]
    [InlineData(1, 30, 4, 2)] // The writer rotates this tall symbol.
    [InlineData(4, 12, 3, 2)] // Even columns used to merge word colors vertically.
    [InlineData(5, 8, 1, 1)]
    [InlineData(2, 10, 4, 0)]
    [InlineData(10, 60, 2, 8)]
    public async Task PhysicalCodewordPositionsFormOneValidReedSolomonSequence(int columns, int rows, int height, int level)
    {
        var sample = Assert.Single(new CodeGenerator().CreateSamples(new Pdf417GenerationOptions
        {
            Template = "HELLO", Count = 1, Width = 1024, Height = 768,
            MinimumColumns = columns, MaximumColumns = columns, MinimumRows = rows, MaximumRows = rows,
            RowHeight = height, CorrectionLevel = level
        }));
        var matrix = sample.OriginalBitmap!;
        var layout = sample.Layout!;
        var groups = layout.Cells.Select((cell, index) => (Cell: cell, X: index % layout.Width, Y: index / layout.Width))
            .Where(item => item.Cell.Codeword >= 0).GroupBy(item => item.Cell.Codeword).OrderBy(group => group.Key).ToArray();
        Assert.Equal(rows * columns, groups.Length);
        var parity = 1 << (level + 1);
        var values = new List<int>();
        // Use the encoder's cluster tables independently of the inspector's decoder table.
        var tables = (int[][])typeof(PDF417Reader).Assembly.GetType("ZXing.PDF417.Internal.PDF417")!
            .GetField("CODEWORD_TABLE", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        foreach (var group in groups)
        {
            Assert.Equal(17 * height, group.Count());
            Assert.Equal(group.Key < rows * columns - parity ? ModuleRole.Data : ModuleRole.ErrorCorrection,
                Assert.Single(group.Select(item => item.Cell.Role).Distinct()));
            Assert.All(group, item => Assert.Equal(0, item.Cell.Block));
            var bits = group.GroupBy(item => item.Cell.Bit).OrderBy(bit => bit.Key).ToArray();
            Assert.Equal(Enumerable.Range(0, 17), bits.Select(bit => bit.Key));
            var pattern = 0;
            foreach (var bit in bits)
            {
                var black = Assert.Single(bit.Select(item => matrix[item.X, item.Y]).Distinct());
                pattern = (pattern << 1) | (black ? 1 : 0);
            }
            var value = Array.IndexOf(tables[group.Key / columns % 3], pattern);
            Assert.InRange(value, 0, 928);
            values.Add(value);
        }
        Assert.Equal(rows * columns - parity, values[0]); // Symbol Length Descriptor.
        // Every RS syndrome over GF(929) must be zero. Wrong word order or parity placement fails this.
        var root = 1;
        for (var i = 1; i <= parity; i++)
        {
            root = root * 3 % 929;
            var syndrome = 0;
            foreach (var value in values) syndrome = (syndrome * root + value) % 929;
            Assert.Equal(0, syndrome);
        }
        Assert.All(layout.Cells.Where(cell => cell.Codeword < 0), cell => Assert.Equal(-1, cell.Block));
        var result = Assert.Single(await new ZxingCodeRecognizer().RecognizeAsync([sample], new(10000), TestContext.Current.CancellationToken));
        Assert.Equal(Status.Success, result.Status);
        Assert.Equal(0, result.Inspection!.Modules!.Changed);
        Assert.Equal(0, result.Inspection.DamagedCodewords);
        Assert.Empty(result.Inspection.Blocks);
        Assert.Equal(parity / 2, result.Inspection.CorrectionCapacity);
        Assert.Contains(result.Inspection.Parameters, p => p.Name == "Проверочных слов" && p.Value == parity.ToString());
    }
}
