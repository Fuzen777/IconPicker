using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IconPicker.App.Services;
using IconPicker.Core.Detection;
using Microsoft.Extensions.DependencyInjection;

namespace IconPicker.App.ViewModels;

/// <summary>A remembered library: a folder rescanned often, such as a Steam library.</summary>
public sealed class LibraryItemViewModel(string folderPath, bool canRescan, bool canRemove)
{
    public string FolderPath { get; } = folderPath;

    public string Name { get; } = DisplayText.FolderName(folderPath);

    public bool Exists { get; } = Directory.Exists(folderPath);

    public bool IsMissing => !Exists;

    public bool CanRescan { get; } = canRescan && Directory.Exists(folderPath);

    public bool CanRemove { get; } = canRemove;
}

/// <summary>The library list, shown on Home (to rescan) and in Settings (to add and remove).</summary>
public sealed partial class LibrariesViewModel : ObservableObject
{
    private readonly SettingsStore _settings;
    private readonly IServiceProvider _services;
    private readonly bool _isForRescanning;

    internal LibrariesViewModel(IServiceProvider services, bool isForRescanning)
    {
        _services = services;
        _settings = services.GetRequiredService<SettingsStore>();
        _isForRescanning = isForRescanning;
        Reload();
    }

    public ObservableCollection<LibraryItemViewModel> Items { get; } = [];

    public bool HasLibraries => Items.Count > 0;

    public bool HasNoLibraries => Items.Count == 0;

    public Task RescanAsync(LibraryItemViewModel library)
    {
        return _services.GetRequiredService<Navigator>().OpenFolderAsync(FolderRequest.ForFolder(library.FolderPath, FolderMode.Batch));
    }

    public void Remove(LibraryItemViewModel library)
    {
        _settings.RemoveLibrary(library.FolderPath);
        Reload();
    }

    [RelayCommand(CanExecute = nameof(HasLibraries))]
    private Task RescanAllAsync()
    {
        var existingLibraries = Items.Where(library => library.Exists).Select(library => library.FolderPath).ToList();
        if (existingLibraries.Count == 0)
        {
            return Task.CompletedTask;
        }
        return _services.GetRequiredService<Navigator>().OpenFolderAsync(new FolderRequest(existingLibraries, FolderMode.Batch));
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var folderPath = await _services.GetRequiredService<PickerService>().PickFolderAsync();
        if (folderPath is not null)
        {
            _settings.AddLibrary(folderPath);
            Reload();
        }
    }

    private void Reload()
    {
        Items.Clear();
        foreach (var folderPath in _settings.Current.Libraries.Order(StringComparer.CurrentCultureIgnoreCase))
        {
            Items.Add(new LibraryItemViewModel(folderPath, canRescan: _isForRescanning, canRemove: !_isForRescanning));
        }
        OnPropertyChanged(nameof(HasLibraries));
        OnPropertyChanged(nameof(HasNoLibraries));
        RescanAllCommand.NotifyCanExecuteChanged();
    }
}
