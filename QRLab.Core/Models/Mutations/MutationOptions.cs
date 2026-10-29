using System.Text.Json.Serialization;

namespace QRLab.Core.Models.Mutations;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$mutation")]
[JsonDerivedType(typeof(GaussianBlurOptions), "gaussian")]
[JsonDerivedType(typeof(SaltAndPepperOptions), "salt-and-pepper")]
[JsonDerivedType(typeof(PerspectiveMutationOptions), "perspective")]
[JsonDerivedType(typeof(OcclusionOptions), "occlusion")]
public abstract record MutationOptions
{
    [JsonIgnore] public abstract MutationKind Kind { get; }
    public abstract void Validate();
}
