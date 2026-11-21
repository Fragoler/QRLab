using QRLab.Core.Models.Generation.Barcode;

namespace QRLab.Core.Models.Generation;

public sealed record Pdf417GenerationOptions : BarcodeGenerationOptions
{
    public override BarcodeType BarcodeType => BarcodeType.Pdf417;
    public int CorrectionLevel { get; init; } = 2;
    public int MinimumColumns { get; init; } = 2;
    public int MaximumColumns { get; init; } = 30;
    public int MinimumRows { get; init; } = 3;
    public int MaximumRows { get; init; } = 90;
    public int RowHeight { get; init; } = 4;
    public Pdf417Compaction Compaction { get; init; } = Pdf417Compaction.Auto;

    public override string PrepareContent(int number)
    {
        var content = base.PrepareContent(number);
        if (Compaction == Pdf417Compaction.Numeric && content.Any(c => c is < '0' or > '9'))
            throw new ArgumentException("Числовое уплотнение PDF417 допускает только цифры 0–9.");
        if (Compaction == Pdf417Compaction.Text && content.Any(c => c is not ('\t' or '\n' or '\r') && (c < ' ' || c > '~')))
            throw new ArgumentException("Текстовое уплотнение PDF417 допускает печатные ASCII-символы. Для Unicode выберите авто или байты.");
        return content;
    }

    public override void Validate()
    {
        base.Validate();
        if (CorrectionLevel is < 0 or > 8)
            throw new ArgumentException("Уровень коррекции PDF417 должен быть от 0 до 8.");
        if (MinimumColumns < 1 || MaximumColumns > 30 || MinimumColumns > MaximumColumns)
            throw new ArgumentException("Столбцы PDF417: 1–30, минимум не больше максимума.");
        if (MinimumRows < 3 || MaximumRows > 90 || MinimumRows > MaximumRows)
            throw new ArgumentException("Строки PDF417: 3–90, минимум не больше максимума.");
        if (RowHeight is < 1 or > 4)
            throw new ArgumentException("Высота строки PDF417 должна быть от 1 до 4 модулей.");
        if (!Enum.IsDefined(Compaction)) throw new ArgumentException("Неизвестный режим уплотнения PDF417.");
    }
}
