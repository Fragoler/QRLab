using CommunityToolkit.Mvvm.ComponentModel;
using QRLab.Core.Models.Mutations;

namespace QRLab.View.ViewModels.Mutations;

public sealed partial class GaussianBlurSettingsViewModel(GaussianBlurOptions options) : MutationSettingsViewModel
{
    [ObservableProperty] private double _sigma = options.Sigma;
    public override MutationOptions ToOptions() => new GaussianBlurOptions { Sigma = Sigma };
}
