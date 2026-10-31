namespace QRLab.View.Presentation;

public record EnumOption(Enum Value, string Name, string Icon = "");

public sealed record EnumOption<T>(T Id, string Name, string Icon = "")
    : EnumOption(Id, Name, Icon)
    where T : struct, Enum;

public sealed record NumericRange(decimal Minimum, decimal Maximum);
