using System.Collections.Concurrent;

namespace QRLab.Image.Recognition.Inspection;

internal interface IQrStructureProvider
{
    QrStructure Get(int dimension);
}

/// <summary>
/// One version-aware structure table for QR versions 1–40, shared by layout and damage analysis.
/// </summary>
internal sealed class QrStructureProvider : IQrStructureProvider
{
    private readonly ConcurrentDictionary<int, QrStructure> _structures = new();
    public QrStructure Get(int dimension)
    {
        if (dimension is < 21 or > 177 || (dimension - 17) % 4 != 0)
            throw new ArgumentException("Некорректный размер QR: ожидается версия от 1 до 40.", nameof(dimension));
        return _structures.GetOrAdd(dimension, value => new QrStructure(value));
    }
}
