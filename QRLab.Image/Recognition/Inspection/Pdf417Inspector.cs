using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Images;
using QRLab.Core.Models.Recognition;
using QRLab.Core.Models.Samples;
using ZXing;
using ZXing.Common;

namespace QRLab.Image.Recognition.Inspection;

internal sealed class Pdf417Inspector : CodeInspector
{
    public override bool Supports(GenerationOptions settings) => settings is Pdf417GenerationOptions;

    public override CodeLayout CreateLayout(BinaryImage original, GenerationOptions settings)
    {
        return Pdf417Structure.Read(original).CreateLayout();
    }

    public override CodeInspection Inspect(SamplePreview sample, BitMatrix raster, Result? decoded, BinaryImage? detected)
    {
        var original = sample.OriginalBitmap!;
        var structure = Pdf417Structure.Read(original);
        var layout = structure.CreateLayout();
        var probe = Probe(sample, raster);
        var observed = Capture(sample, structure);
        var compared = observed ?? probe;
        var level = structure.CorrectionLevel;
        var parity = structure.CorrectionWords;
        var totalWords = structure.Words.Count;
        var conclusion = $"PDF417, EC {level}: {totalWords} кодовых слов. "
            + $"Один общий блок Reed–Solomon, {parity} проверочных слов. Предел коррекции ошибок: {parity / 2} кодовых слов.";
        if (decoded is not null) conclusion += " Код прочитан декодером.";
        if (observed is null) conclusion += " " + ReferenceWarning;
        else if (decoded is null) conclusion += " Границы найдены, но отказано в чтении строк";
        return new("PDF417", layout, observed, probe, null,
            conclusion, Damage(original, compared, layout), [], 0, totalWords, parity / 2)
        {
            Basis = observed is null ? InspectionBasis.ReferenceGrid : InspectionBasis.DetectorBounds,
            Parameters = [new(InspectionCategory.Symbol, "Строки / столбцы", $"{structure.Rows.Count} / {structure.Columns}"),
                new(InspectionCategory.Codewords, "Кодовые слова - данные", structure.DataWords.ToString()),
                new(InspectionCategory.Codewords, "Кодовые слова - коррекция", structure.CorrectionWords.ToString()),
                new(InspectionCategory.Correction, "Уровень коррекции", level.ToString()),
                new(InspectionCategory.Correction, "Проверочных слов", parity.ToString()),
                new(InspectionCategory.Correction, "Блоков", "1")]
        };
    }

    private static BinaryImage? Capture(SamplePreview sample, Pdf417Structure structure)
    {
        var bitmap = new BinaryBitmap(new HybridBinarizer(BitmapReader.ReadLuminance(sample.Frames.LastOrDefault()?.Image ?? sample.Image)));
        var detection = ZXing.PDF417.Internal.Detector.detect(bitmap, null, false);
        var points = detection?.Points.FirstOrDefault();
        if (points is not { Length: >= 4 } || points.Take(4).Any(point => point is null)) return null;
        var original = sample.OriginalBitmap!;
        var matrix = detection!.Bits;
        return Build(original.Width, original.Height, (x, y) =>
        {
            var point = structure.ToCanonical(x, y);
            var u = (point.X - structure.Left + .5) / structure.SymbolWidth;
            var v = (point.Y - structure.Top + .5) / structure.SymbolHeight;
            var px = (1 - u) * ((1 - v) * points[0].X + v * points[1].X) + u * ((1 - v) * points[2].X + v * points[3].X);
            var py = (1 - u) * ((1 - v) * points[0].Y + v * (points[1].Y + 1)) + u * ((1 - v) * points[2].Y + v * (points[3].Y + 1));
            return matrix[Math.Clamp((int)px, 0, matrix.Width - 1), Math.Clamp((int)py, 0, matrix.Height - 1)];
        });
    }
}
