namespace QRLab.View.Presentation;

public abstract record EnumOption(string Name, string Icon)
{
    public abstract Enum Value { get; }
}
