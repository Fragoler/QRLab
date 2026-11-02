namespace QRLab.View.Presentation;

public sealed record EnumOption<T>(T Id, string Name, string Icon = "") : EnumOption(Name, Icon) where T : struct, Enum
{
    public override Enum Value => Id;
}
