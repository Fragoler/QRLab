using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRLab.Core.Models.Mutations;
using QRLab.View.ViewModels.Common;

namespace QRLab.View.ViewModels.Mutations;

public sealed partial class PrintDefectsSettingsViewModel(PrintDefectsOptions options) : MutationSettingsViewModel
{
    [ObservableProperty] private int _count = options.Count;
    [ObservableProperty] private double _diameter = options.Diameter;
    [ObservableProperty] private PrintDefectSizeDistribution _distribution = options.Distribution;
    [ObservableProperty] private double _sizeVariationPercent = options.SizeVariationPercent;
    [ObservableProperty] private decimal? _seed = options.Seed;
    public int MaximumCount => PrintDefectsOptions.MaximumCount;
    public IReadOnlyList<EnumOption<PrintDefectSizeDistribution>> Distributions => MutationChoices.PrintDefectDistributions;
    public override MutationOptions ToOptions()
    {
        if (Seed is null || Seed != decimal.Truncate(Seed.Value)) throw new ArgumentException("Seed должен быть целым числом.");
        return new PrintDefectsOptions
        {
            Count = Count, Diameter = Diameter, Distribution = Distribution, SizeVariationPercent = SizeVariationPercent,
            Seed = checked((int)Seed.Value)
        };
    }
    [RelayCommand] private void Randomize() => Seed = Seed >= int.MaxValue ? 0 : (Seed ?? MutationDefaults.Seed) + 1;
}
