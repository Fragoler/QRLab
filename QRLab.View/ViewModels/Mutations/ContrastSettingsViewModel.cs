using CommunityToolkit.Mvvm.ComponentModel;
using QRLab.Core.Models.Mutations;

namespace QRLab.View.ViewModels.Mutations;

public sealed partial class ContrastSettingsViewModel(ContrastOptions options) : MutationSettingsViewModel
{
    [ObservableProperty] private double _reduction = options.Reduction;
    [ObservableProperty] private double _center = options.Center;
    
    public override MutationOptions ToOptions() => new ContrastOptions { Reduction = Reduction, Center = Center };
}
