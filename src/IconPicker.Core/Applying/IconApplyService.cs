using System.Text.Json.Serialization;
using IconPicker.Core.Candidates;
using IconPicker.Core.Folders;
using IconPicker.Core.Shell;

namespace IconPicker.Core.Applying;

public enum IconChangeKind
{
    Set,
    Remove,
}

/// <summary>One folder to change.</summary>
/// <param name="FolderPath">The folder's full path.</param>
/// <param name="Kind">Set a new icon or remove the current one.</param>
/// <param name="Source">The new icon, for <see cref="IconChangeKind.Set"/>.</param>
/// <param name="ShortcutPath">A shortcut to the folder that shows its icon too, when the folder was chosen through one.</param>
public sealed record IconChange(string FolderPath, IconChangeKind Kind, IconSource? Source = null, string? ShortcutPath = null)
{
    /// <summary>What the user chose: the shortcut when there is one, otherwise the folder.</summary>
    [JsonIgnore]
    public string ItemPath => ShortcutPath ?? FolderPath;
}

public enum IconChangeResult
{
    Applied,

    /// <summary>Nothing to do: Remove on a folder without a custom icon.</summary>
    Unchanged,

    /// <summary>Windows refused access; an elevated retry may work.</summary>
    Refused,

    Failed,
}

/// <summary>What happened to one change.</summary>
/// <param name="Change">The change.</param>
/// <param name="Result">How it went.</param>
/// <param name="Problem">Why the chosen file couldn't give an icon, when that is the reason it failed.</param>
/// <param name="FailureMessage">Windows' own error message for other failures.</param>
public sealed record IconChangeOutcome(IconChange Change, IconChangeResult Result, IconSourceProblem? Problem = null, string? FailureMessage = null);

/// <summary>Applies icon changes and reports each outcome, ported from Invoke-FolderChanges in Set Folder Icons.ps1. Used by both modes and by the elevated helper.</summary>
public static class IconApplyService
{
    public static Task<List<IconChangeOutcome>> ApplyAsync(IReadOnlyList<IconChange> changes, IconTarget target, CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            var outcomes = new List<IconChangeOutcome>(changes.Count);
            foreach (var change in changes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                outcomes.Add(await ApplyOneAsync(change, target, cancellationToken).ConfigureAwait(false));
            }
            if (outcomes.Exists(outcome => outcome.Result == IconChangeResult.Applied))
            {
                ShellNotifier.NotifyIconsChanged();
            }
            return outcomes;
        }, cancellationToken);
    }

    private static async Task<IconChangeOutcome> ApplyOneAsync(IconChange change, IconTarget target, CancellationToken cancellationToken)
    {
        try
        {
            var folder = new DirectoryInfo(change.FolderPath);
            if (change.Kind == IconChangeKind.Remove)
            {
                var isFolderChanged = FolderIconWriter.RemoveIcon(folder);
                var isShortcutChanged = change.ShortcutPath is { } shortcutPath && ShortcutIconWriter.RemoveFolderIcon(shortcutPath, folder);
                return new IconChangeOutcome(change, isFolderChanged || isShortcutChanged ? IconChangeResult.Applied : IconChangeResult.Unchanged);
            }
            var source = change.Source ?? throw new ArgumentException("A Set change needs a source.", nameof(change));
            var plan = await IconTargetResolver.ResolveAsync(folder, source, target, cancellationToken).ConfigureAwait(false);
            FolderIconWriter.SetIcon(folder, plan);
            if (change.ShortcutPath is not null)
            {
                ShortcutIconWriter.ShowFolderIcon(change.ShortcutPath, folder);
            }
            return new IconChangeOutcome(change, IconChangeResult.Applied);
        }
        catch (IconSourceException exception)
        {
            return new IconChangeOutcome(change, IconChangeResult.Failed, exception.Problem);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)
        {
            var innermostException = GetInnermostException(exception);
            return innermostException is UnauthorizedAccessException
                ? new IconChangeOutcome(change, IconChangeResult.Refused)
                : new IconChangeOutcome(change, IconChangeResult.Failed, FailureMessage: innermostException.Message);
        }
    }

    private static Exception GetInnermostException(Exception exception)
    {
        while (exception.InnerException is not null && exception is not UnauthorizedAccessException)
        {
            exception = exception.InnerException;
        }
        return exception;
    }
}
