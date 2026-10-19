using IconPicker.App.ViewModels;
using IconPicker.App.Views;
using IconPicker.Core.Builder;
using IconPicker.Core.Candidates;

namespace IconPicker.App.Services;

/// <summary>Opens the icon builder dialog from a folder's picker, so view models never build XAML themselves.</summary>
internal sealed class IconBuilderService(IServiceProvider services, DialogService dialogs, MyIconsStore store, BuiltIconCache builtIconCache)
{
    /// <summary>A shape suits an icon being customized: pictures and executables' icons keep their colors on a rounded square.</summary>
    public static IconStyle CustomizeStyle { get; } = new(null, IconShape.Rounded, IconStyle.Default.BackgroundColor, IconStyle.DefaultScale(IconShape.Rounded));

    /// <summary>Icons: the saved icons, then the built-in ones. Null when cancelled.</summary>
    public Task<IconCandidate?> PickIconAsync()
    {
        return ShowAsync(dialog => dialog.ShowList());
    }

    /// <summary>The builder on a source: an SVG from Browse, or a tile to customize. Null when cancelled.</summary>
    public Task<IconCandidate?> BuildAsync(GlyphSource source, IconStyle? style = null)
    {
        return ShowAsync(dialog =>
        {
            dialog.Builder.LoadSource(source, style);
            dialog.ShowBuilder();
        });
    }

    /// <summary>The builder on an icon built earlier, as it was. Null when cancelled.</summary>
    public Task<IconCandidate?> EditAsync(BuiltIconOrigin origin)
    {
        return ShowAsync(dialog =>
        {
            dialog.Builder.LoadOrigin(origin);
            dialog.ShowBuilder();
        });
    }

    private async Task<IconCandidate?> ShowAsync(Action<IconBuilderDialog> prepare)
    {
        var dialog = new IconBuilderDialog(services);
        prepare(dialog);
        await dialogs.ShowAsync(dialog);
        var result = dialog.PickedSavedIcon is { } pickedIcon ? await BuiltIconCandidates.FromSavedIconAsync(pickedIcon, builtIconCache) : dialog.Result;
        // A saved icon picked for a folder moves to the top of My icons.
        if (result?.Built?.SavedIconId is { } id && store.Find(id) is { } savedIcon)
        {
            await store.MarkUsedAsync(savedIcon);
        }
        return result;
    }
}
