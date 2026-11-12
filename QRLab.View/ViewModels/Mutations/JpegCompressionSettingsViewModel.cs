using CommunityToolkit.Mvvm.ComponentModel;
using QRLab.Core.Models.Mutations;

namespace QRLab.View.ViewModels.Mutations;

public sealed partial class JpegCompressionSettingsViewModel(JpegCompressionOptions options) : MutationSettingsViewModel
{
    [ObservableProperty] private int _quality = options.Quality;
    public override MutationOptions ToOptions() => new JpegCompressionOptions { Quality = Quality };
}
