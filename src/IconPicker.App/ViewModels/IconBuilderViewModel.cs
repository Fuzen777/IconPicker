using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IconPicker.App.Services;
using IconPicker.Core.Builder;
using IconPicker.Core.Candidates;
using IconPicker.Core.Icons;
using IconPicker.Core.Library;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;

namespace IconPicker.App.ViewModels;

/// <summary>What the builder's settings column shows.</summary>
public enum BuilderPane
{
    Settings,

    /// <summary>The built-in icons, to pick a glyph from.</summary>
    BuiltInIcons,

    /// <summary>The icons of a browsed .exe or .dll holding several.</summary>
    ModuleIcons,
}

/// <summary>
/// The icon builder, shared by the My icons tab and the dialog opened from a folder's picker: a glyph (a built-in icon, an SVG or any icon the app can use), its color, the shape behind it and its size, with a live preview.
/// </summary>
public sealed partial class IconBuilderViewModel : ObservableObject
{
    public const int LargePreviewSize = 256;

    private const int PreviewDelayMilliseconds = 150;
    private const int RecentColorCount = 8;
    private const string BuiltInIconColor = "#FFFFFF";

    private static readonly int[] SmallPreviewSizes = [48, 32, 16];

    private readonly MyIconsStore _store;
    private readonly SettingsStore _settings;
    private readonly PickerService _pickers;
    private readonly ThumbnailLoader _thumbnailLoader;
    private readonly BuiltIconCache _builtIconCache;
    private readonly FluentIconLibrary _library;
    private CancellationTokenSource? _previewCancellation;
    private bool _isLoading;
    private bool _isNameChangedByHand;
    private bool _isScaleChangedByHand;

    internal IconBuilderViewModel(IServiceProvider services, bool isForFolder)
    {
        _store = services.GetRequiredService<MyIconsStore>();
        _settings = services.GetRequiredService<SettingsStore>();
        _pickers = services.GetRequiredService<PickerService>();
        _thumbnailLoader = services.GetRequiredService<ThumbnailLoader>();
        _builtIconCache = services.GetRequiredService<BuiltIconCache>();
        _library = services.GetRequiredService<FluentIconLibrary>();
        IsForFolder = isForFolder;
        BuiltInList = new IconListViewModel(services, showsSavedIcons: false);
        BuiltInList.BuiltInIconPicked += (_, source) => LoadBuiltIn(source);
        Reset();
    }

    /// <summary>Opened from a folder's picker, which adds Use for this folder and Save to My icons.</summary>
    public bool IsForFolder { get; }

    public IconListViewModel BuiltInList { get; }

    public ObservableCollection<CandidateViewModel> ModuleIcons { get; } = [];

    /// <summary>Preset swatches: Windows' accent palette, gray, black and white.</summary>
    public IReadOnlyList<ColorSwatchViewModel> PresetColors { get; } = new[] { "#0078D4", "#0099BC", "#00B294", "#107C10", "#FFB900", "#F7630C", "#E81123", "#E3008C", "#8764B8", "#767676", "#000000", "#FFFFFF" }
        .Select(hexColor => new ColorSwatchViewModel(ColorText.FromHex(hexColor, default)))
        .ToList();

    /// <summary>The last colors used on a saved or applied icon, newest first.</summary>
    public IReadOnlyList<ColorSwatchViewModel> RecentColors => _settings.Current.RecentColors.Select(hexColor => new ColorSwatchViewModel(ColorText.FromHex(hexColor, default))).ToList();

    public bool HasRecentColors => _settings.Current.RecentColors.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSource), nameof(CanRecolor), nameof(ShowsIconColor), nameof(HasSeveralColors), nameof(SourceText))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial GlyphSource? Source { get; private set; }

    /// <summary>The saved icon being edited, or null for a new icon.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SaveToMyIconsText))]
    public partial SavedIcon? EditedIcon { get; private set; }

    [ObservableProperty]
    public partial string Name { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsIconColor))]
    public partial bool IsRecolored { get; set; }

    [ObservableProperty]
    public partial Color IconColor { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Shape), nameof(UsesBackgroundColor))]
    public partial int ShapeIndex { get; set; }

    [ObservableProperty]
    public partial Color BackgroundColor { get; set; }

    /// <summary>How much of the icon the glyph fills, in percent.</summary>
    [ObservableProperty]
    public partial double IconSizePercent { get; set; }

    [ObservableProperty]
    public partial bool IsSaveToMyIconsChecked { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSettingsPane), nameof(IsBuiltInIconsPane), nameof(IsModuleIconsPane))]
    public partial BuilderPane Pane { get; private set; }

    [ObservableProperty]
    public partial string? ModuleName { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoPreview))]
    public partial ImageSource? LargePreview { get; private set; }

    [ObservableProperty]
    public partial ImageSource? Preview48 { get; private set; }

    [ObservableProperty]
    public partial ImageSource? Preview32 { get; private set; }

    [ObservableProperty]
    public partial ImageSource? Preview16 { get; private set; }

    /// <summary>Why the preview is empty: the file draws nothing, or can't be read.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblem))]
    public partial string? ProblemText { get; private set; }

    /// <summary>Changed since it was opened or last saved. Never while there is no icon: there is nothing to lose.</summary>
    [ObservableProperty]
    public partial bool IsDirty { get; private set; }

    public bool HasSource => Source is not null;

    public bool HasNoPreview => LargePreview is null;

    public bool HasProblem => ProblemText is not null;

    public bool CanRecolor => Source?.Glyph.CanRecolor == true;

    public bool ShowsIconColor => CanRecolor && IsRecolored;

    /// <summary>An SVG in several colors keeps them; tinting pictures and executables' icons isn't offered at all.</summary>
    public bool HasSeveralColors => Source?.Glyph is VectorGlyph { CanRecolor: false };

    public IconShape Shape => (IconShape)Math.Clamp(ShapeIndex, 0, (int)IconShape.Folder);

    public bool UsesBackgroundColor => CurrentStyle.UsesBackgroundColor;

    public bool IsSettingsPane => Pane == BuilderPane.Settings;

    public bool IsBuiltInIconsPane => Pane == BuilderPane.BuiltInIcons;

    public bool IsModuleIconsPane => Pane == BuilderPane.ModuleIcons;

    /// <summary>Where the glyph comes from: a built-in icon by its own name, even in a saved icon renamed since.</summary>
    public string SourceText => Source is null
        ? Strings.Get("BuilderNoSource")
        : Strings.Format("BuilderSourceText", Source.BuiltInIconId is { } id && _library.Find(id) is { } builtInIcon ? builtInIcon.Name : Source.Name);

    public string SaveToMyIconsText => Strings.Get(EditedIcon is null ? "BuilderSaveToMyIcons" : "BuilderSaveChangesToMyIcons");

    public IconStyle CurrentStyle => new(IsRecolored && CanRecolor ? ColorText.ToHex(IconColor) : null, Shape, ColorText.ToHex(BackgroundColor), IconSizePercent / 100);

    /// <summary>Starts a new icon from nothing.</summary>
    public void Reset()
    {
        Load(null, IconStyle.Default, "", editedIcon: null, opensAsSaved: true);
    }

    /// <summary>Opens a saved icon to edit it.</summary>
    public void LoadSavedIcon(SavedIcon savedIcon)
    {
        Load(savedIcon.LoadGlyphSource(), savedIcon.Recipe.Style, savedIcon.Recipe.Name, savedIcon, opensAsSaved: true);
        if (Source is null)
        {
            ProblemText = Strings.Get("BuilderUnreadableSource");
        }
    }

    /// <summary>Opens a built icon picked earlier for a folder, as it was.</summary>
    public void LoadOrigin(BuiltIconOrigin origin)
    {
        var editedIcon = origin.SavedIconId is { } id ? _store.Find(id) : null;
        Load(origin.Source, origin.Style, origin.Name, editedIcon, opensAsSaved: true);
        if (Source is null)
        {
            ProblemText = Strings.Get("BuilderUnreadableSource");
        }
    }

    /// <summary>
    /// Opens the builder on a source. A built-in icon starts white on an accent-colored rounded square; anything else starts as it is, on no background unless <paramref name="style"/> says otherwise.
    /// </summary>
    public void LoadSource(GlyphSource source, IconStyle? style = null)
    {
        Load(source, style ?? (source.BuiltInIconId is null ? IconStyle.Default : BuiltInStyle()), name: null, editedIcon: null, opensAsSaved: false);
    }

    [RelayCommand]
    private void ShowBuiltInIcons()
    {
        Pane = BuilderPane.BuiltInIcons;
    }

    [RelayCommand]
    private void BackToSettings()
    {
        Pane = BuilderPane.Settings;
        ModuleIcons.Clear();
    }

    /// <summary>Any icon the app can use, or an SVG, from anywhere. An .exe or .dll holding several icons shows them to pick from.</summary>
    [RelayCommand]
    private async Task BrowseAsync()
    {
        var filePath = await _pickers.PickIconSourceAsync();
        if (filePath is null || IconSource.GetKind(filePath) is not { } kind)
        {
            return;
        }
        if (kind == IconSourceKind.Module)
        {
            var moduleIcons = await CandidateFinder.InspectFileAsync(filePath, new DirectoryInfo(Path.GetDirectoryName(filePath)!));
            if (moduleIcons.Count > 1)
            {
                ModuleIcons.Clear();
                foreach (var moduleIcon in moduleIcons)
                {
                    ModuleIcons.Add(new CandidateViewModel(moduleIcon, _thumbnailLoader));
                }
                ModuleName = Path.GetFileName(filePath);
                Pane = BuilderPane.ModuleIcons;
                return;
            }
            if (moduleIcons.Count == 1)
            {
                LoadFile(moduleIcons[0].Source);
                return;
            }
            ProblemText = Strings.Format("BrowseNoIcon", Path.GetFileName(filePath));
            return;
        }
        LoadFile(new IconSource(filePath, kind));
    }

    public void PickModuleIcon(CandidateViewModel moduleIcon)
    {
        BackToSettings();
        LoadFile(moduleIcon.Source);
    }

    /// <summary>Saves the icon to My icons, as a new one or over the one being edited. Null when it can't be drawn.</summary>
    [RelayCommand(CanExecute = nameof(HasSource))]
    private async Task<SavedIcon?> SaveAsync()
    {
        // The look as it was when Save was pressed: the builder stays editable while it draws.
        if (TakeSnapshot() is not { } snapshot || await BuildAsync(snapshot) is not { } builtIcon)
        {
            return null;
        }
        EditedIcon = EditedIcon is null
            ? await _store.SaveAsync(snapshot.Source, snapshot.Style, snapshot.Name, builtIcon)
            : await _store.UpdateAsync(EditedIcon, snapshot.Source, snapshot.Style, snapshot.Name, builtIcon);
        RememberColors(snapshot.Style);
        IsDirty = TakeSnapshot() != snapshot;
        return EditedIcon;
    }

    /// <summary>
    /// The icon as a candidate for the folder: written among the built icons, or saved to My icons first when <see cref="IsSaveToMyIconsChecked"/> is. Null when it can't be drawn.
    /// </summary>
    public async Task<IconCandidate?> CreateCandidateAsync()
    {
        if (TakeSnapshot() is not { } snapshot)
        {
            return null;
        }
        if (IsSaveToMyIconsChecked)
        {
            return await SaveAsync() is { } savedIcon ? await BuiltIconCandidates.FromSavedIconAsync(savedIcon, _builtIconCache, snapshot.Source) : null;
        }
        if (await BuildAsync(snapshot) is not { } builtIcon)
        {
            return null;
        }
        RememberColors(snapshot.Style);
        var iconPath = await _builtIconCache.WriteAsync(builtIcon.IconFile);
        return BuiltIconCandidates.Create(iconPath, new BuiltIconOrigin(snapshot.Source, snapshot.Style, snapshot.Name, SavedIconId: null));
    }

    /// <summary>The saved icon being edited was renamed in the list: the builder follows, unless another name was typed since.</summary>
    public void FollowRename(SavedIcon renamedIcon)
    {
        if (EditedIcon?.Id != renamedIcon.Id)
        {
            return;
        }
        var wasDirty = IsDirty;
        _isLoading = true;
        EditedIcon = renamedIcon;
        if (!_isNameChangedByHand || Name.Trim().Length == 0 || Name.Trim() == renamedIcon.Recipe.Name)
        {
            Name = renamedIcon.Recipe.Name;
        }
        _isLoading = false;
        IsDirty = wasDirty;
    }

    partial void OnNameChanged(string value)
    {
        if (!_isLoading)
        {
            _isNameChangedByHand = true;
            IsDirty = HasSource;
        }
    }

    partial void OnIsRecoloredChanged(bool value)
    {
        OnLookChanged();
    }

    partial void OnIconColorChanged(Color value)
    {
        OnLookChanged();
    }

    partial void OnShapeIndexChanged(int value)
    {
        if (!_isLoading && !_isScaleChangedByHand)
        {
            // Each shape has its own natural size until the slider is moved by hand.
            _isLoading = true;
            IconSizePercent = IconStyle.DefaultScale(Shape) * 100;
            _isLoading = false;
        }
        OnLookChanged();
    }

    partial void OnBackgroundColorChanged(Color value)
    {
        OnLookChanged();
    }

    partial void OnIconSizePercentChanged(double value)
    {
        if (!_isLoading)
        {
            _isScaleChangedByHand = true;
        }
        OnLookChanged();
    }

    private static IconStyle BuiltInStyle()
    {
        return new IconStyle(BuiltInIconColor, IconShape.Rounded, IconStyle.Default.BackgroundColor, IconStyle.DefaultScale(IconShape.Rounded));
    }

    private void LoadBuiltIn(GlyphSource source)
    {
        Pane = BuilderPane.Settings;
        // A built-in icon replacing another keeps the look chosen so far.
        Load(source, Source?.BuiltInIconId is not null || EditedIcon is not null ? CurrentStyle with { IconColor = CurrentStyle.IconColor ?? BuiltInIconColor } : BuiltInStyle(), name: null, EditedIcon, opensAsSaved: false);
    }

    /// <summary>Another source keeps the look chosen so far.</summary>
    private void LoadFile(IconSource iconSource)
    {
        if (GlyphSource.FromFile(iconSource) is not { } source)
        {
            ProblemText = Strings.Format("BuilderUnreadableFile", Path.GetFileName(iconSource.FilePath));
            return;
        }
        Load(source, Source is null && EditedIcon is null ? IconStyle.Default : CurrentStyle, name: null, EditedIcon, opensAsSaved: false);
    }

    /// <param name="opensAsSaved">Opening an icon as it was saved (or a new, empty one) starts with nothing to lose; giving an icon another glyph is a change.</param>
    private void Load(GlyphSource? source, IconStyle style, string? name, SavedIcon? editedIcon, bool opensAsSaved)
    {
        _isLoading = true;
        var keepsTypedName = _isNameChangedByHand && editedIcon == EditedIcon && name is null;
        Source = source;
        EditedIcon = editedIcon;
        ProblemText = null;
        ApplyStyle(style);
        if (!keepsTypedName)
        {
            Name = name ?? source?.Name ?? "";
            // A saved icon's name counts as chosen; a new icon takes its source's name until one is typed.
            _isNameChangedByHand = !string.IsNullOrEmpty(name);
        }
        _isScaleChangedByHand = Math.Abs(style.ClampedScale - IconStyle.DefaultScale(style.Shape)) > 0.001;
        _isLoading = false;
        IsDirty = source is not null && (editedIcon is null || !opensAsSaved);
        SchedulePreview();
    }

    private void ApplyStyle(IconStyle style)
    {
        IsRecolored = style.IconColor is not null;
        IconColor = ColorText.FromHex(style.IconColor ?? BuiltInIconColor, default);
        ShapeIndex = (int)style.Shape;
        BackgroundColor = ColorText.FromHex(style.BackgroundColor, default);
        IconSizePercent = style.ClampedScale * 100;
    }

    private void OnLookChanged()
    {
        if (_isLoading)
        {
            return;
        }
        IsDirty = HasSource;
        SchedulePreview();
    }

    private string GetNameToSave()
    {
        var name = Name.Trim();
        return name.Length > 0 ? name : Source?.Name ?? Strings.Get("BuilderDefaultName");
    }

    private BuilderSnapshot? TakeSnapshot()
    {
        return Source is { } source ? new BuilderSnapshot(source, CurrentStyle, GetNameToSave()) : null;
    }

    /// <summary>The finished icon, or null with a message when it can't be drawn or an SVG draws nothing (a template left uncolored, a sprite sheet).</summary>
    private async Task<BuiltIcon?> BuildAsync(BuilderSnapshot snapshot)
    {
        var glyph = snapshot.Source.Glyph;
        var style = snapshot.Style;
        var builtIcon = await Task.Run(async () =>
        {
            if (glyph is VectorGlyph vectorGlyph && !SvgRenderer.HasVisiblePixels(style.IconColor is { } color && vectorGlyph.CanRecolor ? vectorGlyph.WithColor(color).Svg : vectorGlyph.Svg))
            {
                return null;
            }
            return await IconComposer.BuildAsync(glyph, style);
        });
        if (builtIcon is null)
        {
            ProblemText = Strings.Get("BuilderNothingToDraw");
        }
        return builtIcon;
    }

    /// <summary>The colors of an icon that was saved or used join the recent colors, newest first.</summary>
    private void RememberColors(IconStyle style)
    {
        var usedColors = new[] { style.UsesBackgroundColor ? style.BackgroundColor : null, style.IconColor }.OfType<string>().ToList();
        if (usedColors.Count == 0)
        {
            return;
        }
        _settings.Update(settings =>
        {
            settings.RecentColors = usedColors
                .Concat(settings.RecentColors)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(RecentColorCount)
                .ToList();
        });
        OnPropertyChanged(nameof(RecentColors));
        OnPropertyChanged(nameof(HasRecentColors));
    }

    /// <summary>Redraws the preview off the UI thread once changes pause; a newer change drops an older redraw.</summary>
    private async void SchedulePreview()
    {
        _previewCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _previewCancellation = cancellation;
        if (Source is not { } source)
        {
            ShowPreview(null, null, null, null);
            return;
        }
        try
        {
            await Task.Delay(PreviewDelayMilliseconds, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        var glyph = source.Glyph;
        var style = CurrentStyle;
        var renders = await Task.Run(async () =>
        {
            var sizes = SmallPreviewSizes.Prepend(LargePreviewSize).ToArray();
            var pixels = new byte[]?[sizes.Length];
            for (var index = 0; index < sizes.Length && !cancellation.IsCancellationRequested; index++)
            {
                pixels[index] = await IconComposer.ComposeAsync(glyph, style, sizes[index]);
            }
            return pixels;
        });
        if (cancellation.IsCancellationRequested)
        {
            return;
        }
        if (renders[0] is null || (glyph is VectorGlyph vectorGlyph && !SvgRenderer.HasVisiblePixels(style.IconColor is { } color ? vectorGlyph.WithColor(color).Svg : vectorGlyph.Svg)))
        {
            ProblemText = Strings.Get("BuilderNothingToDraw");
            ShowPreview(null, null, null, null);
            return;
        }
        ProblemText = null;
        ShowPreview(await ToImageAsync(renders[0], LargePreviewSize), await ToImageAsync(renders[1], 48), await ToImageAsync(renders[2], 32), await ToImageAsync(renders[3], 16));
    }

    private void ShowPreview(ImageSource? large, ImageSource? at48, ImageSource? at32, ImageSource? at16)
    {
        LargePreview = large;
        Preview48 = at48;
        Preview32 = at32;
        Preview16 = at16;
    }

    /// <summary>What a save or a use works from, taken when it starts.</summary>
    private sealed record BuilderSnapshot(GlyphSource Source, IconStyle Style, string Name);

    private static async Task<ImageSource?> ToImageAsync(byte[]? pixels, int size)
    {
        if (pixels is null)
        {
            return null;
        }
        var image = new SoftwareBitmapSource();
        await image.SetBitmapAsync(IconPreviewRenderer.ToSoftwareBitmap(pixels, size));
        return image;
    }
}
