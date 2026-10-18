using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IconPicker.App.Services;
using IconPicker.Core.Applying;
using IconPicker.Core.Candidates;
using IconPicker.Core.Folders;
using IconPicker.Core.Icons;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace IconPicker.App.ViewModels;

/// <summary>"This folder": the folder's current icon, the one picked for it, and the picker.</summary>
public sealed partial class SingleModeViewModel : ObservableObject
{
    private const int LargeIconSize = 256;

    private readonly DirectoryInfo _folder;
    private readonly string? _shortcutPath;
    private readonly SettingsStore _settings;
    private readonly ThumbnailLoader _thumbnailLoader;
    private readonly ElevatedRetry _elevatedRetry;
    private IReadOnlyList<IconChange> _refusedChanges = [];
    private bool _hasUserChoice;
    private CancellationTokenSource? _searchCancellation;

    /// <param name="shortcutPath">A shortcut to the folder that shows its icon too, when the folder was opened through one.</param>
    internal SingleModeViewModel(DirectoryInfo folder, string? shortcutPath, IServiceProvider services)
    {
        _folder = folder;
        _shortcutPath = shortcutPath;
        _settings = services.GetRequiredService<SettingsStore>();
        _thumbnailLoader = services.GetRequiredService<ThumbnailLoader>();
        _elevatedRetry = services.GetRequiredService<ElevatedRetry>();
        Picker = new IconPickerViewModel(folder, services.GetRequiredService<PickerService>(), _thumbnailLoader, _settings, services.GetRequiredService<IconBuilderService>());
        Picker.PropertyChanged += OnPickerPropertyChanged;
        Picker.ChoiceChangedByUser += OnChoiceChangedByUser;
    }

    public IconPickerViewModel Picker { get; }

    public StatusMessage Status { get; } = new();

    public bool HasUnappliedChanges => _hasUserChoice;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    public partial FolderIconState State { get; private set; } = FolderIconState.NoIcon;

    [ObservableProperty]
    public partial ImageSource? CurrentIcon { get; private set; }

    [ObservableProperty]
    public partial string CurrentIconDescription { get; private set; } = "";

    [ObservableProperty]
    public partial ImageSource? NewIcon { get; private set; }

    [ObservableProperty]
    public partial string NewIconDescription { get; private set; } = "";

    [ObservableProperty]
    public partial bool IsVersionFolderWarningOpen { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand), nameof(RemoveCommand))]
    public partial bool IsBusy { get; private set; }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        UpdateNewIconDescription();
        await RefreshCurrentIconAsync();
        if (Picker.HasUserChoice)
        {
            Picker.StopSearch();
            return;
        }
        using var searchCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _searchCancellation = searchCancellation;
        Picker.IsLoading = true;
        try
        {
            var candidates = await CandidateFinder.FindAsync(_folder, [_folder.Name], searchCancellation.Token);
            Picker.SetCandidates(candidates);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Stopped by a pick made by hand, not by leaving the folder.
            Picker.StopSearch();
        }
        finally
        {
            _searchCancellation = null;
        }
    }

    private bool CanApply => !IsBusy && Picker.Choice is not null;

    [RelayCommand(CanExecute = nameof(CanApply))]
    private Task ApplyAsync()
    {
        return RunChangesAsync([new IconChange(_folder.FullName, IconChangeKind.Set, Picker.Choice!.Source, _shortcutPath)]);
    }

    private bool CanRemove => !IsBusy && State.HasCustomIcon;

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private Task RemoveAsync()
    {
        return RunChangesAsync([new IconChange(_folder.FullName, IconChangeKind.Remove, ShortcutPath: _shortcutPath)]);
    }

    [RelayCommand]
    private async Task RetryAsAdminAsync()
    {
        IsBusy = true;
        Status.Close();
        try
        {
            var outcomes = await _elevatedRetry.RunAsync(_refusedChanges, _settings.Current.IconTarget);
            if (outcomes is null)
            {
                Status.Show(InfoBarSeverity.Warning, Strings.Get("AdminNotGrantedTitle"), Strings.Get("AdminNotGrantedMessage"));
                return;
            }
            await ShowOutcomesAsync(outcomes, wasElevated: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunChangesAsync(IReadOnlyList<IconChange> changes)
    {
        IsBusy = true;
        Status.Close();
        try
        {
            var outcomes = await IconApplyService.ApplyAsync(changes, _settings.Current.IconTarget);
            await ShowOutcomesAsync(outcomes, wasElevated: false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ShowOutcomesAsync(IReadOnlyList<IconChangeOutcome> outcomes, bool wasElevated)
    {
        _refusedChanges = outcomes.Where(outcome => outcome.Result == IconChangeResult.Refused).Select(outcome => outcome.Change).ToList();
        if (outcomes.Any(outcome => outcome.Result == IconChangeResult.Applied))
        {
            _hasUserChoice = false;
        }
        DisplayText.ShowOutcomes(Status, outcomes, RetryAsAdminCommand, wasElevated);
        await RefreshCurrentIconAsync();
    }

    private async Task RefreshCurrentIconAsync()
    {
        var state = await Task.Run(() => FolderIconState.Read(_folder, _shortcutPath));
        State = state;
        CurrentIconDescription = DisplayText.DescribeIconState(state, _folder.FullName);
        CurrentIcon = await _thumbnailLoader.LoadAsync(() => IconPreviewRenderer.RenderFolderIconAsync(state, LargeIconSize));
    }

    private void OnChoiceChangedByUser(object? sender, EventArgs eventArgs)
    {
        _hasUserChoice = true;
        // A pick made by hand ends the folder's search: its results would only replace the pick.
        _searchCancellation?.Cancel();
    }

    private async void OnPickerPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName != nameof(IconPickerViewModel.Choice))
        {
            return;
        }
        ApplyCommand.NotifyCanExecuteChanged();
        UpdateNewIconDescription();
        var choice = Picker.Choice;
        var newIcon = choice is null ? null : await _thumbnailLoader.LoadAsync(() => IconPreviewRenderer.RenderAsync(choice.Source, LargeIconSize));
        // A quicker later pick may have finished first.
        if (ReferenceEquals(choice, Picker.Choice))
        {
            NewIcon = newIcon;
        }
    }

    private void UpdateNewIconDescription()
    {
        var choice = Picker.Choice;
        if (choice is null)
        {
            NewIconDescription = Strings.Get("NoNewIcon");
            IsVersionFolderWarningOpen = false;
            return;
        }
        var target = _settings.Current.IconTarget;
        if (IconTargetResolver.WillLink(_folder, choice.Source, target))
        {
            NewIconDescription = Strings.Format("NewIconWillLink", Path.GetRelativePath(_folder.FullName, choice.Source.FilePath));
        }
        else
        {
            NewIconDescription = Strings.Get(choice.Source.IsConverted ? "NewIconWillConvert" : "NewIconWillCopy");
        }
        IsVersionFolderWarningOpen = IconTargetResolver.LinksIntoVersionFolder(_folder, choice.Source, target);
    }
}
