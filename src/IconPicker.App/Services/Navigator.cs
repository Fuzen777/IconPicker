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

/// <summary>
/// Moves between pages. The open folder lives here rather than in its page, so visiting another page keeps it exactly as it was, and its tab or Back returns to it.
/// Opening another folder replaces it, after asking when it has icons picked but not applied; leaving My icons with an icon being built asks too.
/// </summary>
internal sealed class Navigator(DialogService dialogs, IServiceProvider services)
{
    private Frame? _frame;

    /// <summary>The folder opened last, kept until another one replaces it.</summary>
    public FolderViewModel? CurrentFolder { get; private set; }

    public event EventHandler? CurrentFolderChanged;

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
        if (!await ConfirmLeavingAsync() || !await ConfirmReplacingFolderAsync())
        {
            return;
        }
        CurrentFolder?.Dispose();
        CurrentFolder = new FolderViewModel(request, services);
        // Back never returns to a folder that was replaced.
        foreach (var entry in Frame.BackStack.Where(entry => entry.SourcePageType == typeof(FolderPage)).ToList())
        {
            Frame.BackStack.Remove(entry);
        }
        CurrentFolderChanged?.Invoke(this, EventArgs.Empty);
        Frame.Navigate(typeof(FolderPage), CurrentFolder);
    }

    /// <summary>Back to the open folder, as it was left.</summary>
    public async Task ReturnToFolderAsync()
    {
        if (CurrentFolder is not null && Frame.Content is not FolderPage && await ConfirmLeavingAsync())
        {
            Frame.Navigate(typeof(FolderPage), CurrentFolder);
        }
    }

    private async Task<bool> ConfirmReplacingFolderAsync()
    {
        return CurrentFolder is not { HasUnappliedChanges: true }
            || await dialogs.ConfirmAsync(Strings.Get("DiscardChangesTitle"), Strings.Format("DiscardFolderChangesMessage", CurrentFolder.TabName), Strings.Get("DiscardChangesConfirm"));
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
