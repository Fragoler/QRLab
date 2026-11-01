using CommunityToolkit.Mvvm.ComponentModel;
using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Generation.Barcode;
using QRLab.Core.Models.Generation.Qrcode;
using QRLab.View.ViewModels.Common;

namespace QRLab.View.ViewModels.Generation;

public sealed partial class GenerationSettingsViewModel : ObservableObject
{
    public IReadOnlyList<EnumOption<GenerationMode>> Modes => GenerationChoices.Modes;
    public IReadOnlyList<EnumOption<CodeKind>> Formats => GenerationChoices.Formats;
    public IReadOnlyList<EnumOption<ContentSource>> Sources => GenerationChoices.Sources;
    
    public GenerationInputLimits Limits { get; } = new();

    [ObservableProperty] private GenerationMode _mode = GenerationDefaults.Mode;
    [ObservableProperty] private CodeKind _format = GenerationDefaults.Kind;
    [ObservableProperty] private ContentSource _source = GenerationDefaults.Source;
    [ObservableProperty] private string _template = GenerationDefaults.Template;
    [ObservableProperty] private decimal? _firstNumber = GenerationDefaults.FirstNumber;
    [ObservableProperty] private decimal? _count = GenerationDefaults.Count;
    [ObservableProperty] private decimal? _imageWidth = GenerationDefaults.Width;
    [ObservableProperty] private decimal? _imageHeight = GenerationDefaults.Height;
    [ObservableProperty] private decimal? _quietZone = GenerationDefaults.QuietZone;
    public QrSettingsViewModel Qr { get; } = new();
    public BarcodeSettingsViewModel Barcode { get; } = new();
    public bool IsQr => Format == CodeKind.QrCode;
    public bool IsBarcode => Format == CodeKind.Barcode;
    public bool IsBatch => Mode == GenerationMode.Batch;
    public string GenerateLabel => IsBatch ? $"Подготовить {Count} кодов" : "Подготовить код";

    partial void OnFormatChanged(CodeKind value)
    {
        OnPropertyChanged(nameof(IsQr));
        OnPropertyChanged(nameof(IsBarcode));
    }
    partial void OnModeChanged(GenerationMode value)
    {
        OnPropertyChanged(nameof(IsBatch));
        OnPropertyChanged(nameof(GenerateLabel));
    }
    partial void OnCountChanged(decimal? value) => OnPropertyChanged(nameof(GenerateLabel));

    public GenerationOptions ToOptions()
    {
        GenerationOptions settings = IsQr
            ? new QrGenerationOptions { Version = Integer(Qr.Version), Correction = Qr.Correction }
            : BarcodeGenerationOptions.Create(Barcode.Type);
        return settings with
        {
            Template = Template, FirstNumber = Integer(FirstNumber),
            Count = IsBatch ? Integer(Count) : GenerationDefaults.SingleCount,
            Width = Integer(ImageWidth), Height = Integer(ImageHeight),
            QuietZone = Integer(QuietZone), Source = Source
        };
    }

    private static int Integer(decimal? value)
    {
        if (value is null || value != decimal.Truncate(value.Value))
            throw new ArgumentException("Заполните числовые поля целыми числами.");
        return checked((int)value.Value);
    }

    public void ApplyOptions(GenerationOptions settings, int count)
    {
        Template = settings.Template;
        FirstNumber = settings.FirstNumber;
        Count = count;
        Mode = count == GenerationDefaults.SingleCount ? GenerationMode.Single : GenerationMode.Batch;
        Format = settings.Kind;
        ImageWidth = settings.Width;
        ImageHeight = settings.Height;
        QuietZone = settings.QuietZone;
        Source = settings.Source;
        if (settings is QrGenerationOptions qr)
        {
            Qr.Version = qr.Version;
            Qr.Correction = qr.Correction;
        }
        else if (settings is BarcodeGenerationOptions barcode) Barcode.Type = barcode.BarcodeType;
    }

}
