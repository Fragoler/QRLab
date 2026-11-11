using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Images;
using QRLab.Core.Models.Recognition;
using QRLab.Core.Models.Samples;
using ZXing;
using ZXing.Common;

namespace QRLab.Image.Recognition.Inspection;

internal sealed class QrCodeInspector(IQrStructureProvider structures) : CodeInspector
{
    public override bool Supports(GenerationOptions settings) => settings.Kind == CodeKind.QrCode;
    public override CodeLayout CreateLayout(BinaryImage original, GenerationOptions settings) => structures.Get(original.Width).Layout;

    public override CodeInspection Inspect(SamplePreview sample, BitMatrix raster, Result? decoded, BinaryImage? detected)
    {
        var original = sample.OriginalBitmap!;
        var structure = structures.Get(original.Width);
        var layout = structure.Layout;
        var probe = Probe(sample, raster);
        var compatible = detected?.Width == original.Width && detected.Height == original.Height;
        var observed = compatible ? detected! : probe;
        var format = QrStructure.ReadFormat(original)!.Value;
        var map = structure.Interleaving(format.Level, out var counts, out var parity);
        var damaged = DamagedWords(original, observed, layout);
        var blocks = counts.Select((count, index) => new CorrectionBlock(index + 1,
            damaged.Count(word => map[word].Block == index), count + parity, parity)).ToArray();
        var regions = Damage(original, observed, layout);
        var damagedServices = regions.Where(region => region.Name != "Кодовые слова" && region.Name != "Остаточные биты" && region.Changed > 0);
        var conclusion = $"QR v{structure.Version.VersionNumber}, {format.Level}: повреждено {damaged.Count} слов; суммарный предел {blocks.Sum(block => block.Capacity)} (проверяется отдельно для каждого блока).";
        if (!compatible) conclusion += " " + ReferenceWarning;
        else if (decoded is null && QrStructure.ReadFormat(observed) is null) conclusion += " Обе копии информации формата не прошли BCH-декодирование.";
        if (blocks.Any(block => block.ExceedsCapacity)) conclusion += " Превышен гарантированный предел коррекции хотя бы одного блока.";
        if (damagedServices.Any()) conclusion += " Затронуты служебные зоны: " + string.Join(", ", damagedServices.Select(region => $"{region.Name} ({region.Changed})")) + ".";
        if (decoded is null && compatible && blocks.All(block => !block.ExceedsCapacity))
            conclusion += " Предел Рида — Соломона по известному исходнику не превышен; отказ мог произойти при чтении служебных полей или из-за неверной сетки. Единственная причина не установлена.";
        if (decoded is not null && decoded.Text != sample.Content)
            conclusion += " Расшифрованное содержимое не совпало с ожидаемым; восстановленная матрица не подтверждает исходное сообщение.";
        return new("QR", layout, compatible ? detected : null, probe, null,
            compatible ? "Сетка модулей, выделенная QR-детектором" : ReferenceWarning,
            "",
            conclusion, regions, blocks, damaged.Count, structure.Version.TotalCodewords, blocks.Sum(block => block.Capacity))
        {
            Basis = compatible ? InspectionBasis.DetectedModules : InspectionBasis.ReferenceGrid,
            Parameters = [new(InspectionCategory.Symbol, "Версия QR", structure.Version.VersionNumber.ToString()),
                new(InspectionCategory.Correction, "Уровень коррекции", format.Level.ToString())]
        };
    }
}
