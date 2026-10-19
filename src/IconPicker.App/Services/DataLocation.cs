using System.Diagnostics;
using IconPicker.Core.Launch;
using IconPicker.Core.Storage;

namespace IconPicker.App.Services;

/// <summary>Why moving the data failed.</summary>
/// <param name="WindowsMessage">Windows' own error, or null when the previous window never closed.</param>
internal sealed record DataMoveProblem(string? WindowsMessage);

/// <summary>
/// Where the app keeps its data: %LocalAppData%\Icon Picker by default, or a Data folder next to IconPicker.exe when there is one (portable).
/// Read once at startup, since every service fixes its paths then; changing it restarts the app.
/// </summary>
internal static class DataLocation
{
    private const string PortableFolderName = "Data";

    /// <summary>How long the restarted copy waits for the one that asked for the restart to exit.</summary>
    private static readonly TimeSpan PreviousCopyTimeout = TimeSpan.FromSeconds(30);

    private static bool? _isPortable;

    public static string AppDataFolder { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Icon Picker");

    /// <summary>
    /// The folder the app was unzipped or installed to: in the release layout, lib's parent, where the launcher IconPicker.exe sits; otherwise the app's own folder (a build).
    /// </summary>
    public static string AppRoot { get; } = FindAppRoot();

    public static string PortableFolder { get; } = Path.Combine(AppRoot, PortableFolderName);

    public static bool IsPortable => _isPortable ??= Directory.Exists(PortableFolder);

    public static string Folder => IsPortable ? PortableFolder : AppDataFolder;

    /// <summary>Whether a Data folder can be made next to the app: not in a folder only admins may write to, such as Program Files.</summary>
    public static bool CanBePortable()
    {
        if (IsPortable)
        {
            return true;
        }
        var probePath = Path.Combine(AppRoot, $"write-test-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(probePath, "");
            File.Delete(probePath);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string FindAppRoot()
    {
        var appFolder = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
        var parentFolder = Path.GetDirectoryName(appFolder);
        var isInLib = string.Equals(Path.GetFileName(appFolder), "lib", StringComparison.OrdinalIgnoreCase) && parentFolder is not null && File.Exists(Path.Combine(parentFolder, "IconPicker.exe"));
        return isInLib ? parentFolder! : appFolder;
    }

    /// <summary>Starts a new copy of the app that moves the data once this one has exited. The caller then exits.</summary>
    public static void RestartToMove(bool toPortable)
    {
        var request = new DataMoveRequest(toPortable, Environment.ProcessId);
        Process.Start(new ProcessStartInfo(Environment.ProcessPath!, LaunchArguments.FormatDataMove(request)) { UseShellExecute = false });
    }

    /// <summary>
    /// Runs first in the restarted copy, before anything reads the data: waits for the previous copy to exit (it saves the window's placement as it closes), then moves the data. Returns why it failed, or null.
    /// </summary>
    public static DataMoveProblem? MoveForRestart(DataMoveRequest request)
    {
        try
        {
            if (!WaitForExit(request.PreviousProcessId))
            {
                return new DataMoveProblem(null);
            }
            if (request.ToPortable)
            {
                DataFolderMover.Move(AppDataFolder, PortableFolder);
            }
            else
            {
                DataFolderMover.Move(PortableFolder, AppDataFolder);
            }
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ErrorLog.Write(exception);
            return new DataMoveProblem(exception.Message);
        }
    }

    /// <summary>Whether the previous copy has exited within the timeout.</summary>
    private static bool WaitForExit(int processId)
    {
        Process previousCopy;
        try
        {
            previousCopy = Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            // Already gone.
            return true;
        }
        using (previousCopy)
        {
            // The number may already belong to another program.
            using var currentProcess = Process.GetCurrentProcess();
            return !string.Equals(previousCopy.ProcessName, currentProcess.ProcessName, StringComparison.OrdinalIgnoreCase) || previousCopy.WaitForExit(PreviousCopyTimeout);
        }
    }
}
