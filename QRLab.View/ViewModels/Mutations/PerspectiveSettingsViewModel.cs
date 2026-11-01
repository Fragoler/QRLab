using CommunityToolkit.Mvvm.ComponentModel;
using QRLab.Core.Models.Mutations;
using QRLab.View.ViewModels.Common;

namespace QRLab.View.ViewModels.Mutations;

public sealed partial class PerspectiveSettingsViewModel(PerspectiveMutationOptions options) : MutationSettingsViewModel
{
    [ObservableProperty] private double _angleX = options.AngleX;
    [ObservableProperty] private double _angleY = options.AngleY;
    [ObservableProperty] private double _distance = options.Distance;
    [ObservableProperty] private ImageInterpolation _interpolation = options.Interpolation;
    public IReadOnlyList<EnumOption<ImageInterpolation>> Interpolations => MutationChoices.Interpolations;
    public override MutationOptions ToOptions() => new PerspectiveMutationOptions
    {
        AngleX = AngleX, AngleY = AngleY, Distance = Distance, Interpolation = Interpolation
    };
}
