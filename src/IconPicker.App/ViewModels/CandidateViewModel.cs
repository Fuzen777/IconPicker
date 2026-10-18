using CommunityToolkit.Mvvm.ComponentModel;
using IconPicker.App.Services;
using IconPicker.Core.Candidates;
using IconPicker.Core.Icons;
using Microsoft.UI.Xaml.Media;

namespace IconPicker.App.ViewModels;

/// <summary>One tile of the icon picker.</summary>
public sealed partial class CandidateViewModel : ObservableObject
{
    /// <summary>Physical pixels: sharp at 64 px on screens scaled up to 150%.</summary>
    private const int ThumbnailSize = 96;

    private readonly ThumbnailLoader _thumbnailLoader;
    private ImageSource? _thumbnail;
    private bool _isThumbnailRequested;

    internal CandidateViewModel(IconCandidate candidate, ThumbnailLoader thumbnailLoader)
    {
        Candidate = candidate;
        _thumbnailLoader = thumbnailLoader;
    }

    public IconCandidate Candidate { get; }

    public IconSource Source => Candidate.Source;

    /// <summary>The file's name; a built icon's own name rather than its file's.</summary>
    public string FileName => Candidate.Built?.Name ?? Path.GetFileName(Candidate.RelativePath);

    public string Badge => Strings.Get(Candidate.Built is not null ? "BadgeMine" : Candidate.ShortcutPath is { } shortcutPath
        ? (string.Equals(Path.GetExtension(shortcutPath), ".url", StringComparison.OrdinalIgnoreCase) ? "BadgeUrl" : "BadgeLnk")
        : Source.Kind switch
        {
            IconSourceKind.Module when string.Equals(Path.GetExtension(Source.FilePath), ".dll", StringComparison.OrdinalIgnoreCase) => "BadgeDll",
            IconSourceKind.Module => "BadgeExe",
            IconSourceKind.IconFile => "BadgeIco",
            IconSourceKind.Vector => "BadgeSvg",
            _ => "BadgeImage",
        });

    public string DetailText
    {
        get
        {
            if (Source.Kind == IconSourceKind.Image)
            {
                return Strings.Format("DetailImageSize", Candidate.Width, Candidate.Height);
            }
            if (Source.Kind == IconSourceKind.Vector)
            {
                return Strings.Get("DetailScalable");
            }
            return Source.Kind == IconSourceKind.Module && Source.IconIndex > 0
                ? Strings.Format("DetailIconSizeWithIndex", Candidate.Width, Source.IconIndex + 1)
                : Strings.Format("DetailIconSize", Candidate.Width);
        }
    }

    public string ToolTipText => Candidate switch
    {
        { Built: { } origin } => Strings.Format(origin.SavedIconId is null ? "BuiltIconToolTip" : "SavedIconToolTip", origin.Name),
        { IsShortcut: true } => Strings.Format("ShortcutToolTip", Candidate.RelativePath, Source.FilePath),
        _ => Candidate.RelativePath,
    } + Environment.NewLine + DetailText;

    /// <summary>
    /// Drawn the first time a tile asks for it. The grid only creates tiles as they scroll into view, so large folders only draw what is seen.
    /// </summary>
    public ImageSource? Thumbnail
    {
        get
        {
            if (!_isThumbnailRequested)
            {
                _isThumbnailRequested = true;
                _ = LoadThumbnailAsync();
            }
            return _thumbnail;
        }
    }

    private async Task LoadThumbnailAsync()
    {
        _thumbnail = await _thumbnailLoader.LoadAsync(() => IconPreviewRenderer.RenderAsync(Source, ThumbnailSize));
        OnPropertyChanged(nameof(Thumbnail));
    }
}
