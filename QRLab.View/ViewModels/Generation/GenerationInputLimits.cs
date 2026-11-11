using QRLab.Core.Models.Generation;
using QRLab.View.ViewModels.Common;

namespace QRLab.View.ViewModels.Generation;

public sealed class GenerationInputLimits
{
    public NumericRange ContentLength { get; } = new(1, GenerationLimits.MaximumContentLength);
    public NumericRange Count { get; } = new(GenerationLimits.MinimumCount, GenerationLimits.MaximumCount);
    public NumericRange FirstNumber { get; } = new(GenerationDefaults.FirstNumber, GenerationLimits.MaximumFirstNumber);
    public NumericRange ImageSize { get; } = new(GenerationLimits.MinimumImageSize, GenerationLimits.MaximumImageSize);
    public NumericRange QuietZone { get; } = new(GenerationLimits.MinimumQuietZone, GenerationLimits.MaximumQuietZone);
    public NumericRange QrVersion { get; } = new(GenerationDefaults.AutomaticQrVersion, GenerationLimits.MaximumQrVersion);
}
