using System.Text.Json;
using System.Text.Json.Serialization;
using IconPicker.Core.Shell;

namespace IconPicker.Core.Applying;

/// <summary>The changes handed to the elevated copy of the app.</summary>
public sealed record ApplyJob(IconTarget Target, IReadOnlyList<IconChange> Changes);

/// <summary>What the elevated copy did with each change.</summary>
public sealed record ApplyJobResult(IReadOnlyList<IconChangeOutcome> Outcomes);

/// <summary>
/// Hands changes Windows refused to a short-lived elevated copy of the app and reads back what it did. The job goes through a file because the elevated copy can't be given objects directly.
/// </summary>
public static class ElevatedApplyJob
{
    public static string GetResultPath(string jobPath)
    {
        return jobPath + ".result.json";
    }

    public static async Task WriteJobAsync(string jobPath, ApplyJob job)
    {
        await using var jobFile = File.Create(jobPath);
        await JsonSerializer.SerializeAsync(jobFile, job, ApplyJobJsonContext.Default.ApplyJob).ConfigureAwait(false);
    }

    /// <summary>The elevated copy's outcomes, or null when it didn't write any (it was refused admin rights or failed to start).</summary>
    public static async Task<ApplyJobResult?> ReadResultAsync(string jobPath)
    {
        var resultPath = GetResultPath(jobPath);
        if (!File.Exists(resultPath))
        {
            return null;
        }
        await using var resultFile = File.OpenRead(resultPath);
        return await JsonSerializer.DeserializeAsync(resultFile, ApplyJobJsonContext.Default.ApplyJobResult).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs in the elevated copy: applies the job and writes the outcomes next to it. Only existing folders, and shortcuts to folders, are touched, and only through <see cref="IconApplyService"/>.
    /// </summary>
    public static async Task<int> RunAsync(string jobPath)
    {
        ApplyJob? job;
        await using (var jobFile = File.OpenRead(jobPath))
        {
            job = await JsonSerializer.DeserializeAsync(jobFile, ApplyJobJsonContext.Default.ApplyJob).ConfigureAwait(false);
        }
        if (job is null)
        {
            return 1;
        }
        var validChanges = job.Changes.Where(IsValidChange).ToList();
        var outcomes = await IconApplyService.ApplyAsync(validChanges, job.Target).ConfigureAwait(false);
        outcomes.AddRange(job.Changes.Except(validChanges).Select(change => new IconChangeOutcome(change, IconChangeResult.Failed, FailureMessage: "Not an existing folder.")));
        await using var resultFile = File.Create(GetResultPath(jobPath));
        await JsonSerializer.SerializeAsync(resultFile, new ApplyJobResult(outcomes), ApplyJobJsonContext.Default.ApplyJobResult).ConfigureAwait(false);
        return 0;
    }

    private static bool IsValidChange(IconChange change)
    {
        return Path.IsPathFullyQualified(change.FolderPath)
            && Directory.Exists(change.FolderPath)
            && (change.Kind == IconChangeKind.Remove || (change.Source is not null && Path.IsPathFullyQualified(change.Source.FilePath)))
            && (change.ShortcutPath is null || (Path.IsPathFullyQualified(change.ShortcutPath) && File.Exists(change.ShortcutPath) && ShortcutReader.ReadFolderTarget(change.ShortcutPath) is not null));
    }
}

[JsonSerializable(typeof(ApplyJob))]
[JsonSerializable(typeof(ApplyJobResult))]
internal sealed partial class ApplyJobJsonContext : JsonSerializerContext;
