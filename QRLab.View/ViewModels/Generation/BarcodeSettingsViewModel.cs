using CommunityToolkit.Mvvm.ComponentModel;
using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Generation.Barcode;
using QRLab.View.ViewModels.Common;

namespace QRLab.View.ViewModels.Generation;

public sealed partial class BarcodeSettingsViewModel : ObservableObject
{
    [ObservableProperty] private BarcodeType _type = GenerationDefaults.Barcode;
    [ObservableProperty] private decimal? _correctionLevel = 2;
    [ObservableProperty] private decimal? _minimumColumns = 2;
    [ObservableProperty] private decimal? _maximumColumns = 30;
    [ObservableProperty] private decimal? _minimumRows = 3;
    [ObservableProperty] private decimal? _maximumRows = 90;
    [ObservableProperty] private decimal? _rowHeight = 4;
    [ObservableProperty] private Pdf417Compaction _compaction = Pdf417Compaction.Auto;
    public IReadOnlyList<EnumOption<BarcodeType>> Types => GenerationChoices.BarcodeTypes;
    public IReadOnlyList<EnumOption<Pdf417Compaction>> Compactions { get; } =
    [
        new(Pdf417Compaction.Auto, "Авто"), new(Pdf417Compaction.Text, "Текст (ASCII)"),
        new(Pdf417Compaction.Byte, "Байты (UTF-8)"), new(Pdf417Compaction.Numeric, "Цифры")
    ];

    public BarcodeGenerationOptions ToOptions() => Type == BarcodeType.Pdf417
        ? new Pdf417GenerationOptions
        {
            CorrectionLevel = Integer(CorrectionLevel), MinimumColumns = Integer(MinimumColumns),
            MaximumColumns = Integer(MaximumColumns), MinimumRows = Integer(MinimumRows),
            MaximumRows = Integer(MaximumRows), RowHeight = Integer(RowHeight), Compaction = Compaction
        }
        : throw new ArgumentException("Настройки выбранного штрихкода не зарегистрированы.");

    public void ApplyOptions(Pdf417GenerationOptions options)
    {
        Type = options.BarcodeType;
        CorrectionLevel = options.CorrectionLevel;
        MinimumColumns = options.MinimumColumns;
        MaximumColumns = options.MaximumColumns;
        MinimumRows = options.MinimumRows;
        MaximumRows = options.MaximumRows;
        RowHeight = options.RowHeight;
        Compaction = options.Compaction;
    }

    private static int Integer(decimal? value) => value is { } number && number == decimal.Truncate(number)
        ? checked((int)number) : throw new ArgumentException("Заполните параметры PDF417 целыми числами.");
}
