using CommunityToolkit.Mvvm.ComponentModel;
using IconPicker.App.Services;
using IconPicker.Core.Applying;
using IconPicker.Core.Candidates;
using IconPicker.Core.Detection;
using IconPicker.Core.Folders;
using IconPicker.Core.Icons;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Media;

namespace IconPicker.App.ViewModels;

public enum BatchRowStatus
{
    Searching,
    Ready,
    NoCandidate,
    NotSearched,
}

/// <summary>One folder in batch mode: its current icon, the predicted one, and whether it is ticked.</summary>
public sealed partial class BatchRowViewModel : ObservableObject
{
    /// <summary>Physical pixels: sharp at 32 px on screens scaled up to 200%.</summary>
    private const int RowIconSize = 64;
    private const double DimmedOpacity = 0.4;

    private readonly IServiceProvider _services;
    private readonly ThumbnailLoader _thumbnailLoader;
    private readonly SettingsStore _settings;
    private IReadOnlyList<IconCandidate> _candidates = [];
    private IconPickerViewModel? _picker;
    private bool _isCheckingQuietly;
    private string? _outcomeText;

    /// <summary>Applied or removed: the row keeps the tick it was left with, even when its search finishes afterwards.</summary>
    private bool _hasOutcome;

    /// <summary>Counts reads of the folder's icon, so a slower earlier read (the search's) never overwrites a later one (after applying).</summary>
    private int _stateReadCount;
    private CancellationTokenSource? _searchCancellation;

    internal BatchRowViewModel(BatchRowTarget target, DirectoryInfo root, IServiceProvider services)
    {
        Folder = target.Folder;
        SearchNames = target.SearchNames;
        ShortcutPath = target.Shortcut?.FullName;
        _services = services;
        _thumbnailLoader = services.GetRequiredService<ThumbnailLoader>();
        _settings = services.GetRequiredService<SettingsStore>();
        Title = ShortcutPath is null ? Path.GetRelativePath(root.FullName, Folder.FullName) : Strings.Format("RowShortcutTitle", target.Name);
        GroupName = DisplayText.FolderName(root.FullName);
    }

    public DirectoryInfo Folder { get; }

    public IReadOnlyList<string> SearchNames { get; }

    /// <summary>For a shortcut to a folder, the shortcut, which gets the icon too; <see cref="Folder"/> is the folder it opens.</summary>
    public string? ShortcutPath { get; }

    /// <summary>What the row stands for: the shortcut, or the folder.</summary>
    public string ItemPath => ShortcutPath ?? Folder.FullName;

    /// <summary>The folder's path below the batch's folder: its name for direct subfolders, more for the deep search, and the shortcut's name for a shortcut.</summary>
    public string Title { get; }

    /// <summary>The library the row belongs to, for "Rescan all".</summary>
    public string GroupName { get; }

    /// <summary>The user turned "Include shortcuts" on or off in this row's picker.</summary>
    public event EventHandler? ShortcutFilterChanged;

    /// <summary>The user ticked, unticked or picked an icon here.</summary>
    public bool IsUserChanged { get; private set; }

    /// <summary>Leaving would lose something here: the row is ticked by hand, or for an icon picked by hand, and Apply or Remove would act on it. A row only unticked loses nothing.</summary>
    public bool HasUnappliedChange => IsUserChanged && IsChecked && CanCheck;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailText), nameof(IsSearching))]
    public partial BatchRowStatus Status { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCheck), nameof(PredictionOpacity), nameof(CurrentIconDescription))]
    public partial FolderIconState State { get; private set; } = FolderIconState.NoIcon;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PredictionOpacity))]
    public partial bool IsChecked { get; set; }

    [ObservableProperty]
    public partial ImageSource? CurrentIcon { get; private set; }

    [ObservableProperty]
    public partial ImageSource? PredictedIcon { get; private set; }

    public bool IsSearching => Status == BatchRowStatus.Searching;

    /// <summary>The icon Apply would set: the one picked by hand, otherwise the best candidate.</summary>
    public IconSource? ChosenSource => ChosenCandidate?.Source;

    private IconCandidate? ChosenCandidate => _picker is not null ? _picker.Choice?.Candidate : ShownCandidates.FirstOrDefault();

    /// <summary>The candidates "Include shortcuts" lets through.</summary>
    private IEnumerable<IconCandidate> ShownCandidates => _candidates.Where(candidate => _settings.Current.IncludeShortcuts || !candidate.IsShortcut);

    /// <summary>Ticking needs something to do: an icon to set, or one to remove.</summary>
    public bool CanCheck => ChosenSource is not null || State.HasCustomIcon;

    /// <summary>Searched, and every icon found is a picture or an SVG, with nothing picked by hand: no exe, .ico or shortcut icon, which usually means no app inside.</summary>
    public bool HasOnlyPictures => Status is BatchRowStatus.Ready && _picker?.HasUserChoice != true && ShownCandidates.Any() && ShownCandidates.All(candidate => candidate.Source.IsConverted);

    /// <summary>Searched, and no icon was found or picked.</summary>
    public bool HasNoMatch => Status == BatchRowStatus.NoCandidate;

    /// <summary>A folder keeping its own icon shows the prediction faded.</summary>
    public double PredictionOpacity => State is { HasCustomIcon: true, NeedsIcon: false } && !IsChecked ? DimmedOpacity : 1.0;

    public string CurrentIconDescription => DisplayText.DescribeIconState(State, Folder.FullName);

    /// <summary>Where a shortcut row's icon goes, shown when hovering its name.</summary>
    public string? TitleToolTip => ShortcutPath is null ? null : Strings.Format("RowShortcutToolTip", Folder.FullName);

    public bool HasVersionFolderWarning => ChosenSource is { } source && IconTargetResolver.LinksIntoVersionFolder(Folder, source, _settings.Current.IconTarget);

    public string DetailText
    {
        get
        {
            if (_outcomeText is not null)
            {
                return _outcomeText;
            }
            return Status switch
            {
                BatchRowStatus.Searching => Strings.Get("RowSearching"),
                BatchRowStatus.NoCandidate when ChosenSource is null => Strings.Get("RowNoCandidate"),
                BatchRowStatus.NotSearched => Strings.Get("RowNotSearched"),
                // A shortcut's icon is named after the shortcut, not the file it comes from.
                // A built icon is named after itself, not its file among the app's data.
                _ => ChosenCandidate is { } candidate ? Strings.Format("RowSource", candidate.Built?.Name ?? candidate.RelativePath) : "",
            };
        }
    }

    /// <summary>The side panel's picker, made the first time the row is selected.</summary>
    public IconPickerViewModel Picker
    {
        get
        {
            if (_picker is null)
            {
                _picker = new IconPickerViewModel(Folder, _services.GetRequiredService<PickerService>(), _thumbnailLoader, _settings, _services.GetRequiredService<IconBuilderService>());
                _picker.SetCandidates(_candidates);
                _picker.IsLoading = IsSearching;
                _picker.ChoiceChangedByUser += OnChoiceChangedByUser;
                _picker.ShortcutFilterChanged += (sender, eventArgs) => ShortcutFilterChanged?.Invoke(this, eventArgs);
            }
            return _picker;
        }
    }

    internal async Task ScanAsync(CancellationToken cancellationToken)
    {
        await RefreshStateAsync(cancellationToken);
        if (await SearchCandidatesAsync(cancellationToken))
        {
            _picker?.SetCandidates(_candidates);
            UpdateSearchedStatus();
        }
        else
        {
            Status = BatchRowStatus.Ready;
            _picker?.StopSearch();
        }
        RaiseChoiceChanged();
        await Task.WhenAll(RefreshCurrentIconAsync(), RefreshPredictedIconAsync());
    }

    /// <summary>
    /// Looks for the folder's candidates. Returns false when an icon was picked by hand before or during the search, which stops it: its results would only replace the pick.
    /// </summary>
    private async Task<bool> SearchCandidatesAsync(CancellationToken cancellationToken)
    {
        if (_picker?.HasUserChoice == true)
        {
            return false;
        }
        using var searchCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _searchCancellation = searchCancellation;
        try
        {
            _candidates = await CandidateFinder.FindAsync(Folder, SearchNames, searchCancellation.Token);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        finally
        {
            _searchCancellation = null;
        }
    }

    /// <summary>Follows "Include shortcuts" after it changed: the prediction, and the starting tick unless the user changed it.</summary>
    internal async Task RefreshShortcutFilterAsync()
    {
        _picker?.SyncShortcutFilter();
        if (Status is BatchRowStatus.Ready or BatchRowStatus.NoCandidate)
        {
            UpdateSearchedStatus();
        }
        RaiseChoiceChanged();
        await RefreshPredictedIconAsync();
    }

    internal IconChange CreateSetChange()
    {
        return new IconChange(Folder.FullName, IconChangeKind.Set, ChosenSource, ShortcutPath);
    }

    internal IconChange CreateRemoveChange()
    {
        return new IconChange(Folder.FullName, IconChangeKind.Remove, ShortcutPath: ShortcutPath);
    }

    internal void MarkNotSearched()
    {
        if (IsSearching)
        {
            Status = BatchRowStatus.NotSearched;
            if (_picker is not null)
            {
                _picker.IsLoading = false;
            }
        }
    }

    /// <summary>Shows what applying did to this folder and refreshes its icon.</summary>
    internal async Task ShowOutcomeAsync(IconChangeOutcome outcome)
    {
        _outcomeText = outcome.Result switch
        {
            IconChangeResult.Applied => Strings.Get(outcome.Change.Kind == IconChangeKind.Set ? "RowIconSet" : "RowIconRemoved"),
            IconChangeResult.Unchanged => null,
            IconChangeResult.Refused => Strings.Get("RowNeedsAdmin"),
            _ => Strings.Format("RowFailed", DisplayText.DescribeFailure(outcome)),
        };
        if (outcome.Result is IconChangeResult.Applied or IconChangeResult.Unchanged)
        {
            // An icon picked by hand joins "Single"'s recent icons; predictions applied by the hundred would only crowd them out.
            if (outcome.Change.Kind == IconChangeKind.Set && _picker is { HasUserChoice: true, Choice: { } choice } && choice.Source == outcome.Change.Source)
            {
                _ = _services.GetRequiredService<RecentIconRecorder>().RecordAsync(choice.Candidate);
            }
            IsUserChanged = false;
            _hasOutcome = true;
            SetCheckedQuietly(false);
            await RefreshStateAsync(CancellationToken.None);
            await RefreshCurrentIconAsync();
        }
        OnPropertyChanged(nameof(DetailText));
    }

    partial void OnIsCheckedChanged(bool value)
    {
        if (!_isCheckingQuietly)
        {
            IsUserChanged = true;
        }
    }

    private void UpdateSearchedStatus()
    {
        var hasCandidate = ShownCandidates.Any();
        Status = hasCandidate || ChosenSource is not null ? BatchRowStatus.Ready : BatchRowStatus.NoCandidate;
        // Folders without a working icon are proposed; folders keeping their own are only shown. A tick changed by hand meanwhile stays.
        if (!IsUserChanged && !_hasOutcome)
        {
            SetCheckedQuietly(State.NeedsIcon && hasCandidate);
        }
    }

    private void SetCheckedQuietly(bool isChecked)
    {
        _isCheckingQuietly = true;
        IsChecked = isChecked;
        _isCheckingQuietly = false;
    }

    private async void OnChoiceChangedByUser(object? sender, EventArgs eventArgs)
    {
        _searchCancellation?.Cancel();
        // Picking an icon means wanting it.
        IsUserChanged = true;
        _outcomeText = null;
        SetCheckedQuietly(true);
        RaiseChoiceChanged();
        await RefreshPredictedIconAsync();
    }

    private void RaiseChoiceChanged()
    {
        OnPropertyChanged(nameof(ChosenSource));
        OnPropertyChanged(nameof(CanCheck));
        OnPropertyChanged(nameof(DetailText));
        OnPropertyChanged(nameof(HasVersionFolderWarning));
    }

    private async Task RefreshStateAsync(CancellationToken cancellationToken)
    {
        var readNumber = ++_stateReadCount;
        var folder = Folder;
        var shortcutPath = ShortcutPath;
        var state = await Task.Run(() => FolderIconState.Read(folder, shortcutPath), cancellationToken);
        if (readNumber == _stateReadCount)
        {
            State = state;
        }
    }

    private async Task RefreshCurrentIconAsync()
    {
        var state = State;
        var currentIcon = await _thumbnailLoader.LoadAsync(() => IconPreviewRenderer.RenderFolderIconAsync(state, RowIconSize));
        // Applying while the row was searched may have changed the icon meanwhile.
        if (state == State)
        {
            CurrentIcon = currentIcon;
        }
    }

    private async Task RefreshPredictedIconAsync()
    {
        var source = ChosenSource;
        var predictedIcon = source is null ? null : await _thumbnailLoader.LoadAsync(() => IconPreviewRenderer.RenderAsync(source, RowIconSize));
        // A quicker later pick may have finished first.
        if (source == ChosenSource)
        {
            PredictedIcon = predictedIcon;
        }
    }
}
