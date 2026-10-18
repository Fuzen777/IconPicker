using System.ComponentModel;
using System.Diagnostics;
using IconPicker.Core.Applying;
using IconPicker.Core.Launch;

namespace IconPicker.App.Services;

/// <summary>
/// Retries the changes Windows refused in a short-lived elevated copy of the app (one UAC prompt). The window itself never runs elevated, since Windows blocks drag and drop from Explorer into elevated windows.
/// </summary>
internal sealed class ElevatedRetry
{
    /// <summary>The elevated copy's outcomes, or null when admin rights weren't granted.</summary>
    public async Task<IReadOnlyList<IconChangeOutcome>?> RunAsync(IReadOnlyList<IconChange> changes, IconTarget target)
    {
        var jobsFolder = Path.Combine(SettingsStore.DataFolder, "jobs");
        Directory.CreateDirectory(jobsFolder);
        var jobPath = Path.Combine(jobsFolder, Guid.NewGuid().ToString("N") + ".json");
        await ElevatedApplyJob.WriteJobAsync(jobPath, new ApplyJob(target, changes));
        try
        {
            var startInfo = new ProcessStartInfo(Environment.ProcessPath!)
            {
                UseShellExecute = true,
                Verb = "runas",
                Arguments = $"{LaunchArguments.ElevatedJobSwitch} \"{jobPath}\"",
            };
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }
            await process.WaitForExitAsync();
            var result = await ElevatedApplyJob.ReadResultAsync(jobPath);
            return result?.Outcomes;
        }
        catch (Win32Exception)
        {
            // The UAC prompt was declined.
            return null;
        }
        finally
        {
            File.Delete(jobPath);
            File.Delete(ElevatedApplyJob.GetResultPath(jobPath));
        }
    }
}
