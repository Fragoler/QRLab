namespace QRLab.View.ViewModels.Common;

public abstract record EnumOption(string Name, string Icon)
{
    public abstract Enum Value { get; }
}
