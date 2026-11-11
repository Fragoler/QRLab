namespace QRLab.Core.Models.Recognition;

public enum InspectionCategory { Symbol, Modules, Codewords, Correction, Blocks, Zones }
public enum InspectionBasis { DetectedModules, Scanline, DetectorBounds, ReferenceGrid }

public sealed record InspectionParameter(InspectionCategory Category, string Name, string Value);
public sealed record InspectionSection(InspectionCategory Category, string Title, IReadOnlyList<InspectionParameter> Parameters);
