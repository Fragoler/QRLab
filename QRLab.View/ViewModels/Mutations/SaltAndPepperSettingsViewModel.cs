using CommunityToolkit.Mvvm.ComponentModel;
using QRLab.Core.Models.Mutations;

namespace QRLab.View.ViewModels.Mutations;

public sealed partial class SaltAndPepperSettingsViewModel(SaltAndPepperOptions options) : MutationSettingsViewModel
{
    [ObservableProperty] private double _densityPercent = options.DensityPercent;
    [ObservableProperty] private double _saltPercent = options.SaltPercent;
    [ObservableProperty] private decimal? _seed = options.Seed;
    [ObservableProperty] private bool _randomForEachSample = options.RandomForEachSample;
    public override MutationOptions ToOptions() => new SaltAndPepperOptions
    {
        DensityPercent = DensityPercent, SaltPercent = SaltPercent,
        Seed = checked((int)(Seed ?? MutationDefaults.Seed)), RandomForEachSample = RandomForEachSample
    };
}
