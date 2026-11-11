using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Images;
using QRLab.Core.Models.Recognition;
using QRLab.Core.Models.Samples;
using ZXing;
using ZXing.Common;
using ZXing.PDF417;

namespace QRLab.Image.Recognition.Inspection;

internal sealed class Pdf417Inspector : CodeInspector
{
    public override bool Supports(GenerationOptions settings) => settings is Pdf417GenerationOptions;

    public override CodeLayout CreateLayout(BinaryImage original, GenerationOptions settings)
    {
        var bounds = Bounds(original);
        var rows = RowRanges(original);
        var columns = (bounds.Right - bounds.Left + 1 - 69) / 17;
        var cells = Enumerable.Repeat(new ModuleCell(ModuleRole.QuietZone), original.Width * original.Height).ToArray();
        for (var row = 0; row < rows.Count; row++)
            for (var y = rows[row].Top; y <= rows[row].Bottom; y++)
                for (var x = bounds.Left; x <= bounds.Right; x++)
                {
                    var offset = x - bounds.Left;
                    cells[y * original.Width + x] = offset < 17 || offset >= 51 + columns * 17 ? new(ModuleRole.Guard)
                        : offset < 34 || offset >= 34 + columns * 17 ? new(ModuleRole.RowIndicator)
                        : new(ModuleRole.Data, row * columns + (offset - 34) / 17, (offset - 34) % 17);
                }
        return new(original.Width, original.Height, cells);
    }

    public override CodeInspection Inspect(SamplePreview sample, BitMatrix raster, Result? decoded, BinaryImage? detected)
    {
        var original = sample.OriginalBitmap!;
        var layout = CreateLayout(original, sample.Settings);
        var probe = Probe(sample, raster);
        var observed = Capture(sample, raster);
        var compared = observed ?? probe;
        var bounds = Bounds(original);
        var rows = RowRanges(original);
        var columns = (bounds.Right - bounds.Left + 1 - 69) / 17;
        var levelWord = ReadPattern(original, bounds.Left + 17, (rows[1].Top + rows[1].Bottom) / 2);
        var levelValue = Pdf417Symbols.Decode(levelWord);
        var level = levelValue >= 0 ? levelValue % 30 / 3 : 2;
        var parity = 1 << (level + 1);
        var damaged = DamagedWords(original, compared, layout);
        var words = new int[rows.Count * columns];
        var erasures = new List<int>();
        for (var row = 0; row < rows.Count; row++)
            for (var column = 0; column < columns; column++)
            {
                var index = row * columns + column;
                words[index] = Pdf417Symbols.Decode(ReadPattern(compared, bounds.Left + 34 + column * 17, (rows[row].Top + rows[row].Bottom) / 2));
                if (words[index] < 0) { erasures.Add(index); words[index] = 0; }
            }
        var knownErasures = erasures.Count(damaged.Contains);
        var block = new CorrectionBlock(1, damaged.Count, words.Length, parity) { Erasures = knownErasures };
        var conclusion = $"PDF417, EC {level}: повреждено {damaged.Count} из {words.Length} слов, из них {knownErasures} не распознаны как допустимые 17-модульные символы. "
            + $"Допуск: {parity / 2} ошибок неизвестного положения либо {parity} стираний; условие 2e + s ≤ {parity}.";
        if (block.ExceedsCapacity) conclusion += " Расчётная граница коррекции превышена.";
        if (observed is null) conclusion += " " + ReferenceWarning;
        else if (decoded is null) conclusion += " Границы найдены; отказ возможен при чтении индикаторов строк, кодовых слов или коррекции. Эти этапы стандартный API не различает.";
        return new("PDF417", layout, observed, probe, null,
            observed is null ? ReferenceWarning : "Границы найдены PDF417-детектором; для учебной выборки используются число строк и ширина известного образца.",
            "",
            conclusion, Damage(original, compared, layout), [block], damaged.Count, words.Length, parity / 2)
        {
            Basis = observed is null ? InspectionBasis.ReferenceGrid : InspectionBasis.DetectorBounds,
            Parameters = [new(InspectionCategory.Symbol, "Строки / столбцы", $"{rows.Count} / {columns}"),
                new(InspectionCategory.Correction, "Уровень коррекции", level.ToString())]
        };
    }

    private static BinaryImage? Capture(SamplePreview sample, BitMatrix raster)
    {
        var bitmap = new BinaryBitmap(new HybridBinarizer(BitmapReader.ReadLuminance(sample.Frames.LastOrDefault()?.Image ?? sample.Image)));
        var detection = ZXing.PDF417.Internal.Detector.detect(bitmap, null, false);
        var points = detection?.Points.FirstOrDefault();
        if (points is not { Length: >= 4 } || points.Take(4).Any(point => point is null)) return null;
        var original = sample.OriginalBitmap!;
        var bounds = Bounds(original);
        var matrix = detection!.Bits;
        return Build(original.Width, original.Height, (x, y) =>
        {
            var u = (x - bounds.Left + .5) / (bounds.Right - bounds.Left + 1);
            var v = (y - bounds.Top + .5) / (bounds.Bottom - bounds.Top + 1);
            var px = (1 - u) * ((1 - v) * points[0].X + v * points[1].X) + u * ((1 - v) * points[2].X + v * points[3].X);
            var py = (1 - u) * ((1 - v) * points[0].Y + v * (points[1].Y + 1)) + u * ((1 - v) * points[2].Y + v * (points[3].Y + 1));
            return matrix[Math.Clamp((int)px, 0, matrix.Width - 1), Math.Clamp((int)py, 0, matrix.Height - 1)];
        });
    }

    private static List<(int Top, int Bottom)> RowRanges(BinaryImage original)
    {
        var bounds = Bounds(original);
        var result = new List<(int, int)>();
        var start = bounds.Top;
        for (var y = bounds.Top + 1; y <= bounds.Bottom; y++)
            if (Enumerable.Range(bounds.Left, bounds.Right - bounds.Left + 1).Any(x => original[x, y] != original[x, y - 1]))
            { result.Add((start, y - 1)); start = y; }
        result.Add((start, bounds.Bottom));
        return result;
    }

    private static int ReadPattern(BinaryImage image, int left, int y)
    {
        var value = 0;
        for (var x = 0; x < 17; x++) value = (value << 1) | (image[left + x, y] ? 1 : 0);
        return value;
    }
}
