using System.Diagnostics;
using Microsoft.Windows.AppLifecycle;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;

namespace IconPicker.App.Services;

/// <summary>Keeps a single Icon Picker window: a second launch hands its folder to the running one and exits.</summary>
internal static class SingleInstance
{
    private const string InstanceKey = "IconPicker.Main";

    /// <summary>Returns true when another instance is running and received this launch, so this process should exit.</summary>
    public static bool RedirectToRunningInstance()
    {
        var mainInstance = AppInstance.FindOrRegisterForKey(InstanceKey);
        if (mainInstance.IsCurrent)
        {
            return false;
        }
        RedirectActivation(mainInstance, AppInstance.GetCurrent().GetActivatedEventArgs());
        return true;
    }

    private static void RedirectActivation(AppInstance mainInstance, AppActivationArguments activationArguments)
    {
        using var redirectFinished = PInvoke.CreateEvent(null, true, false, null);
        _ = Task.Run(() =>
        {
            try
            {
                mainInstance.RedirectActivationToAsync(activationArguments).AsTask().Wait();
            }
            finally
            {
                PInvoke.SetEvent(redirectFinished);
            }
        });
        // The redirection needs COM calls to reach this thread, so the wait keeps pumping them instead of blocking.
        PInvoke.CoWaitForMultipleObjects((uint)CWMO_FLAGS.CWMO_DEFAULT, PInvoke.INFINITE, [(HANDLE)redirectFinished.DangerousGetHandle()], out _);
        // This process was just started by the user, so it may hand the foreground to the running window.
        using var mainProcess = Process.GetProcessById((int)mainInstance.ProcessId);
        PInvoke.SetForegroundWindow((HWND)mainProcess.MainWindowHandle);
    }
}
