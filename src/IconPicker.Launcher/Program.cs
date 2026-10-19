using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace IconPicker.Launcher;

/// <summary>
/// Starts lib\IconPicker.exe with this program's own command line, as it was typed: the app reads its raw command line itself (a drive passed as "D:\" included).
/// </summary>
internal static class Program
{
    private const int AnyProcess = -1;
    private const uint ErrorIcon = 0x10;

    [STAThread]
    private static int Main()
    {
        var appPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib", "IconPicker.exe");
        try
        {
            // The app brings its window to the front, or a running window's, which Windows only allows when started from the foreground.
            AllowSetForegroundWindow(AnyProcess);
            using var app = Process.Start(new ProcessStartInfo(appPath, ArgumentsAfterProgram(Environment.CommandLine))
            {
                UseShellExecute = false,
                WorkingDirectory = Environment.CurrentDirectory,
            });
            return 0;
        }
        catch (Win32Exception exception)
        {
            // The app's own texts live in its resources, out of reach here; this one only shows when the lib folder is missing or damaged.
            MessageBox(IntPtr.Zero, $"Icon Picker couldn't start {appPath}: {exception.Message}\n\nExtract the whole zip again, keeping the lib folder next to IconPicker.exe.", "Icon Picker", ErrorIcon);
            return 1;
        }
    }

    /// <summary>The command line without the program's own path, which comes first, quoted or not.</summary>
    private static string ArgumentsAfterProgram(string commandLine)
    {
        var text = commandLine.TrimStart();
        int end;
        if (text.StartsWith("\"", StringComparison.Ordinal))
        {
            var closingQuote = text.IndexOf('"', 1);
            end = closingQuote < 0 ? text.Length : closingQuote + 1;
        }
        else
        {
            end = text.IndexOfAny([' ', '\t']);
            if (end < 0)
            {
                end = text.Length;
            }
        }
        return text.Substring(end).TrimStart();
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr window, string text, string caption, uint type);
}
