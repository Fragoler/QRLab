using CommunityToolkit.Mvvm.ComponentModel;
using QRLab.Core.Models.Mutations;

namespace QRLab.View.ViewModels.Mutations;

public abstract class MutationSettingsViewModel : ObservableObject
{
    public abstract MutationOptions ToOptions();

    public static MutationSettingsViewModel FromOptions(MutationOptions options) => options switch
    {
        GaussianBlurOptions blur => new GaussianBlurSettingsViewModel(blur),
        SaltAndPepperOptions noise => new SaltAndPepperSettingsViewModel(noise),
        OcclusionOptions area => new OcclusionSettingsViewModel(area),
        JpegCompressionOptions jpeg => new JpegCompressionSettingsViewModel(jpeg),
        ContrastOptions contrast => new ContrastSettingsViewModel(contrast),
        MarkerOptions marker => new MarkerSettingsViewModel(marker),
        DilationOptions dilation => new DilationSettingsViewModel(dilation),
        ErosionOptions erosion => new ErosionSettingsViewModel(erosion),
        PrintDefectsOptions defects => new PrintDefectsSettingsViewModel(defects),
        
        _ => throw new ArgumentOutOfRangeException(nameof(options))
    };
}
