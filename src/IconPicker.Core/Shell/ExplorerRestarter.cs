using System.ComponentModel;
using System.Diagnostics;

namespace IconPicker.Core.Shell;

/// <summary>Restarts Explorer so it redraws icons it has cached, ported from Restart Explorer.ps1.</summary>
public static class ExplorerRestarter
{
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RespawnGracePeriod = TimeSpan.FromSeconds(3);

    public static async Task RestartAsync()
    {
        foreach (var explorer in GetSessionExplorers())
        {
            using (explorer)
            {
                try
                {
                    explorer.Kill();
                    using var exitTimeout = new CancellationTokenSource(ExitTimeout);
                    await explorer.WaitForExitAsync(exitTimeout.Token);
                }
                catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or OperationCanceledException)
                {
                    // Already gone, or not ours to stop.
                }
            }
        }
        // Windows usually brings the shell back on its own after a kill. Starting a second one on top of it would only open a stray File Explorer window, so Windows gets a moment first.
        var deadline = DateTime.UtcNow + RespawnGracePeriod;
        while (!IsExplorerRunning() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(250);
        }
        if (!IsExplorerRunning())
        {
            Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true })?.Dispose();
        }
    }

    /// <summary>Only this session's Explorer: other signed-in users have their own, which must not be touched.</summary>
    private static Process[] GetSessionExplorers()
    {
        using var currentProcess = Process.GetCurrentProcess();
        var sessionId = currentProcess.SessionId;
        return Process.GetProcessesByName("explorer").Where(explorer => explorer.SessionId == sessionId).ToArray();
    }

    private static bool IsExplorerRunning()
    {
        var explorers = GetSessionExplorers();
        foreach (var explorer in explorers)
        {
            explorer.Dispose();
        }
        return explorers.Length > 0;
    }
}
