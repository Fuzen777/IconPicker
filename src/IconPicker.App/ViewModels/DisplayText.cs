using System.Windows.Input;
using IconPicker.App.Services;
using IconPicker.Core.Applying;
using IconPicker.Core.Folders;
using Microsoft.UI.Xaml.Controls;

namespace IconPicker.App.ViewModels;

/// <summary>Text shared by single and batch mode: folder names, icon states and the results of applying.</summary>
internal static class DisplayText
{
    /// <summary>A folder's own name, or the whole path for a drive.</summary>
    public static string FolderName(string folderPath)
    {
        var folderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(folderPath));
        return string.IsNullOrEmpty(folderName) ? folderPath : folderName;
    }

    /// <summary>The name of what a change was for: a shortcut's name, or the folder's.</summary>
    public static string ChangeName(IconChange change)
    {
        return change.ShortcutPath is null ? FolderName(change.FolderPath) : Path.GetFileNameWithoutExtension(change.ShortcutPath);
    }

    public static string DescribeIconState(FolderIconState state, string folderPath)
    {
        if (state.IsBroken)
        {
            return Strings.Get("IconStateBroken");
        }
        if (state.IsMissingFromShortcut)
        {
            return Strings.Get("IconStateNotOnShortcut");
        }
        return state.Kind switch
        {
            FolderIconKind.Copied => Strings.Get("IconStateCopied"),
            FolderIconKind.Linked => Strings.Format("IconStateLinked", Path.GetRelativePath(folderPath, state.IconPath!)),
            FolderIconKind.External => Strings.Format("IconStateExternal", state.IconPath ?? state.IconSetting),
            _ => Strings.Get("IconStateNone"),
        };
    }

    public static string DescribeFailure(IconChangeOutcome outcome)
    {
        return outcome.Problem switch
        {
            IconSourceProblem.NoIcon => Strings.Get("ProblemNoIcon"),
            IconSourceProblem.InvalidIconFile => Strings.Get("ProblemInvalidIconFile"),
            IconSourceProblem.UnreadableImage => Strings.Get("ProblemUnreadableImage"),
            IconSourceProblem.UnreadableVector => Strings.Get("ProblemUnreadableVector"),
            _ => outcome.FailureMessage ?? "",
        };
    }

    /// <summary>
    /// Shows what applying did, as the script's summary did: how many icons were set or removed, each failure with its reason, and the folders Windows refused, with a retry as admin when it may help.
    /// </summary>
    public static void ShowOutcomes(StatusMessage status, IReadOnlyList<IconChangeOutcome> outcomes, ICommand retryAsAdminCommand, bool wasElevated)
    {
        var setCount = outcomes.Count(outcome => outcome.Result == IconChangeResult.Applied && outcome.Change.Kind == IconChangeKind.Set);
        var removedCount = outcomes.Count(outcome => outcome.Result == IconChangeResult.Applied && outcome.Change.Kind == IconChangeKind.Remove);
        var failed = outcomes.Where(outcome => outcome.Result == IconChangeResult.Failed).ToList();
        var refused = outcomes.Where(outcome => outcome.Result == IconChangeResult.Refused).ToList();

        var titleParts = new List<string>();
        if (setCount > 0)
        {
            titleParts.Add(Strings.Count(setCount, "OutcomeIconSet", "OutcomeIconsSet"));
        }
        if (removedCount > 0)
        {
            titleParts.Add(Strings.Count(removedCount, "OutcomeIconRemoved", "OutcomeIconsRemoved"));
        }
        if (failed.Count > 0)
        {
            titleParts.Add(Strings.Count(failed.Count, "OutcomeFolderFailed", "OutcomeFoldersFailed"));
        }
        if (refused.Count > 0)
        {
            titleParts.Add(Strings.Count(refused.Count, "OutcomeFolderRefused", "OutcomeFoldersRefused"));
        }
        if (titleParts.Count == 0)
        {
            titleParts.Add(Strings.Get("OutcomeNothingChanged"));
        }

        var messageLines = failed.Select(outcome => Strings.Format("FailureLine", ChangeName(outcome.Change), DescribeFailure(outcome))).ToList();
        if (refused.Count > 0)
        {
            messageLines.Add(Strings.Get(wasElevated ? "OutcomeRefusedEvenAsAdmin" : "OutcomeRefusedMessage"));
            messageLines.AddRange(refused.Select(outcome => ChangeName(outcome.Change)));
        }
        if (setCount + removedCount > 0)
        {
            messageLines.Add(Strings.Get("OutcomeExplorerHint"));
        }

        var severity = failed.Count > 0 ? InfoBarSeverity.Error : refused.Count > 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Success;
        var canRetry = refused.Count > 0 && !wasElevated;
        status.Show(severity, string.Join(" · ", titleParts), string.Join(Environment.NewLine, messageLines), canRetry ? Strings.Get("RetryAsAdminButton") : null, canRetry ? retryAsAdminCommand : null);
    }
}
