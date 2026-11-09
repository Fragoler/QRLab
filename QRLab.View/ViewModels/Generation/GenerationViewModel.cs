using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRLab.Core.Models.Generation;
using QRLab.Core.Services;

namespace QRLab.View.ViewModels.Generation;

public sealed partial class GenerationViewModel(
    Workspace.WorkspaceViewModel workspace, GenerationService generation, SampleStorageService storage)
    : ScreenViewModel("01", "Генерация кодов")
{
    public Workspace.WorkspaceViewModel Workspace { get; } = workspace;
    public GenerationSettingsViewModel Settings { get; } = new();
    public IReadOnlyList<LibrarySet> LibrarySets => GenerationPresets.Library;
    [ObservableProperty] private bool _isLibraryOpen;
    [ObservableProperty] private bool _isExportOpen;
    [ObservableProperty] private string _exportDestination = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "QRLab", GenerationDefaults.NewSetName);
    [ObservableProperty] private string _loadDirectory = "";
    [ObservableProperty] private bool _isBusy;

    [RelayCommand] private void Generate() => GenerateSet(Settings.ToOptions);
    private bool GenerateSet(Func<GenerationOptions> options)
    {
        try
        {
            generation.Generate(options());
            Workspace.ShowNotice($"Подготовлено {Workspace.SampleCount} образцов");
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        { Workspace.ShowError(exception.Message); return false; }
    }
    [RelayCommand] private void OpenLibrary() => IsLibraryOpen = !IsLibraryOpen;
    [RelayCommand] private void Export() => IsExportOpen = !IsExportOpen;
    [RelayCommand] private void LoadSet(LibrarySet set)
    {
        if (!GenerateSet(() => set.Settings)) return;
        Settings.ApplyOptions(set.Settings, set.Settings.Count);
        IsLibraryOpen = false;
    }
    [RelayCommand] private void LoadFolder()
    {
        try
        {
            var set = storage.Load(LoadDirectory);
            Settings.ApplyOptions(set.Samples[0].Settings, set.Samples.Count);
            IsLibraryOpen = false;
            Workspace.ShowNotice($"Загружен набор «{set.Name}»: {set.Samples.Count} образцов");
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or InvalidDataException or UnauthorizedAccessException)
        { Workspace.ShowError(exception.Message); }
    }
    [RelayCommand] private async Task ConfirmExport()
    {
        IsBusy = true;
        try
        {
            var set = await storage.SaveAsync(new(ExportDestination));
            Workspace.ShowNotice($"Сохранён набор «{set.Name}»: {set.Samples.Count} образцов.");
            IsExportOpen = false;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or InvalidDataException or UnauthorizedAccessException)
        { Workspace.ShowError(exception.Message); }
        finally { IsBusy = false; }
    }
}
