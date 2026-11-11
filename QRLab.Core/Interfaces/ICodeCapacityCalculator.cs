using QRLab.Core.Models.Generation;

namespace QRLab.Core.Interfaces.Generation;

public interface ICodeCapacityCalculator
{
    int Calculate(GenerationOptions options, string content);
}
