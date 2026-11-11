using QRLab.Core.Models.Images;
using QRLab.Core.Models.Recognition;
using ZXing;

namespace QRLab.Image.Recognition.Inspection;

internal sealed class InspectionReportBuilder : IInspectionReportBuilder
{
    public CodeInspection Build(CodeInspection inspection, BinaryImage original, Result? decoded)
    {
        var measured = inspection.Observed ?? inspection.ReferenceProbe;
        var modules = original.Width == measured.Width && original.Height == measured.Height ? new BitmapComparison(original, measured) : null;
        var values = new List<InspectionParameter>();
        void Add(InspectionCategory category, string name, string value) => values.Add(new(category, name, value));
        Add(InspectionCategory.Symbol, "Формат", inspection.Format);
        Add(InspectionCategory.Symbol, "Основа сравнения", inspection.IsReferenceEstimate ? "Сетка исходника (оценка)" : inspection.Basis switch
        {
            InspectionBasis.Scanline => "Сканирующая строка",
            InspectionBasis.DetectorBounds => "Границы детектора",
            _ => "Матрица считывания"
        });
        values.AddRange(inspection.Parameters);
        if (modules is not null)
        {
            Add(InspectionCategory.Modules, "Изменено / всего", $"{modules.Changed:N0} / {modules.Total:N0}");
            Add(InspectionCategory.Modules, "Белый → чёрный", $"{modules.AddedBlack:N0}");
            Add(InspectionCategory.Modules, "Чёрный → белый", $"{modules.RemovedBlack:N0}");
            Add(InspectionCategory.Modules, "Чёрных в исходнике", $"{modules.OriginalBlack:N0}");
            Add(InspectionCategory.Modules, "Чёрных в результате", $"{modules.FinalBlack:N0}");
            Add(InspectionCategory.Modules, "Без изменений", $"{modules.Unchanged:N0}");
        }
        Add(InspectionCategory.Codewords, "Повреждено / всего", $"{inspection.DamagedCodewords:N0} / {inspection.TotalCodewords:N0}");
        Add(InspectionCategory.Correction, "Предел коррекции ошибок", $"{inspection.CorrectionCapacity:N0}");
        var exceeded = inspection.Blocks.Count > 0 ? inspection.Blocks.Any(block => block.ExceedsCapacity) : inspection.DamagedCodewords > 0;
        Add(InspectionCategory.Correction, "Лимит превышен", exceeded ? "Да" : "Нет");
        if (decoded?.ResultMetadata?.TryGetValue(ResultMetadataType.ERRORS_CORRECTED, out var corrected) == true && corrected is int count)
            Add(InspectionCategory.Correction, "Исправлено кодовых слов", $"{count:N0}");
        foreach (var block in inspection.Blocks)
        {
            Add(InspectionCategory.Blocks, $"Блок {block.Number} · повреждено / лимит / всего", $"{block.DamagedWords:N0} / {block.Capacity:N0} / {block.TotalWords:N0}");
        }
        foreach (var region in inspection.Regions)
            Add(InspectionCategory.Zones, region.Name == "Кодовые слова" ? "Модули данных" : region.Name, $"{region.Changed:N0} / {region.Total:N0}");
        var parameters = Array.AsReadOnly(values.OrderBy(parameter => parameter.Category).ToArray());
        var sections = parameters.GroupBy(parameter => parameter.Category)
            .Select(group => new InspectionSection(group.Key, Title(group.Key), Array.AsReadOnly(group.ToArray()))).ToArray();
        return inspection with { Modules = modules, Parameters = parameters, Sections = Array.AsReadOnly(sections) };
    }

    private static string Title(InspectionCategory category) => category switch
    {
        InspectionCategory.Symbol => "Код и источник данных", InspectionCategory.Modules => "Модули",
        InspectionCategory.Codewords => "Кодовые слова", InspectionCategory.Correction => "Коррекция ошибок",
        InspectionCategory.Blocks => "Блоки коррекции", InspectionCategory.Zones => "Зоны · изменено / всего",
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };
}
