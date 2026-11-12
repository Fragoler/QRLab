using CommunityToolkit.Mvvm.ComponentModel;
using QRLab.Core.Models.Mutations;

namespace QRLab.View.ViewModels.Mutations;

public abstract partial class MorphologySettingsViewModel(MorphologyOptions options) : MutationSettingsViewModel
{
    [ObservableProperty] private int _radiusX = options.RadiusX;
    [ObservableProperty] private int _radiusY = options.RadiusY;
    [ObservableProperty] private int _iterations = options.Iterations;
    public int MaximumRadius => MorphologyOptions.MaximumRadius;
    public int MaximumIterations => MorphologyOptions.MaximumIterations;
    protected abstract MorphologyOptions CreateOptions();
    public sealed override MutationOptions ToOptions() => CreateOptions() with
    {
        RadiusX = RadiusX, RadiusY = RadiusY, Iterations = Iterations
    };
}
