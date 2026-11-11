using QRLab.Core.Models.Images;

namespace QRLab.Core.Models.Recognition;

public enum ModuleRole { Data, Finder, Separator, Alignment, Timing, Format, Version, Fixed, Remainder, Guard, RowIndicator, Checksum, QuietZone }

public readonly record struct ModuleCell(ModuleRole Role, int Codeword = -1, int Bit = -1)
{
    // Zero-based correction block; service modules and linear barcodes have none.
    public int Block { get; init; } = -1;
}

public sealed class CodeLayout(int width, int height, IReadOnlyList<ModuleCell> cells)
{
    public int Width { get; } = width;
    public int Height { get; } = height;
    public IReadOnlyList<ModuleCell> Cells { get; } = Array.AsReadOnly(cells.ToArray());
    public ModuleCell this[int x, int y] => Cells[y * Width + x];
}

public sealed record RegionDamage(string Name, int Changed, int Total);
public sealed record CorrectionBlock(int Number, int DamagedWords, int TotalWords, int CorrectionWords)
{
    public int Capacity => CorrectionWords / 2;
    public int Erasures { get; init; }
    public bool ExceedsCapacity => DamagedWords > Capacity;
}

public sealed record CodeInspection(
    string Format, CodeLayout Layout, BinaryImage? Observed, BinaryImage ReferenceProbe,
    BinaryImage? Restored, string SamplingDescription, string RecoveryDescription,
    string Conclusion, IReadOnlyList<RegionDamage> Regions, IReadOnlyList<CorrectionBlock> Blocks,
    int DamagedCodewords, int TotalCodewords, int CorrectionCapacity)
{
    public InspectionBasis Basis { get; init; }
    public bool IsReferenceEstimate => Observed is null || Basis == InspectionBasis.ReferenceGrid;
    public BitmapComparison? Modules { get; init; }
    public IReadOnlyList<InspectionParameter> Parameters { get; init; } = [];
    public IReadOnlyList<InspectionSection> Sections { get; init; } = [];
}
