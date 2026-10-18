using IconPicker.App.Services;
using IconPicker.Core.Applying;
using IconPicker.Core.Launch;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace IconPicker.App;

public static class Program
{
    [STAThread]
    private static int Main()
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        // The raw command line: .NET's own version starts with IconPicker.dll and has already split the arguments the standard way.
        var launchArguments = LaunchArguments.Parse(Windows.Win32.PInvoke.GetCommandLine().ToString());
        // The elevated helper applies the changes Windows refused, then exits without showing a window.
        if (launchArguments.ElevatedJobPath is not null)
        {
            return ElevatedApplyJob.RunAsync(launchArguments.ElevatedJobPath).GetAwaiter().GetResult();
        }
        // Restarted after the portable setting changed: the data moves before anything reads it, once the previous window has closed.
        var dataMoveProblem = launchArguments.DataMove is { } dataMove ? DataLocation.MoveForRestart(dataMove) : null;
        if (SingleInstance.RedirectToRunningInstance())
        {
            return 0;
        }
        Application.Start(initializationParameters =>
        {
            var synchronizationContext = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);
            _ = new App(launchArguments, dataMoveProblem);
        });
        return 0;
    }
}
