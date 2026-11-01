using CommunityToolkit.Mvvm.ComponentModel;
using QRLab.Core.Models.Recognition;
using QRLab.View.ViewModels.Common;

namespace QRLab.View.ViewModels.Reading;

public sealed partial class RecognitionSettingsViewModel : ObservableObject
{
    [ObservableProperty] private decimal? _timeout = RecognitionDefaults.TimeoutMilliseconds;
    [ObservableProperty] private bool _enhanced = RecognitionDefaults.Enhanced;
    public NumericRange TimeoutRange { get; } = new(RecognitionDefaults.MinimumTimeoutMilliseconds, RecognitionDefaults.MaximumTimeoutMilliseconds);
    public RecognitionOptions ToOptions()
    {
        if (Timeout is null || Timeout != decimal.Truncate(Timeout.Value))
            throw new ArgumentException("Укажите целый тайм-аут в миллисекундах.");
        return new(checked((int)Timeout.Value), Enhanced);
    }
}
