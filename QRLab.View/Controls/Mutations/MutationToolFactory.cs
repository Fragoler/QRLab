using QRLab.View.ViewModels.Mutations;

namespace QRLab.View.Controls.Mutations;

public static class MutationToolFactory
{
    private static readonly IReadOnlyDictionary<Type, Func<MutationSettingsViewModel, IMutationTool>> Tools =
        new Dictionary<Type, Func<MutationSettingsViewModel, IMutationTool>>
        {
            [typeof(OcclusionSettingsViewModel)] = settings => new OcclusionTool((OcclusionSettingsViewModel)settings),
            [typeof(MarkerSettingsViewModel)] = settings => new MarkerTool((MarkerSettingsViewModel)settings)
        };

    public static IMutationTool? Create(MutationSettingsViewModel? settings) =>
        settings is not null && Tools.TryGetValue(settings.GetType(), out var create) ? create(settings) : null;
}
