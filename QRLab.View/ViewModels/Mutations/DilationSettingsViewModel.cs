using QRLab.Core.Models.Mutations;

namespace QRLab.View.ViewModels.Mutations;

public sealed class DilationSettingsViewModel(DilationOptions options) : MorphologySettingsViewModel(options)
{
    protected override MorphologyOptions CreateOptions() => new DilationOptions();
}
