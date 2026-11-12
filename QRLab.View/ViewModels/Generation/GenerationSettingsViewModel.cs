using CommunityToolkit.Mvvm.ComponentModel;
using System.Text;
using QRLab.Core.Interfaces.Generation;
using QRLab.Image.Generation;
using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Generation.Barcode;
using QRLab.Core.Models.Generation.Qrcode;
using QRLab.View.ViewModels.Common;

namespace QRLab.View.ViewModels.Generation;

public sealed partial class GenerationSettingsViewModel : ObservableObject
{
    private readonly ICodeCapacityCalculator _capacity;

    public GenerationSettingsViewModel() : this(new CodeCapacityCalculator()) { }

    public GenerationSettingsViewModel(ICodeCapacityCalculator capacity)
    {
        _capacity = capacity;
        PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(Template) or nameof(Source) or nameof(RandomLength)
                or nameof(FirstNumber) or nameof(Count) or nameof(Mode) or nameof(Format))
                OnPropertyChanged(nameof(ContentUsage));
        };
        Qr.PropertyChanged += (_, _) => OnPropertyChanged(nameof(ContentUsage));
        Barcode.PropertyChanged += (_, _) => OnPropertyChanged(nameof(ContentUsage));
    }
    public IReadOnlyList<EnumOption<GenerationMode>> Modes => GenerationChoices.Modes;
    public IReadOnlyList<EnumOption<CodeKind>> Formats => GenerationChoices.Formats;
    public IReadOnlyList<EnumOption<ContentSource>> Sources => IsBatch
        ? GenerationChoices.Sources
        : GenerationChoices.SingleSources;
    
    public GenerationInputLimits Limits { get; } = new();

    [ObservableProperty] private GenerationMode _mode = GenerationDefaults.Mode;
    [ObservableProperty] private CodeKind _format = GenerationDefaults.Kind;
    [ObservableProperty] private ContentSource _source = GenerationDefaults.Source;
    [ObservableProperty] private string _template = GenerationDefaults.Template;
    [ObservableProperty] private decimal? _randomLength = GenerationDefaults.RandomLength;
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
    public bool IsRandom => IsBatch && Source == ContentSource.RandomString;
    public bool IsText => !IsRandom;
    public string ContentUsage
    {
        get
        {
            try
            {
                var options = ToOptions();
                // Preview the longest numbered payload in the batch without generating random data.
                var content = IsRandom
                    ? new string(options is Ean13GenerationOptions ? '1' : 'a', options.RandomLength)
                    : options.Source == ContentSource.NumberedTemplate
                        ? options.Template.Replace(GenerationDefaults.NumberPlaceholder,
                            checked(options.FirstNumber + options.Count - 1).ToString(GenerationDefaults.NumberFormat))
                        : options.Template;
                var length = content.EnumerateRunes().Count();
                return $"{length} / {_capacity.Calculate(options, content)}";
            }
            catch (Exception exception) when (exception is ArgumentException or OverflowException)
            {
                return "— / —";
            }
        }
    }

    partial void OnSourceChanged(ContentSource value)
    {
        OnPropertyChanged(nameof(IsRandom));
        OnPropertyChanged(nameof(IsText));
    }
    public string GenerateLabel => IsBatch ? $"Подготовить {Count} кодов" : "Подготовить код";

    partial void OnFormatChanged(CodeKind value)
    {
        OnPropertyChanged(nameof(IsQr));
        OnPropertyChanged(nameof(IsBarcode));
    }
    partial void OnModeChanged(GenerationMode value)
    {
        if (!IsBatch && Source == ContentSource.RandomString) Source = GenerationDefaults.Source;
        OnPropertyChanged(nameof(Sources));
        OnPropertyChanged(nameof(IsRandom));
        OnPropertyChanged(nameof(IsText));
        OnPropertyChanged(nameof(IsBatch));
        OnPropertyChanged(nameof(GenerateLabel));
    }
    partial void OnCountChanged(decimal? value) => OnPropertyChanged(nameof(GenerateLabel));

    public GenerationOptions ToOptions()
    {
        GenerationOptions settings = IsQr
            ? new QrGenerationOptions { Version = Integer(Qr.Version), Correction = Qr.Correction, MaskPattern = Qr.MaskPattern }
            : BarcodeGenerationOptions.Create(Barcode.Type);
        return settings with
        {
            Template = Template, FirstNumber = Integer(FirstNumber),
            Count = IsBatch ? Integer(Count) : GenerationDefaults.SingleCount,
            Width = Integer(ImageWidth), Height = Integer(ImageHeight),
            QuietZone = Integer(QuietZone), Source = Source,
            RandomLength = Source == ContentSource.RandomString ? Integer(RandomLength) : GenerationDefaults.RandomLength
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
        Mode = count == GenerationDefaults.SingleCount && settings.Source != ContentSource.RandomString
            ? GenerationMode.Single : GenerationMode.Batch;
        Format = settings.Kind;
        ImageWidth = settings.Width;
        ImageHeight = settings.Height;
        QuietZone = settings.QuietZone;
        Source = settings.Source;
        RandomLength = settings.RandomLength;
        if (settings is QrGenerationOptions qr)
        {
            Qr.Version = qr.Version;
            Qr.Correction = qr.Correction;
            Qr.MaskPattern = qr.MaskPattern;
        }
        else if (settings is BarcodeGenerationOptions barcode) Barcode.Type = barcode.BarcodeType;
    }

}
