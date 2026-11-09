using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace QRLab.View.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    public Workspace.WorkspaceViewModel Workspace { get; }
    public Generation.GenerationViewModel Generation { get; }
    public MutationsViewModel Mutations { get; }
    public ReadingViewModel Reading { get; }
    public ComparisonViewModel Comparison { get; }
    
    public IReadOnlyList<ScreenViewModel> Screens { get; }
    
    [ObservableProperty] private ScreenViewModel _selectedScreen;
    [ObservableProperty] private bool _isAboutOpen;

    public MainWindowViewModel(Workspace.WorkspaceViewModel workspace, Generation.GenerationViewModel generation,
        MutationsViewModel mutations, ReadingViewModel reading, ComparisonViewModel comparison)
    {
        Workspace = workspace;
        Generation = generation;
        Mutations = mutations;
        Reading = reading;
        Comparison = comparison;
        Screens = [Generation, Mutations, Reading, Comparison];
        _selectedScreen = Generation;
    }
    [RelayCommand] private void Navigate(string number) => SelectedScreen = Screens.First(screen => screen.Number == number);
    [RelayCommand] private void About() => IsAboutOpen = !IsAboutOpen;
    public void Dispose()
    {
        Reading.Dispose();
        Comparison.Dispose();
        Mutations.Dispose();
        Workspace.Dispose();
    }
}
