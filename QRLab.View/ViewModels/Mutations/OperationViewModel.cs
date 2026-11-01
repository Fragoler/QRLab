using QRLab.Core.Models.Images;
using QRLab.Core.Models.Mutations;

namespace QRLab.View.ViewModels.Mutations;

public sealed record OperationViewModel(string Number, MutationOptions? Options, RasterImage Image)
{
    public string Name => Options is null ? MutationChoices.Original.Name
        : MutationChoices.Name(Options.Kind);
    public string Details => Options is null ? MutationChoices.Original.Description : MutationChoices.Describe(Options);
}
