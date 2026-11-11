using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Images;
using QRLab.Core.Models.Recognition;
using QRLab.Core.Models.Samples;
using ZXing;
using ZXing.Common;

namespace QRLab.Image.Recognition.Inspection;

internal abstract class CodeInspector
{
    public abstract bool Supports(GenerationOptions settings);
    public abstract CodeLayout CreateLayout(BinaryImage original, GenerationOptions settings);
    public abstract CodeInspection Inspect(SamplePreview sample, BitMatrix raster, Result? decoded, BinaryImage? detected);

    protected static BinaryImage Build(int width, int height, Func<int, int, bool> pixel)
    {
        var bytes = new byte[(width * height + 7) / 8];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                if (pixel(x, y)) bytes[(y * width + x) / 8] |= (byte)(1 << ((y * width + x) % 8));
        return new(width, height, bytes);
    }

    protected static BinaryImage Probe(SamplePreview sample, BitMatrix raster)
    {
        var original = sample.OriginalBitmap!;
        var border = sample.Settings.Kind == CodeKind.QrCode ? sample.Settings.QuietZone : 0;
        var matrixWidth = original.Width + border * 2;
        var matrixHeight = original.Height + border * 2;
        var scale = Math.Min(sample.Image.Width / matrixWidth, sample.Image.Height / matrixHeight);
        var scaleY = sample.Settings.IsLinear ? sample.Image.Height / matrixHeight : scale;
        var left = (sample.Image.Width - matrixWidth * scale) / 2;
        var top = (sample.Image.Height - matrixHeight * scaleY) / 2;
        return Build(original.Width, original.Height, (x, y) => raster[
            Math.Clamp((int)(left + (x + border + .5) * scale), 0, raster.Width - 1),
            Math.Clamp((int)(top + (y + border + .5) * scaleY), 0, raster.Height - 1)]);
    }

    protected static (int Left, int Top, int Right, int Bottom) Bounds(BinaryImage matrix)
    {
        var left = matrix.Width; var top = matrix.Height; var right = -1; var bottom = -1;
        for (var y = 0; y < matrix.Height; y++)
            for (var x = 0; x < matrix.Width; x++)
                if (matrix[x, y]) { left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y); }
        return (left, top, right, bottom);
    }

    protected static IReadOnlyList<RegionDamage> Damage(BinaryImage original, BinaryImage observed, CodeLayout layout) =>
        layout.Cells.Select((cell, index) => (cell.Role, Changed: original[index % layout.Width, index / layout.Width] != observed[index % layout.Width, index / layout.Width]))
            .GroupBy(cell => cell.Role).Select(group => new RegionDamage(RoleName(group.Key), group.Count(cell => cell.Changed), group.Count())).ToArray();

    protected static HashSet<int> DamagedWords(BinaryImage original, BinaryImage observed, CodeLayout layout)
    {
        var result = new HashSet<int>();
        for (var y = 0; y < layout.Height; y++)
            for (var x = 0; x < layout.Width; x++)
                if (layout[x, y].Codeword >= 0 && original[x, y] != observed[x, y]) result.Add(layout[x, y].Codeword);
        return result;
    }

    private static string RoleName(ModuleRole role) => role switch
    {
        ModuleRole.Finder => "Поисковые узоры", ModuleRole.Alignment => "Выравнивающие узоры", ModuleRole.Timing => "Метки времени",
        ModuleRole.Format => "Формат", ModuleRole.Version => "Версия", ModuleRole.Separator => "Разделители", ModuleRole.Fixed => "Фиксированный модуль",
        ModuleRole.Guard => "Старт / стоп / защитные штрихи", ModuleRole.Checksum => "Контрольная сумма", ModuleRole.RowIndicator => "Индикаторы строк",
        ModuleRole.QuietZone => "Тихая зона", ModuleRole.Remainder => "Остаточные биты", _ => "Кодовые слова"
    };

    protected const string ReferenceWarning = "Оценка по сетке исходника: при геометрических искажениях это не сетка, выделенная декодером. Точную причину отказа по ней установить нельзя.";
}

internal static class CodeInspectors
{
    private static readonly CodeInspector[] Inspectors = [new QrCodeInspector(), new Ean13Inspector(), new Code128Inspector(), new Pdf417Inspector()];
    public static CodeInspector For(GenerationOptions settings) => Inspectors.Single(inspector => inspector.Supports(settings));
}
