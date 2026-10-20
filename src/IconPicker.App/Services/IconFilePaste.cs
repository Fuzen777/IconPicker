using System.Runtime.InteropServices;
using IconPicker.Core.Candidates;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Core;

namespace IconPicker.App.Services;

/// <summary>A page or view that takes icon files pasted with Ctrl+V.</summary>
internal interface IIconFilePasteTarget
{
    /// <summary>Takes the pasted files, as a drop on its icons would; false when there is nowhere to put them (in "Batch", no row selected).</summary>
    Task<bool> PasteAsync(IReadOnlyList<string> iconFiles);
}

/// <summary>
/// Ctrl+V outside a text box: files copied in Explorer or Everything, or copied paths (Everything's Copy Full Name, Explorer's Copy as Path), go where a drop would.
/// A copied path also works from an app running as administrator, which Windows keeps from handing its files over.
/// </summary>
internal static class IconFilePaste
{
    /// <summary>Ctrl+V. It reaches the window only outside text boxes, which paste text themselves.</summary>
    public static bool IsPasteKey(KeyRoutedEventArgs eventArgs)
    {
        return eventArgs.Key == VirtualKey.V
            && !eventArgs.KeyStatus.IsMenuKeyDown
            && InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
    }

    /// <summary>The clipboard's files, or the paths in its text.</summary>
    public static async Task<IconFileSelection> ReadClipboardAsync()
    {
        try
        {
            var content = Clipboard.GetContent();
            if (content.Contains(StandardDataFormats.StorageItems))
            {
                return await IconFileDrop.ReadAsync(content);
            }
            if (content.Contains(StandardDataFormats.Text))
            {
                return IconFileSelection.FromPaths(IconFileSelection.ParsePathText(await content.GetTextAsync()));
            }
        }
        catch (COMException)
        {
            // The clipboard is busy, or its owner didn't hand the content over.
        }
        return IconFileSelection.None;
    }
}
