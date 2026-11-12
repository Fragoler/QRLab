using System.Text.Json.Serialization;

namespace QRLab.Core.Models.Mutations;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$mutation")]
[JsonDerivedType(typeof(GaussianBlurOptions), "gaussian")]
[JsonDerivedType(typeof(SaltAndPepperOptions), "salt-and-pepper")]
[JsonDerivedType(typeof(OcclusionOptions), "occlusion")]
[JsonDerivedType(typeof(JpegCompressionOptions), "jpeg-compression")]
[JsonDerivedType(typeof(ContrastOptions), "contrast")]
[JsonDerivedType(typeof(MarkerOptions), "marker")]
[JsonDerivedType(typeof(DilationOptions), "dilation")]
[JsonDerivedType(typeof(ErosionOptions), "erosion")]
public abstract record MutationOptions
{
    [JsonIgnore] public abstract MutationKind Kind { get; }
    public abstract void Validate();
}
