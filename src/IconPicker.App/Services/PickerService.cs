using IconPicker.Core.Candidates;
using Microsoft.Windows.Storage.Pickers;

namespace IconPicker.App.Services;

/// <summary>The Windows file and folder pickers, attached to the main window.</summary>
internal sealed class PickerService(WindowContext windowContext)
{
    public async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker(windowContext.WindowId);
        var result = await picker.PickSingleFolderAsync();
        return result?.Path;
    }

    /// <summary>Any file the app can take an icon from: .ico, .exe, .dll or a picture.</summary>
    public async Task<string?> PickIconSourceAsync()
    {
        var picker = new FileOpenPicker(windowContext.WindowId);
        foreach (var extension in IconSource.SupportedExtensions)
        {
            picker.FileTypeFilter.Add(extension);
        }
        var result = await picker.PickSingleFileAsync();
        return result?.Path;
    }
}
