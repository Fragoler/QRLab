using QRLab.Core.Models.Mutations;

namespace QRLab.View.ViewModels.Mutations;

public sealed class ErosionSettingsViewModel(ErosionOptions options) : MorphologySettingsViewModel(options)
{
    protected override MorphologyOptions CreateOptions() => new ErosionOptions();
}
