namespace QRLab.Core.Models.Images;

/// <summary>Compares encoder modules with the grid sampled by the QR detector.</summary>
public sealed class BitmapComparison
{
    public int Total { get; }
    public int AddedBlack { get; }
    public int RemovedBlack { get; }
    public int OriginalBlack { get; }
    public int FinalBlack { get; }
    public int Changed => AddedBlack + RemovedBlack;
    public int Unchanged => Total - Changed;
    public double ChangedPercent => 100d * Changed / Total;

    public BitmapComparison(BinaryImage original, BinaryImage final)
    {
        if (original.Width != final.Width || original.Height != final.Height)
            throw new ArgumentException("Для сравнения нужны матрицы модулей одинакового размера.");
        Total = original.Width * original.Height;
        for (var y = 0; y < original.Height; y++)
            for (var x = 0; x < original.Width; x++)
            {
                var before = original[x, y];
                var after = final[x, y];
                if (before) OriginalBlack++;
                if (after) FinalBlack++;
                if (!before && after) AddedBlack++;
                if (before && !after) RemovedBlack++;
            }
    }
}
