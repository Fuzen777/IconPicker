using IconPicker.App.Services;
using IconPicker.App.Views;
using IconPicker.Core.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace IconPicker.App;

public sealed partial class MainWindow : Window
{
    // In effective pixels; multiplied by the display scale.
    private const int DefaultWidth = 1120;
    private const int DefaultHeight = 780;
    private const int MinimumWidth = 760;
    private const int MinimumHeight = 540;

    private readonly Navigator _navigator;
    private readonly SettingsStore _settings;
    private RectInt32 _restoredBounds;

    public MainWindow()
    {
        InitializeComponent();
        _navigator = App.Services.GetRequiredService<Navigator>();
        _settings = App.Services.GetRequiredService<SettingsStore>();
        Title = Strings.Get("AppTitle");
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        RestorePlacement();
        AppWindow.Changed += OnAppWindowChanged;
        AppWindow.Closing += OnAppWindowClosing;
        _navigator.Initialize(ContentFrame);
    }

    private OverlappedPresenter Presenter => (OverlappedPresenter)AppWindow.Presenter;

    private double DisplayScale => PInvoke.GetDpiForWindow((HWND)WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;

    /// <summary>Shows Home, then the folder from the command line if there is one, so Back returns Home.</summary>
    public async Task StartAsync(string? folderPath)
    {
        ContentFrame.Navigate(typeof(HomePage));
        if (folderPath is null)
        {
            return;
        }
        await WaitUntilLoadedAsync();
        await _navigator.OpenFolderAsync(FolderRequest.ForPath(folderPath));
    }

    /// <summary>After the restart that moved the data: back in Settings, where the switch was turned, saying so when the move failed.</summary>
    internal async Task StartAfterDataMoveAsync(DataMoveProblem? problem)
    {
        ContentFrame.Navigate(typeof(HomePage));
        await _navigator.OpenSettingsAsync();
        if (problem is null)
        {
            return;
        }
        await WaitUntilLoadedAsync();
        var reason = problem.WindowsMessage ?? Strings.Get("DataMoveTimedOut");
        await App.Services.GetRequiredService<DialogService>().ShowMessageAsync(Strings.Get("DataMoveFailedTitle"), Strings.Format("DataMoveFailedMessage", reason, DataLocation.Folder));
    }

    /// <summary>Remembers where and how big the window is, as closing it does.</summary>
    public void SavePlacement()
    {
        var isMaximized = Presenter.State == OverlappedPresenterState.Maximized;
        _settings.Update(settings => settings.WindowPlacement = new WindowPlacement(_restoredBounds.X, _restoredBounds.Y, _restoredBounds.Width, _restoredBounds.Height, isMaximized));
    }

    /// <summary>Dialogs (such as "folder not found") need the window's content to be loaded.</summary>
    private async Task WaitUntilLoadedAsync()
    {
        if (!RootGrid.IsLoaded)
        {
            var loaded = new TaskCompletionSource();
            RootGrid.Loaded += (sender, eventArgs) => loaded.TrySetResult();
            await loaded.Task;
        }
    }

    public void BringToFront()
    {
        if (Presenter.State == OverlappedPresenterState.Minimized)
        {
            Presenter.Restore();
        }
        Activate();
    }

    private void RestorePlacement()
    {
        var displayScale = DisplayScale;
        Presenter.PreferredMinimumWidth = (int)(MinimumWidth * displayScale);
        Presenter.PreferredMinimumHeight = (int)(MinimumHeight * displayScale);
        var placement = _settings.Current.WindowPlacement;
        if (placement is not null)
        {
            var savedBounds = new RectInt32(placement.X, placement.Y, placement.Width, placement.Height);
            // A monitor that was unplugged since would leave the window off screen.
            if (DisplayArea.GetFromRect(savedBounds, DisplayAreaFallback.None) is not null)
            {
                AppWindow.MoveAndResize(savedBounds);
                _restoredBounds = savedBounds;
                if (placement.IsMaximized)
                {
                    Presenter.Maximize();
                }
                return;
            }
        }
        var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var width = Math.Min((int)(DefaultWidth * displayScale), workArea.Width);
        var height = Math.Min((int)(DefaultHeight * displayScale), workArea.Height);
        _restoredBounds = new RectInt32(workArea.X + (workArea.Width - width) / 2, workArea.Y + (workArea.Height - height) / 2, width, height);
        AppWindow.MoveAndResize(_restoredBounds);
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        // Only the normal size is remembered; a maximized window comes back maximized over it.
        if ((args.DidPositionChange || args.DidSizeChange) && Presenter.State == OverlappedPresenterState.Restored)
        {
            _restoredBounds = new RectInt32(sender.Position.X, sender.Position.Y, sender.Size.Width, sender.Size.Height);
        }
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        SavePlacement();
    }

    private async void OnBackRequested(TitleBar sender, object args)
    {
        await _navigator.GoBackAsync();
    }

    private void OnPaneToggleRequested(TitleBar sender, object args)
    {
        Navigation.IsPaneOpen = !Navigation.IsPaneOpen;
    }

    private async void OnNavigationItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            await _navigator.OpenSettingsAsync();
        }
        else if (ReferenceEquals(args.InvokedItemContainer, HomeItem))
        {
            await _navigator.GoHomeAsync();
        }
        else if (ReferenceEquals(args.InvokedItemContainer, MyIconsItem))
        {
            await _navigator.OpenMyIconsAsync();
        }
        // The click already moved the selection, even when leaving the page was cancelled.
        SyncNavigationSelection();
    }

    private void OnFrameNavigated(object sender, NavigationEventArgs e)
    {
        SyncNavigationSelection();
    }

    private void SyncNavigationSelection()
    {
        Navigation.SelectedItem = ContentFrame.Content switch
        {
            HomePage => HomeItem,
            MyIconsPage => MyIconsItem,
            SettingsPage => Navigation.SettingsItem,
            _ => null,
        };
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Link;
            e.DragUIOverride.Caption = Strings.Get("DropCaption");
        }
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }
        var droppedItems = await e.DataView.GetStorageItemsAsync();
        // A shortcut to a folder opens its folder, and gets the icon too.
        var droppedPath = droppedItems
            .Select(item => item.Path)
            .FirstOrDefault(path => !string.IsNullOrEmpty(path) && (Directory.Exists(path) || ShortcutReader.ReadFolderTarget(path) is not null));
        if (droppedPath is not null)
        {
            await _navigator.OpenFolderAsync(FolderRequest.ForPath(droppedPath));
        }
    }
}
