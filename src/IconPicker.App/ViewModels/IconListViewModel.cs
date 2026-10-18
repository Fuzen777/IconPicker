using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using IconPicker.App.Services;
using IconPicker.Core.Builder;
using IconPicker.Core.Candidates;
using IconPicker.Core.Icons;
using IconPicker.Core.Library;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Media;

namespace IconPicker.App.ViewModels;

/// <summary>A saved icon's tile.</summary>
public sealed partial class SavedIconViewModel : ObservableObject
{
    /// <summary>Physical pixels: sharp at 64 px on screens scaled up to 150%.</summary>
    private const int ThumbnailSize = 96;

    private readonly ThumbnailLoader _thumbnailLoader;
    private bool _isThumbnailRequested;

    internal SavedIconViewModel(SavedIcon savedIcon, ThumbnailLoader thumbnailLoader)
    {
        SavedIcon = savedIcon;
        _thumbnailLoader = thumbnailLoader;
    }

    public SavedIcon SavedIcon { get; }

    public string Name => SavedIcon.Recipe.Name;

    /// <summary>Drawn from preview.png on first use; read from the file each time, so an edited icon never shows an old image.</summary>
    public ImageSource? Thumbnail
    {
        get
        {
            if (!_isThumbnailRequested)
            {
                _isThumbnailRequested = true;
                _ = LoadThumbnailAsync();
            }
            return field;
        }
        private set => SetProperty(ref field, value);
    }

    private async Task LoadThumbnailAsync()
    {
        Thumbnail = await _thumbnailLoader.LoadAsync(() => IconPreviewRenderer.RenderAsync(new IconSource(SavedIcon.PreviewPath, IconSourceKind.Image), ThumbnailSize));
    }
}

/// <summary>A built-in icon's tile. Its thumbnail is drawn when the tile comes into view and dropped when it leaves, as the set holds thousands.</summary>
public sealed partial class BuiltInIconViewModel : ObservableObject
{
    /// <summary>Physical pixels for a 32 px tile icon on screens scaled up to 150%.</summary>
    private const int ThumbnailSize = 48;

    private readonly IconListViewModel _list;
    private CancellationTokenSource? _thumbnailCancellation;

    internal BuiltInIconViewModel(BuiltInIcon icon, IconListViewModel list)
    {
        Icon = icon;
        _list = list;
    }

    public BuiltInIcon Icon { get; }

    public string Name => Icon.Name;

    [ObservableProperty]
    public partial ImageSource? Thumbnail { get; private set; }

    /// <summary>Draws the thumbnail in the list's current style and color. Called when the tile comes into view.</summary>
    public async void LoadThumbnail()
    {
        ReleaseThumbnail();
        var cancellation = new CancellationTokenSource();
        _thumbnailCancellation = cancellation;
        var style = _list.Style;
        var glyphColor = _list.GlyphColor;
        var library = _list.Library;
        var thumbnail = await _list.ThumbnailLoader.LoadAsync(() => Task.FromResult(Render(library, Icon.Id, style, glyphColor)), cancellation.Token);
        if (!cancellation.IsCancellationRequested)
        {
            Thumbnail = thumbnail;
        }
    }

    /// <summary>Called when the tile leaves the view: a render still waiting is skipped, and the image is let go.</summary>
    public void ReleaseThumbnail()
    {
        _thumbnailCancellation?.Cancel();
        _thumbnailCancellation = null;
        Thumbnail = null;
    }

    private static Windows.Graphics.Imaging.SoftwareBitmap? Render(FluentIconLibrary library, string id, BuiltInIconStyle style, string glyphColor)
    {
        // Glyphs are drawn in the theme's text color, so they show on dark backgrounds too.
        return library.GetSvg(id, style) is { } svgBytes && SvgFile.TryParse(svgBytes) is { } svg && SvgRenderer.Render(svg.Recolor(glyphColor), ThumbnailSize).Pixels is { } pixels
            ? IconPreviewRenderer.ToSoftwareBitmap(pixels, ThumbnailSize)
            : null;
    }
}

/// <summary>
/// The icons to pick from: the saved icons first (when shown), then the built-in set, with a search box and a Regular / Filled switch.
/// </summary>
public sealed partial class IconListViewModel : ObservableObject
{
    private const int SearchDelayMilliseconds = 200;

    private readonly MyIconsStore _store;
    private readonly ThumbnailLoader _savedIconLoader;
    private CancellationTokenSource? _searchCancellation;

    internal IconListViewModel(IServiceProvider services, bool showsSavedIcons)
    {
        _store = services.GetRequiredService<MyIconsStore>();
        _savedIconLoader = services.GetRequiredService<ThumbnailLoader>();
        Library = services.GetRequiredService<FluentIconLibrary>();
        ShowsSavedIcons = showsSavedIcons;
        RefreshSavedIcons();
        RefreshBuiltInIcons();
    }

    public event EventHandler<SavedIcon>? SavedIconPicked;

    public event EventHandler<GlyphSource>? BuiltInIconPicked;

    public bool ShowsSavedIcons { get; }

    public ObservableCollection<SavedIconViewModel> SavedIcons { get; } = [];

    /// <summary>Replaced as a whole on each search: adding thousands of tiles one by one would redraw the grid each time.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoBuiltInIcons))]
    public partial IReadOnlyList<BuiltInIconViewModel> BuiltInIcons { get; private set; } = [];

    public bool HasSavedIcons => ShowsSavedIcons && SavedIcons.Count > 0;

    public bool HasNoBuiltInIcons => BuiltInIcons.Count == 0;

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsFilled { get; set; }

    /// <summary>The theme's text color (#RRGGBB) the built-in glyphs are drawn in. Set by the view.</summary>
    [ObservableProperty]
    public partial string GlyphColor { get; set; } = "#000000";

    internal FluentIconLibrary Library { get; }

    /// <summary>Its own loader, so scrolling through the set never holds up the builder's preview or the folder's tiles.</summary>
    internal ThumbnailLoader ThumbnailLoader { get; } = new();

    internal BuiltInIconStyle Style => IsFilled ? BuiltInIconStyle.Filled : BuiltInIconStyle.Regular;

    public void RefreshSavedIcons()
    {
        SavedIcons.Clear();
        if (ShowsSavedIcons)
        {
            var query = SearchText.Trim();
            foreach (var savedIcon in _store.List().Where(savedIcon => query.Length == 0 || savedIcon.Recipe.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
            {
                SavedIcons.Add(new SavedIconViewModel(savedIcon, _savedIconLoader));
            }
        }
        OnPropertyChanged(nameof(HasSavedIcons));
    }

    public void PickSavedIcon(SavedIconViewModel savedIcon)
    {
        SavedIconPicked?.Invoke(this, savedIcon.SavedIcon);
    }

    public void PickBuiltInIcon(BuiltInIconViewModel builtInIcon)
    {
        if (Library.GetSvg(builtInIcon.Icon.Id, Style) is { } svgBytes && GlyphSource.FromBuiltIn(builtInIcon.Icon, Style, svgBytes) is { } source)
        {
            BuiltInIconPicked?.Invoke(this, source);
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        _ = SearchAfterTypingAsync();
    }

    partial void OnIsFilledChanged(bool value)
    {
        RefreshBuiltInIcons();
    }

    partial void OnGlyphColorChanged(string value)
    {
        RefreshBuiltInIcons();
    }

    /// <summary>Searches once typing pauses, so each keystroke doesn't rebuild a list of thousands.</summary>
    private async Task SearchAfterTypingAsync()
    {
        _searchCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _searchCancellation = cancellation;
        try
        {
            await Task.Delay(SearchDelayMilliseconds, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        RefreshSavedIcons();
        RefreshBuiltInIcons();
    }

    private void RefreshBuiltInIcons()
    {
        foreach (var builtInIcon in BuiltInIcons)
        {
            builtInIcon.ReleaseThumbnail();
        }
        BuiltInIcons = Library.Search(SearchText, Style).Select(icon => new BuiltInIconViewModel(icon, this)).ToList();
    }
}
