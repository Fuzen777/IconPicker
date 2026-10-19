using IconPicker.App.Services;
using IconPicker.Core.Builder;
using IconPicker.Core.Launch;
using IconPicker.Core.Library;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using LaunchActivationArguments = Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs;

namespace IconPicker.App;

public partial class App : Application
{
    private readonly LaunchArguments _launchArguments;
    private readonly DataMoveProblem? _dataMoveProblem;
    private MainWindow? _mainWindow;

    internal App(LaunchArguments launchArguments, DataMoveProblem? dataMoveProblem)
    {
        _launchArguments = launchArguments;
        _dataMoveProblem = dataMoveProblem;
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        Services = new ServiceCollection()
            .AddSingleton<SettingsStore>()
            .AddSingleton<WindowContext>()
            .AddSingleton<DialogService>()
            .AddSingleton<PickerService>()
            .AddSingleton<ThumbnailLoader>()
            .AddSingleton<Navigator>()
            .AddSingleton<ElevatedRetry>()
            .AddSingleton<ContextMenuRegistration>()
            .AddSingleton(new MyIconsStore(Path.Combine(SettingsStore.DataFolder, "My icons")))
            .AddSingleton(new FluentIconLibrary(Path.Combine(AppContext.BaseDirectory, "Assets", "FluentIcons.zip")))
            .AddSingleton<BuiltIconCache>()
            .AddSingleton<IconBuilderService>()
            .BuildServiceProvider();
        _ = Task.Run(BuiltIconCache.DeleteOldIcons);
        ApplyOwnFolderIcon();
        _mainWindow = new MainWindow();
        Services.GetRequiredService<WindowContext>().Attach(_mainWindow);
        _mainWindow.Activate();
        // A second launch (another right-click, a drop on the shortcut) arrives here instead of opening a second window.
        AppInstance.GetCurrent().Activated += OnRedirectedActivation;
        StartMainWindow(_mainWindow);
    }

    /// <summary>The app's folder gets the app's icon the first time a release copy starts there.</summary>
    private static async void ApplyOwnFolderIcon()
    {
        var settings = Services.GetRequiredService<SettingsStore>();
        if (!AppFolderIcon.IsDue(settings.Current))
        {
            return;
        }
        try
        {
            await Task.Run(AppFolderIcon.ApplyAsync);
            // Back on the window's thread, which follows settings changes.
            settings.Update(current => current.AppFolderIconPath = AppFolderIcon.AppFolder.FullName);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ErrorLog.Write(exception);
        }
    }

    private async void StartMainWindow(MainWindow mainWindow)
    {
        try
        {
            if (_launchArguments.DataMove is not null)
            {
                await mainWindow.StartAfterDataMoveAsync(_dataMoveProblem);
            }
            else
            {
                await mainWindow.StartAsync(_launchArguments.FolderPath);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or System.Runtime.InteropServices.COMException)
        {
            ErrorLog.Write(exception);
        }
    }

    private void OnRedirectedActivation(object? sender, AppActivationArguments activationArguments)
    {
        var commandLine = activationArguments.Data is LaunchActivationArguments launchActivation ? launchActivation.Arguments : null;
        var folderPath = LaunchArguments.Parse(commandLine).FolderPath;
        _mainWindow?.DispatcherQueue.TryEnqueue(async () =>
        {
            _mainWindow.BringToFront();
            if (folderPath is not null)
            {
                await Services.GetRequiredService<Navigator>().OpenFolderAsync(FolderRequest.ForPath(folderPath));
            }
        });
    }

    private async void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs eventArgs)
    {
        ErrorLog.Write(eventArgs.Exception);
        if (_mainWindow is null)
        {
            return;
        }
        // Keep the window and what the user picked; the details are in the log.
        eventArgs.Handled = true;
        try
        {
            await Services.GetRequiredService<DialogService>().ShowMessageAsync(Strings.Get("ErrorTitle"), Strings.Format("ErrorMessage", eventArgs.Message, ErrorLog.LogPath));
        }
        catch (Exception dialogException) when (dialogException is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            ErrorLog.Write(dialogException);
        }
    }
}
