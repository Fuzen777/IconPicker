using IconPicker.Core.Applying;
using IconPicker.Core.Candidates;
using IconPicker.Core.Recent;

namespace IconPicker.App.Services;

/// <summary>Adds an icon just applied to the recent icons, in the background; a failure only goes to the error log, since the folder already has its icon.</summary>
internal sealed class RecentIconRecorder(RecentIconStore store)
{
    public async Task RecordAsync(IconCandidate candidate)
    {
        try
        {
            await Task.Run(() => store.AddAsync(candidate, CancellationToken.None));
        }
        catch (Exception exception) when (exception is IconSourceException or IOException or UnauthorizedAccessException)
        {
            ErrorLog.Write(exception);
        }
    }
}
