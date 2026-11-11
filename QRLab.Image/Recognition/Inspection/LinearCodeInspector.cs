using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Images;
using QRLab.Core.Models.Recognition;
using QRLab.Core.Models.Samples;
using ZXing;
using ZXing.Common;
using ZXing.OneD;

namespace QRLab.Image.Recognition.Inspection;

internal abstract class LinearCodeInspector : CodeInspector
{
    protected abstract string Name { get; }
    protected abstract string ChecksumDescription { get; }
    protected abstract int ChecksumModulus { get; }
    protected abstract OneDReader CreateReader();
    protected abstract double CenterOffset { get; }
    protected abstract double CenterDistance(int encodedWidth);

    public override CodeInspection Inspect(SamplePreview sample, BitMatrix raster, Result? decoded, BinaryImage? detected)
    {
        var original = sample.OriginalBitmap!;
        var layout = CreateLayout(original, sample.Settings);
        var probe = Probe(sample, raster);
        var bounds = Bounds(original);
        var observed = CaptureRow(raster, original, bounds.Left, bounds.Right);
        var basis = observed is null ? InspectionBasis.ReferenceGrid : InspectionBasis.Scanline;
        var rowProbe = Build(probe.Width, probe.Height, (x, _) => probe[x, probe.Height / 2]);
        var guardIntact = !layout.Cells.Select((cell, index) => (cell, index)).Any(item => item.cell.Role == ModuleRole.Guard
            && original[item.index % layout.Width, item.index / layout.Width] != rowProbe[item.index % layout.Width, item.index / layout.Width]);
        var sampling = observed is not null ? "Нормированная выборка успешной строки по двум опорным точкам; размеры сетки взяты из исходника. Строка повторена по высоте для сравнения."
            : guardIntact ? "Строка по известной геометрии образца; защитные штрихи совпали, сегментация символов декодером не подтверждена." : ReferenceWarning;
        if (observed is null && guardIntact) observed = rowProbe;
        var compared = observed ?? rowProbe;
        var damaged = DamagedWords(original, compared, layout);
        var total = layout.Cells.Where(cell => cell.Codeword >= 0).Select(cell => cell.Codeword).Distinct().Count();
        var conclusion = $"{Name}: различаются {damaged.Count} из {total} символов выбранной строки. Символьная коррекция ошибок отсутствует; предел восстановления — 0. "
            + ChecksumDescription + " Повреждение пикселей вне выбранной строки или вдали от границ штрихов может не мешать чтению.";
        if (decoded is null) conclusion += guardIntact
            ? " Защитные штрихи сохранены; вероятен отказ распознавания символов или проверки контрольной суммы. Без внутренних данных декодера эти причины неразличимы."
            : " Не подтверждены стартовые / защитные штрихи. " + ReferenceWarning;
        return new(Name, layout, observed, rowProbe, null, sampling, "",
            conclusion, Damage(original, compared, layout), [], damaged.Count, total, 0)
        {
            Basis = basis,
            Parameters = [new(InspectionCategory.Correction, "Модуль контрольной суммы", ChecksumModulus.ToString())]
        };
    }

    private BinaryImage? CaptureRow(BitMatrix raster, BinaryImage original, int first, int last)
    {
        var reader = CreateReader();
        var working = raster;
        for (var orientation = 0; orientation < 4; orientation++)
        {
            if (orientation > 0) { working = (BitMatrix)working.Clone(); working.rotate90(); }
            for (var inversion = 0; inversion < 2; inversion++)
            {
                var candidate = working;
                if (inversion == 1) { candidate = (BitMatrix)working.Clone(); candidate.flip(); }
                foreach (var y in Enumerable.Range(0, 33).Select(index => Math.Clamp(candidate.Height / 2 + (index % 2 == 0 ? 1 : -1) * ((index + 1) / 2) * Math.Max(1, candidate.Height / 34), 0, candidate.Height - 1)).Distinct())
                {
                    var row = candidate.getRow(y, null);
                    for (var reverse = 0; reverse < 2; reverse++)
                    {
                        if (reverse == 1) row.reverse();
                        Result? result;
                        // ZXing.Net 0.16.11 EAN13Reader dereferences a missing middle guard on some trial rows.
                        // Treat that particular native failure as an unsuccessful row, and keep scanning.
                        try { result = reader.decodeRow(y, row, null); }
                        catch (NullReferenceException) { continue; }
                        if (result?.ResultPoints is not { Length: >= 2 } points) continue;
                        var step = (points[1].X - points[0].X) / CenterDistance(last - first + 1);
                        if (step <= 0) continue;
                        var left = points[0].X - CenterOffset * step;
                        return Build(original.Width, original.Height, (x, _) =>
                        {
                            var position = (int)(left + (x - first + .5) * step);
                            return position >= 0 && position < row.Size && row[position];
                        });
                    }
                }
            }
        }
        return null;
    }
}

internal sealed class Ean13Inspector : LinearCodeInspector
{
    protected override int ChecksumModulus => 10;
    protected override string Name => "EAN-13";
    protected override OneDReader CreateReader() => new EAN13Reader();
    protected override double CenterOffset => 1.5;
    protected override double CenterDistance(int encodedWidth) => 92;
    protected override string ChecksumDescription => "Контрольная цифра по модулю 10 обнаруживает многие, но не все ошибки и не восстанавливает цифры.";
    public override bool Supports(GenerationOptions settings) => settings is Ean13GenerationOptions;
    public override CodeLayout CreateLayout(BinaryImage original, GenerationOptions settings)
    {
        var bounds = Bounds(original);
        var cells = new List<ModuleCell>();
        for (var y = 0; y < original.Height; y++)
            for (var x = 0; x < original.Width; x++)
            {
                var offset = x - bounds.Left;
                cells.Add(offset < 0 || offset >= 95 ? new(ModuleRole.QuietZone)
                    : offset < 3 || offset is >= 45 and < 50 || offset >= 92 ? new(ModuleRole.Guard)
                    : offset < 45 ? new(ModuleRole.Data, (offset - 3) / 7, (offset - 3) % 7)
                    : new(offset >= 85 ? ModuleRole.Checksum : ModuleRole.Data, 6 + (offset - 50) / 7, (offset - 50) % 7));
            }
        return new(original.Width, original.Height, cells);
    }
}

internal sealed class Code128Inspector : LinearCodeInspector
{
    protected override int ChecksumModulus => 103;
    protected override string Name => "Code 128";
    protected override OneDReader CreateReader() => new Code128Reader();
    protected override double CenterOffset => 5.5;
    protected override double CenterDistance(int encodedWidth) => encodedWidth - 13;
    protected override string ChecksumDescription => "Взвешенная контрольная сумма по модулю 103 проверяет символы и их позиции; исправляющей избыточности нет.";
    public override bool Supports(GenerationOptions settings) => settings is Code128GenerationOptions;
    public override CodeLayout CreateLayout(BinaryImage original, GenerationOptions settings)
    {
        var bounds = Bounds(original);
        var width = bounds.Right - bounds.Left + 1;
        var cells = new List<ModuleCell>();
        for (var y = 0; y < original.Height; y++)
            for (var x = 0; x < original.Width; x++)
            {
                var offset = x - bounds.Left;
                cells.Add(offset < 0 || offset >= width ? new(ModuleRole.QuietZone)
                    : offset < 11 || offset >= width - 13 ? new(ModuleRole.Guard)
                    : new(offset >= width - 24 ? ModuleRole.Checksum : ModuleRole.Data, offset / 11 - 1, offset % 11));
            }
        return new(original.Width, original.Height, cells);
    }
}
