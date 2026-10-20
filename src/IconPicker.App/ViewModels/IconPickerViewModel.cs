using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IconPicker.App.Services;
using IconPicker.Core.Builder;
using IconPicker.Core.Candidates;
using IconPicker.Core.Folders;

namespace IconPicker.App.ViewModels;

/// <summary>The ranked icon grid with Browse, Icons and Customize, shared by single mode and batch mode's side panel.</summary>
public sealed partial class IconPickerViewModel : ObservableObject
{
    public const int TopCandidateCount = 50;

    private readonly DirectoryInfo _folder;
    private readonly PickerService _pickers;
    private readonly ThumbnailLoader _thumbnailLoader;
    private readonly SettingsStore _settings;
    private readonly IconBuilderService _iconBuilder;
    private List<CandidateViewModel> _folderCandidates = [];
    private List<CandidateViewModel> _moduleCandidates = [];
    private bool _isSyncingShortcutFilter;

    internal IconPickerViewModel(DirectoryInfo folder, PickerService pickers, ThumbnailLoader thumbnailLoader, SettingsStore settings, IconBuilderService iconBuilder)
    {
        _folder = folder;
        _pickers = pickers;
        _thumbnailLoader = thumbnailLoader;
        _settings = settings;
        _iconBuilder = iconBuilder;
        SyncShortcutFilter();
    }

    /// <summary>The user picked an icon, in the grid or with Browse.</summary>
    public event EventHandler? ChoiceChangedByUser;

    /// <summary>The visible tiles were replaced, so the grid's selection has to be put back.</summary>
    public event EventHandler? CandidatesRefreshed;

    /// <summary>The user turned "Include shortcuts" on or off, which applies to every picker.</summary>
    public event EventHandler? ShortcutFilterChanged;

    public ObservableCollection<CandidateViewModel> VisibleCandidates { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CustomizeCommand))]
    public partial CandidateViewModel? Choice { get; private set; }

    /// <summary>An icon was picked by hand (in the grid or with Browse), so the prediction no longer applies.</summary>
    public bool HasUserChoice { get; private set; }

    [ObservableProperty]
    public partial bool IsSearchStopped { get; private set; }

    [ObservableProperty]
    public partial bool IsProblemOpen { get; set; }

    [ObservableProperty]
    public partial string? ProblemText { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBrowsingModule))]
    public partial string? BrowsedModuleName { get; private set; }

    /// <summary>Whether icons shown by shortcuts are listed and predicted. Saved in settings, so it applies to every folder.</summary>
    [ObservableProperty]
    public partial bool IncludeShortcuts { get; set; }

    private bool IsShowingAll { get; set; }

    /// <summary>The folder's candidates the shortcut filter lets through. An icon picked by hand stays, even from a shortcut.</summary>
    private List<CandidateViewModel> ShownFolderCandidates => _folderCandidates.Where(candidate => PassesShortcutFilter(candidate) || (HasUserChoice && ReferenceEquals(candidate, Choice))).ToList();

    public bool IsBrowsingModule => BrowsedModuleName is not null;

    public bool HasMore => !IsBrowsingModule && !IsShowingAll && ShownFolderCandidates.Count > TopCandidateCount;

    public string ShowAllText => Strings.Format("ShowAllButton", ShownFolderCandidates.Count);

    public bool IsEmpty => !IsLoading && VisibleCandidates.Count == 0;

    public string HeaderText => IsBrowsingModule
        ? Strings.Format("ModuleIconsHeader", BrowsedModuleName, _moduleCandidates.Count)
        : Strings.Format("CandidatesHeader", ShownFolderCandidates.Count);

    /// <summary>
    /// Fills the grid with the folder's candidates and preselects the best one; that is a prediction, not a user choice. An icon already picked by hand stays picked, at the top.
    /// </summary>
    public void SetCandidates(IReadOnlyList<IconCandidate> candidates)
    {
        var foundCandidates = candidates.Select(candidate => new CandidateViewModel(candidate, _thumbnailLoader)).ToList();
        if (HasUserChoice && Choice is not null)
        {
            // A search can finish just as an icon is picked by hand; the pick wins.
            foundCandidates.RemoveAll(found => found.Source == Choice.Source);
            foundCandidates.Insert(0, Choice);
        }
        else
        {
            Choice = foundCandidates.FirstOrDefault(PassesShortcutFilter);
        }
        _folderCandidates = foundCandidates;
        IsLoading = false;
        RefreshVisibleCandidates();
    }

    /// <summary>The folder's search was stopped because an icon was picked by hand: the grid keeps what was picked.</summary>
    public void StopSearch()
    {
        IsLoading = false;
        IsSearchStopped = true;
        RefreshVisibleCandidates();
    }

    /// <summary>Picks an icon by hand, keeping it at the top of the list so it stays visible.</summary>
    public void Choose(CandidateViewModel candidate)
    {
        if (ReferenceEquals(candidate, Choice))
        {
            return;
        }
        if (IsBrowsingModule)
        {
            KeepAtTop(candidate);
        }
        Choice = candidate;
        HasUserChoice = true;
        ChoiceChangedByUser?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Files dropped on the picker: every icon each one offers (all of an executable's, the one a shortcut shows) joins the top of the grid, in order, and the first is picked.
    /// An SVG comes as a tile like any other; Customize colors it.
    /// </summary>
    public async Task AddFilesAsync(IReadOnlyList<string> filePaths)
    {
        IsProblemOpen = false;
        var addedCandidates = new List<CandidateViewModel>();
        var filesWithoutIcon = new List<string>();
        foreach (var filePath in filePaths)
        {
            var candidates = await CandidateFinder.InspectFileAsync(filePath, _folder);
            if (candidates.Count == 0)
            {
                filesWithoutIcon.Add(Path.GetFileName(filePath));
            }
            addedCandidates.AddRange(candidates.Select(candidate => new CandidateViewModel(candidate, _thumbnailLoader)));
        }
        if (filesWithoutIcon.Count > 0)
        {
            ShowProblem(Strings.Format("BrowseNoIcon", string.Join(", ", filesWithoutIcon)));
        }
        if (addedCandidates.Count == 0)
        {
            return;
        }
        foreach (var candidate in Enumerable.Reverse(addedCandidates))
        {
            KeepAtTop(candidate);
        }
        BrowsedModuleName = null;
        _moduleCandidates = [];
        RefreshVisibleCandidates();
        Choose(addedCandidates[0]);
    }

    /// <summary>Picks one of "Single"'s recent icons, as Browse would.</summary>
    internal void PickRecentIcon(CandidateViewModel candidate)
    {
        IsProblemOpen = false;
        PickOutsideCandidate(candidate);
    }

    /// <summary>Takes up "Include shortcuts" as last saved, after another picker changed it.</summary>
    internal void SyncShortcutFilter()
    {
        _isSyncingShortcutFilter = true;
        IncludeShortcuts = _settings.Current.IncludeShortcuts;
        _isSyncingShortcutFilter = false;
    }

    partial void OnIncludeShortcutsChanged(bool value)
    {
        // The prediction follows the filter; an icon picked by hand stays.
        if (!HasUserChoice)
        {
            Choice = _folderCandidates.FirstOrDefault(PassesShortcutFilter);
        }
        RefreshVisibleCandidates();
        if (!_isSyncingShortcutFilter)
        {
            _settings.Update(settings => settings.IncludeShortcuts = value);
            ShortcutFilterChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private void ShowAll()
    {
        IsShowingAll = true;
        RefreshVisibleCandidates();
    }

    [RelayCommand]
    private void BackToCandidates()
    {
        BrowsedModuleName = null;
        _moduleCandidates = [];
        RefreshVisibleCandidates();
    }

    [RelayCommand]
    private async Task BrowseAsync()
    {
        var filePath = await _pickers.PickIconSourceAsync();
        if (filePath is null)
        {
            return;
        }
        IsProblemOpen = false;
        // An SVG opens the builder, to color it and put it on a background before using it.
        if (IconSource.GetKind(filePath) == IconSourceKind.Vector)
        {
            if (GlyphSource.FromFile(new IconSource(filePath, IconSourceKind.Vector)) is not { } source)
            {
                ShowProblem(Strings.Format("BrowseNoIcon", Path.GetFileName(filePath)));
                return;
            }
            PickBuiltIcon(await _iconBuilder.BuildAsync(source));
            return;
        }
        var candidates = (await CandidateFinder.InspectFileAsync(filePath, _folder))
            .Select(candidate => new CandidateViewModel(candidate, _thumbnailLoader))
            .ToList();
        if (candidates.Count == 0)
        {
            ShowProblem(Strings.Format("BrowseNoIcon", Path.GetFileName(filePath)));
            return;
        }
        // An .exe or .dll with several icons opens its own grid to pick from.
        if (candidates.Count > 1)
        {
            _moduleCandidates = candidates;
            BrowsedModuleName = Path.GetFileName(filePath);
            RefreshVisibleCandidates();
            return;
        }
        PickOutsideCandidate(candidates[0]);
    }

    /// <summary>The saved icons and the built-in ones: a saved icon is used as it is, a built-in one goes through the builder.</summary>
    [RelayCommand]
    private async Task PickIconAsync()
    {
        IsProblemOpen = false;
        PickBuiltIcon(await _iconBuilder.PickIconAsync());
    }

    /// <summary>Opens the picked icon in the builder: an icon built earlier as it was, anything else on a background.</summary>
    [RelayCommand(CanExecute = nameof(CanCustomize))]
    private async Task CustomizeAsync()
    {
        if (Choice is not { } choice)
        {
            return;
        }
        IsProblemOpen = false;
        if (choice.Candidate.Built is { } origin)
        {
            PickBuiltIcon(await _iconBuilder.EditAsync(origin));
            return;
        }
        if (GlyphSource.FromFile(choice.Source) is not { } source)
        {
            ShowProblem(Strings.Format("BrowseNoIcon", choice.FileName));
            return;
        }
        // A recent icon's file is named after its hash: the builder names it after what it was.
        if (choice.Candidate.Recent is { } recentIcon)
        {
            source = source with { Name = recentIcon.IsBuilt ? recentIcon.Name : Path.GetFileNameWithoutExtension(recentIcon.Name) };
        }
        PickBuiltIcon(await _iconBuilder.BuildAsync(source, IconBuilderService.CustomizeStyle));
    }

    private bool CanCustomize()
    {
        return Choice is not null;
    }

    private void PickBuiltIcon(IconCandidate? builtIcon)
    {
        if (builtIcon is not null)
        {
            PickOutsideCandidate(new CandidateViewModel(builtIcon, _thumbnailLoader));
        }
    }

    /// <summary>An icon from outside the folder's search (Browse, Icons, Customize, the recent icons) is kept at the top, in view, and picked.</summary>
    private void PickOutsideCandidate(CandidateViewModel candidate)
    {
        KeepAtTop(candidate);
        BrowsedModuleName = null;
        RefreshVisibleCandidates();
        Choose(candidate);
    }

    private void ShowProblem(string problemText)
    {
        ProblemText = problemText;
        IsProblemOpen = true;
    }

    /// <summary>A shortcut from elsewhere was dropped here on purpose, so it shows even with "Include shortcuts" off.</summary>
    private bool PassesShortcutFilter(CandidateViewModel candidate)
    {
        return IncludeShortcuts || candidate.Candidate.ShortcutPath is not { } shortcutPath || !FileHelpers.IsInsideFolder(shortcutPath, _folder.FullName);
    }

    private void KeepAtTop(CandidateViewModel candidate)
    {
        _folderCandidates.RemoveAll(existing => existing.Source == candidate.Source);
        _folderCandidates.Insert(0, candidate);
    }

    private void RefreshVisibleCandidates()
    {
        IEnumerable<CandidateViewModel> candidatesToShow = IsBrowsingModule ? _moduleCandidates
            : IsShowingAll ? ShownFolderCandidates
            : ShownFolderCandidates.Take(TopCandidateCount);
        VisibleCandidates.Clear();
        foreach (var candidate in candidatesToShow)
        {
            VisibleCandidates.Add(candidate);
        }
        OnPropertyChanged(nameof(HasMore));
        OnPropertyChanged(nameof(ShowAllText));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HeaderText));
        CandidatesRefreshed?.Invoke(this, EventArgs.Empty);
    }
}
