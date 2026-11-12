using CommunityToolkit.Mvvm.ComponentModel;
using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Generation.Qrcode;
using QRLab.View.ViewModels.Common;

namespace QRLab.View.ViewModels.Generation;

public sealed partial class QrSettingsViewModel : ObservableObject
{
    [ObservableProperty] private QrCorrectionLevel _correction = GenerationDefaults.Correction;
    [ObservableProperty] private decimal? _version = GenerationDefaults.AutomaticQrVersion;
    [ObservableProperty] private int _maskPattern = GenerationDefaults.AutomaticQrMask;
    public IReadOnlyList<EnumOption<QrCorrectionLevel>> Corrections => GenerationChoices.Corrections;
    public IReadOnlyList<QrMaskOption> Masks => GenerationChoices.QrMasks;
}
