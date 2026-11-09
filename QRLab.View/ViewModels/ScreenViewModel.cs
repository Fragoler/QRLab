using CommunityToolkit.Mvvm.ComponentModel;


namespace QRLab.View.ViewModels;

public abstract class ScreenViewModel(string number, string title) : ObservableObject
{
    public string Number { get; } = number;
    public string Title { get; } = title;
}
