using IconPicker.App.ViewModels;
using IconPicker.App.Views;
using IconPicker.Core.Detection;
using IconPicker.Core.Shell;
using Microsoft.UI.Xaml.Controls;

namespace IconPicker.App.Services;

/// <summary>What to open on the folder page.</summary>
/// <param name="FolderPaths">One folder, or every library for "Rescan all".</param>
/// <param name="ForcedMode">The mode to open in, or null to detect it from the folder.</param>
/// <param name="ShortcutPath">When the folder was opened through a shortcut to it, the shortcut, which shows its icon too in "This folder".</param>
internal sealed record FolderRequest(IReadOnlyList<string> FolderPaths, FolderMode? ForcedMode = null, string? ShortcutPath = null)
{
    public static FolderRequest ForFolder(string folderPath, FolderMode? forcedMode = null)
    {
        return new FolderRequest([folderPath], forcedMode);
    }

    /// <summary>A folder, or a shortcut to one (dropped or passed on the command line), which opens its folder in "This folder" to give both the icon.</summary>
    public static FolderRequest ForPath(string path)
    {
        if (ShortcutReader.IsShortcut(path) && ShortcutReader.ReadFolderTarget(path) is { } target)
        {
            return new FolderRequest([target.FullName], FolderMode.Single, path);
        }
        return ForFolder(path);
    }
}

/// <summary>Moves between pages, asking first when leaving would lose work: icons picked but not applied, or an icon being built.</summary>
internal sealed class Navigator(DialogService dialogs)
{
    private Frame? _frame;

    private Frame Frame => _frame ?? throw new InvalidOperationException("The navigator has no frame yet.");

    public void Initialize(Frame frame)
    {
        _frame = frame;
    }

    public async Task GoHomeAsync()
    {
        if (Frame.Content is not HomePage && await ConfirmLeavingAsync())
        {
            Frame.Navigate(typeof(HomePage));
        }
    }

    public async Task OpenMyIconsAsync()
    {
        if (Frame.Content is not MyIconsPage && await ConfirmLeavingAsync())
        {
            Frame.Navigate(typeof(MyIconsPage));
        }
    }

    public async Task OpenSettingsAsync()
    {
        if (Frame.Content is not SettingsPage && await ConfirmLeavingAsync())
        {
            Frame.Navigate(typeof(SettingsPage));
        }
    }

    public async Task GoBackAsync()
    {
        if (Frame.CanGoBack && await ConfirmLeavingAsync())
        {
            Frame.GoBack();
        }
    }

    public async Task OpenFolderAsync(FolderRequest request)
    {
        var missingFolder = request.FolderPaths.FirstOrDefault(folderPath => !Directory.Exists(folderPath));
        if (missingFolder is not null)
        {
            await dialogs.ShowMessageAsync(Strings.Get("FolderMissingTitle"), Strings.Format("FolderMissingMessage", missingFolder));
            return;
        }
        if (await ConfirmLeavingAsync())
        {
            Frame.Navigate(typeof(FolderPage), request);
        }
    }

    private async Task<bool> ConfirmLeavingAsync()
    {
        if (Frame.Content is not IHasUnappliedChanges { HasUnappliedChanges: true } page)
        {
            return true;
        }
        return await dialogs.ConfirmAsync(page.DiscardChangesTitle, page.DiscardChangesMessage, Strings.Get("DiscardChangesConfirm"));
    }
}
