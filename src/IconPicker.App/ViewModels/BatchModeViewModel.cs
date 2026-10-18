using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IconPicker.App.Services;
using IconPicker.Core.Applying;
using IconPicker.Core.Detection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace IconPicker.App.ViewModels;

/// <summary>The rows of one library, for "Rescan all".</summary>
public sealed class BatchRowGroup(string name, IEnumerable<BatchRowViewModel> rows) : List<BatchRowViewModel>(rows)
{
    public string Name { get; } = name;
}

/// <summary>"Each subfolder": a row per folder with its predicted icon, a side panel to change it, and Apply for the ticked rows.</summary>
public sealed partial class BatchModeViewModel : ObservableObject
{
    private const int ParallelSearches = 4;

    private readonly IReadOnlyList<DirectoryInfo> _roots;
    private readonly bool _isDeep;
    private readonly IServiceProvider _services;
    private readonly SettingsStore _settings;
    private readonly DialogService _dialogs;
    private readonly ElevatedRetry _elevatedRetry;
    private List<BatchRowViewModel> _rows = [];

    /// <summary>The rows the filters let through: the only ones Apply, Remove and Select all act on.</summary>
    private List<BatchRowViewModel> _shownRows = [];
    private Dictionary<string, BatchRowViewModel> _rowsByPath = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<IconChange> _refusedChanges = [];
    private CancellationTokenSource? _scanCancellation;

    internal BatchModeViewModel(IReadOnlyList<DirectoryInfo> roots, bool isDeep, IServiceProvider services)
    {
        _roots = roots;
        _isDeep = isDeep;
        _services = services;
        _settings = services.GetRequiredService<SettingsStore>();
        _dialogs = services.GetRequiredService<DialogService>();
        _elevatedRetry = services.GetRequiredService<ElevatedRetry>();
    }

    /// <summary>"Rescan all" groups the rows by library.</summary>
    public bool IsGrouped => _roots.Count > 1;

    /// <summary>The rows shown, for a single folder.</summary>
    public ObservableCollection<BatchRowViewModel> VisibleRows { get; } = [];

    /// <summary>The rows shown, by library, for "Rescan all".</summary>
    public ObservableCollection<BatchRowGroup> VisibleGroups { get; } = [];

    public StatusMessage Status { get; } = new();

    public bool HasUnappliedChanges => _rows.Exists(row => row.HasUnappliedChange);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedPicker), nameof(HasSelection), nameof(HasNoSelection), nameof(SelectedRowHeader))]
    public partial BatchRowViewModel? SelectedRow { get; set; }

    public IconPickerViewModel? SelectedPicker => SelectedRow?.Picker;

    public bool HasSelection => SelectedRow is not null;

    public bool HasNoSelection => SelectedRow is null;

    public string SelectedRowHeader => SelectedRow is null ? "" : Strings.Format("SidePanelHeader", SelectedRow.Title);

    [ObservableProperty]
    public partial bool HideFoldersWithIcon { get; set; }

    [ObservableProperty]
    public partial bool HideFoldersWithOnlyPictures { get; set; }

    [ObservableProperty]
    public partial bool HideFoldersWithoutMatch { get; set; }

    /// <summary>The "Select all" box: ticked when every shown row that can be is, empty when none is, in between otherwise.</summary>
    public bool? SelectAllState
    {
        get
        {
            var checkableRows = _shownRows.Where(row => row.CanCheck).ToList();
            var checkedCount = checkableRows.Count(row => row.IsChecked);
            return checkedCount == 0 ? false : checkedCount == checkableRows.Count ? true : null;
        }
    }

    public bool CanSelectAll => _shownRows.Exists(row => row.CanCheck);

    private bool IsFiltered => HideFoldersWithIcon || HideFoldersWithOnlyPictures || HideFoldersWithoutMatch;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsScanning { get; private set; }

    [ObservableProperty]
    public partial bool IsFindingFolders { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText), nameof(ProgressValue))]
    public partial int SearchedCount { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand), nameof(RemoveCommand))]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ApplyText))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial int ApplyCount { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RemoveText))]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    public partial int RemoveCount { get; private set; }

    [ObservableProperty]
    public partial string SummaryText { get; private set; } = "";

    public string ProgressText => Strings.Format("SearchProgress", SearchedCount, _rows.Count);

    public double ProgressValue => _rows.Count == 0 ? 0 : 100.0 * SearchedCount / _rows.Count;

    public string ApplyText => Strings.Format("ApplyCountButton", ApplyCount);

    public string RemoveText => Strings.Format("RemoveCountButton", RemoveCount);

    public bool IsEmpty => !IsScanning && _rows.Count == 0;

    internal async Task ScanAsync(CancellationToken cancellationToken)
    {
        using var scanCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _scanCancellation = scanCancellation;
        var scanToken = scanCancellation.Token;
        IsScanning = true;
        try
        {
            IsFindingFolders = _isDeep;
            var rows = new List<BatchRowViewModel>();
            foreach (var root in _roots)
            {
                var targets = await Task.Run(() => _isDeep ? BatchRowSource.GetDeepRows(root, scanToken) : BatchRowSource.GetDirectRows(root), scanToken);
                rows.AddRange(targets.Select(target => new BatchRowViewModel(target, root, _services)));
            }
            IsFindingFolders = false;
            SetRows(rows);

            using var throttle = new SemaphoreSlim(ParallelSearches);
            await Task.WhenAll(rows.Select(async row =>
            {
                await throttle.WaitAsync(scanToken);
                try
                {
                    await row.ScanAsync(scanToken);
                    SearchedCount++;
                }
                finally
                {
                    throttle.Release();
                }
            }));
        }
        catch (OperationCanceledException)
        {
            foreach (var row in _rows)
            {
                row.MarkNotSearched();
            }
        }
        finally
        {
            _scanCancellation = null;
            IsFindingFolders = false;
            IsScanning = false;
            // Rebuilding would drop the selected row, so it only happens when a filter needs the icons just found.
            if (IsFiltered)
            {
                RebuildVisibleRows();
            }
            UpdateCounts();
        }
    }

    [RelayCommand]
    private void CancelScan()
    {
        _scanCancellation?.Cancel();
    }

    /// <summary>Also while the scan goes on: Apply takes the rows ticked so far, and the rest stay to be applied later.</summary>
    private bool CanApply => !IsBusy && ApplyCount > 0;

    [RelayCommand(CanExecute = nameof(CanApply))]
    private Task ApplyAsync()
    {
        var changes = _shownRows
            .Where(row => row.IsChecked && row.ChosenSource is not null)
            .Select(row => row.CreateSetChange())
            .ToList();
        return RunChangesAsync(changes);
    }

    private bool CanRemove => !IsBusy && RemoveCount > 0;

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private async Task RemoveAsync()
    {
        var changes = _shownRows
            .Where(row => row.IsChecked && row.State.HasCustomIcon)
            .Select(row => row.CreateRemoveChange())
            .ToList();
        var title = Strings.Count(changes.Count, "RemoveConfirmTitleSingle", "RemoveConfirmTitle");
        if (await _dialogs.ConfirmAsync(title, Strings.Get("RemoveConfirmMessage"), Strings.Get("RemoveConfirmButton")))
        {
            await RunChangesAsync(changes);
        }
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

    /// <summary>Unticks every shown row when any is ticked, otherwise ticks every shown row that can be.</summary>
    [RelayCommand]
    private void ToggleSelectAll()
    {
        var isAnyChecked = _shownRows.Exists(row => row.IsChecked);
        foreach (var row in _shownRows.Where(row => row.CanCheck))
        {
            row.IsChecked = !isAnyChecked;
        }
        // The box ticked or unticked itself when clicked; this puts it back in line with the rows.
        OnPropertyChanged(nameof(SelectAllState));
    }

    partial void OnHideFoldersWithIconChanged(bool value)
    {
        RebuildVisibleRows();
    }

    partial void OnHideFoldersWithOnlyPicturesChanged(bool value)
    {
        RebuildVisibleRows();
    }

    partial void OnHideFoldersWithoutMatchChanged(bool value)
    {
        RebuildVisibleRows();
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
        await Task.WhenAll(outcomes
            .Where(outcome => _rowsByPath.ContainsKey(outcome.Change.ItemPath))
            .Select(outcome => _rowsByPath[outcome.Change.ItemPath].ShowOutcomeAsync(outcome)));
        DisplayText.ShowOutcomes(Status, outcomes, RetryAsAdminCommand, wasElevated);
        if (IsFiltered)
        {
            RebuildVisibleRows();
        }
        UpdateCounts();
    }

    private void SetRows(List<BatchRowViewModel> rows)
    {
        _rows = rows;
        _rowsByPath = rows.GroupBy(row => row.ItemPath, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            row.PropertyChanged += OnRowPropertyChanged;
            row.ShortcutFilterChanged += OnShortcutFilterChanged;
        }
        OnPropertyChanged(nameof(ProgressText));
        RebuildVisibleRows();
        UpdateCounts();
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(BatchRowViewModel.IsChecked) or nameof(BatchRowViewModel.ChosenSource) or nameof(BatchRowViewModel.State))
        {
            UpdateCounts();
        }
    }

    private async void OnShortcutFilterChanged(object? sender, EventArgs eventArgs)
    {
        await Task.WhenAll(_rows.Select(row => row.RefreshShortcutFilterAsync()));
        // A folder whose only icon besides pictures came from a shortcut may now count as one with only pictures, or none.
        if (IsFiltered && !IsScanning)
        {
            RebuildVisibleRows();
        }
    }

    private void RebuildVisibleRows()
    {
        _shownRows = _rows.Where(IsShown).ToList();
        if (IsGrouped)
        {
            VisibleGroups.Clear();
            foreach (var group in _shownRows.GroupBy(row => row.GroupName))
            {
                VisibleGroups.Add(new BatchRowGroup(group.Key, group));
            }
        }
        else
        {
            VisibleRows.Clear();
            foreach (var row in _shownRows)
            {
                VisibleRows.Add(row);
            }
        }
        OnPropertyChanged(nameof(IsEmpty));
        UpdateCounts();
    }

    private bool IsShown(BatchRowViewModel row)
    {
        return !(HideFoldersWithIcon && !row.State.NeedsIcon)
            && !(HideFoldersWithOnlyPictures && row.HasOnlyPictures)
            && !(HideFoldersWithoutMatch && row.HasNoMatch);
    }

    private void UpdateCounts()
    {
        ApplyCount = _shownRows.Count(row => row.IsChecked && row.ChosenSource is not null);
        RemoveCount = _shownRows.Count(row => row.IsChecked && row.State.HasCustomIcon);
        var withIconCount = _rows.Count(row => row.State.HasCustomIcon);
        var hiddenCount = _rows.Count - _shownRows.Count;
        SummaryText = hiddenCount == 0 ? Strings.Format("BatchSummary", _rows.Count, withIconCount) : Strings.Format("BatchSummaryHidden", _rows.Count, withIconCount, hiddenCount);
        OnPropertyChanged(nameof(SelectAllState));
        OnPropertyChanged(nameof(CanSelectAll));
    }
}
