using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRLab.Core.Models.Mutations;

namespace QRLab.View.ViewModels.Mutations;

public sealed partial class SaltAndPepperSettingsViewModel(SaltAndPepperOptions options) : MutationSettingsViewModel
{
    [ObservableProperty] private double _densityPercent = options.DensityPercent;
    [ObservableProperty] private double _saltPercent = options.SaltPercent;
    [ObservableProperty] private decimal? _seed = options.Seed;
    public override MutationOptions ToOptions() => new SaltAndPepperOptions
    {
        DensityPercent = DensityPercent, SaltPercent = SaltPercent,
        Seed = checked((int)(Seed ?? MutationDefaults.Seed))
    };
    [RelayCommand] private void Randomize() => Seed = Seed >= int.MaxValue ? 0 : (Seed ?? MutationDefaults.Seed) + 1;
}
