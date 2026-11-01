using CommunityToolkit.Mvvm.ComponentModel;
using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Generation.Barcode;
using QRLab.View.ViewModels.Common;

namespace QRLab.View.ViewModels.Generation;

public sealed partial class BarcodeSettingsViewModel : ObservableObject
{
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsEan13))] private BarcodeType _type = GenerationDefaults.Barcode;
    public bool IsEan13 => Type == BarcodeType.Ean13;
    public IReadOnlyList<EnumOption<BarcodeType>> Types => GenerationChoices.BarcodeTypes;
}
