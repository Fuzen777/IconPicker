using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IconPicker.App.Services;
using IconPicker.Core.Detection;
using IconPicker.Core.Rules;
using Microsoft.Extensions.DependencyInjection;

namespace IconPicker.App.ViewModels;

/// <summary>The folder page: the folder, its mode, and the mode's own view model as <see cref="Content"/>.</summary>
public sealed partial class FolderViewModel : ObservableObject, IDisposable
{
    private readonly FolderRequest _request;
    private readonly IServiceProvider _services;
    private readonly DialogService _dialogs;
    private readonly SettingsStore _settings;
    private CancellationTokenSource? _loadCancellation;
    private bool _isChangingQuietly;

    internal FolderViewModel(FolderRequest request, IServiceProvider services)
    {
        _request = request;
        _services = services;
        _dialogs = services.GetRequiredService<DialogService>();
        _settings = services.GetRequiredService<SettingsStore>();
        Folder = new DirectoryInfo(request.FolderPaths[0]);
        IsSingleRoot = request.FolderPaths.Count == 1;
        Title = IsSingleRoot ? Folder.FullName : Strings.Format("AllLibrariesTitle", request.FolderPaths.Count);
        ShortcutText = request.ShortcutPath is null ? null : Strings.Format("FolderShortcutText", request.ShortcutPath);
        // A drive itself can't take a folder icon.
        CanUseSingleMode = IsSingleRoot && !FolderSearchRules.IsDriveRoot(Folder);
    }

    public DirectoryInfo Folder { get; }

    public string Title { get; }

    /// <summary>The folder's tab in the navigation pane: its own name, or all the libraries for "Rescan all".</summary>
    public string TabName => IsSingleRoot ? DisplayText.FolderName(Folder.FullName) : Title;

    /// <summary>For a folder opened through a shortcut, which shortcut shows its icon too in "This folder".</summary>
    public string? ShortcutText { get; }

    public bool HasShortcut => ShortcutText is not null;

    public bool IsSingleRoot { get; }

    public bool CanUseSingleMode { get; }

    /// <summary>Whether the folder is one of the remembered libraries, rescanned from Home.</summary>
    public bool IsLibrary
    {
        get => _settings.IsLibrary(Folder.FullName);
        set
        {
            if (value == IsLibrary)
            {
                return;
            }
            if (value)
            {
                _settings.AddLibrary(Folder.FullName);
            }
            else
            {
                _settings.RemoveLibrary(Folder.FullName);
            }
            OnPropertyChanged();
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBatchMode))]
    public partial FolderMode Mode { get; private set; }

    public bool IsBatchMode => Mode == FolderMode.Batch;

    [ObservableProperty]
    public partial bool IsDeep { get; set; }

    [ObservableProperty]
    public partial bool IsDetecting { get; private set; }

    /// <summary>The current mode's view model.</summary>
    [ObservableProperty]
    public partial object? Content { get; private set; }

    public bool HasUnappliedChanges => Content is SingleModeViewModel { HasUnappliedChanges: true } or BatchModeViewModel { HasUnappliedChanges: true };

    public async Task InitializeAsync()
    {
        if (_request.ForcedMode is { } forcedMode)
        {
            ChangeQuietly(forcedMode, isDeep: FolderSearchRules.IsDriveRoot(Folder));
        }
        else
        {
            IsDetecting = true;
            var detection = await Task.Run(() => ModeDetector.Detect(Folder));
            IsDetecting = false;
            ChangeQuietly(detection.Mode, detection.IsDeep);
        }
        await ShowCurrentModeAsync();
    }

    /// <summary>Switches between single and batch, asking first when picked icons would be lost. Returns whether it switched.</summary>
    public async Task<bool> SwitchModeAsync(FolderMode mode)
    {
        if (mode == Mode)
        {
            return true;
        }
        if (!await ConfirmDiscardingAsync())
        {
            return false;
        }
        Mode = mode;
        await ShowCurrentModeAsync();
        return true;
    }

    public void Dispose()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
    }

    [RelayCommand]
    private async Task ChangeFolderAsync()
    {
        var folderPath = await _services.GetRequiredService<PickerService>().PickFolderAsync();
        if (folderPath is not null)
        {
            await _services.GetRequiredService<Navigator>().OpenFolderAsync(FolderRequest.ForFolder(folderPath));
        }
    }

    partial void OnIsDeepChanged(bool value)
    {
        if (!_isChangingQuietly)
        {
            _ = ApplyDeepToggleAsync(value);
        }
    }

    private async Task ApplyDeepToggleAsync(bool isDeep)
    {
        if (!await ConfirmDiscardingAsync())
        {
            ChangeQuietly(Mode, !isDeep);
            return;
        }
        await ShowCurrentModeAsync();
    }

    private void ChangeQuietly(FolderMode mode, bool isDeep)
    {
        _isChangingQuietly = true;
        Mode = mode;
        IsDeep = isDeep;
        _isChangingQuietly = false;
    }

    private async Task<bool> ConfirmDiscardingAsync()
    {
        return !HasUnappliedChanges
            || await _dialogs.ConfirmAsync(Strings.Get("DiscardChangesTitle"), Strings.Get("DiscardChangesMessage"), Strings.Get("DiscardChangesConfirm"));
    }

    private async Task ShowCurrentModeAsync()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        var cancellationToken = _loadCancellation.Token;
        if (Mode == FolderMode.Single)
        {
            var single = new SingleModeViewModel(Folder, _request.ShortcutPath, _services);
            Content = single;
            await RunLoadAsync(() => single.LoadAsync(cancellationToken));
        }
        else
        {
            var batch = new BatchModeViewModel(_request.FolderPaths.Select(folderPath => new DirectoryInfo(folderPath)).ToList(), IsDeep, _services);
            Content = batch;
            await RunLoadAsync(() => batch.ScanAsync(cancellationToken));
        }
    }

    private async Task RunLoadAsync(Func<Task> load)
    {
        try
        {
            await load();
        }
        catch (OperationCanceledException)
        {
            // Another folder or mode was opened meanwhile.
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ErrorLog.Write(exception);
            await _dialogs.ShowMessageAsync(Strings.Get("ErrorTitle"), Strings.Format("ErrorMessage", exception.Message, ErrorLog.LogPath));
        }
    }
}
