using Microsoft.UI;
using Microsoft.UI.Xaml;

namespace IconPicker.App.Services;

/// <summary>The main window, for services that need it to show dialogs and pickers.</summary>
internal sealed class WindowContext
{
    private MainWindow? _window;

    public MainWindow Window => _window ?? throw new InvalidOperationException("The main window isn't open yet.");

    public XamlRoot XamlRoot => Window.Content.XamlRoot;

    public WindowId WindowId => Window.AppWindow.Id;

    public void Attach(MainWindow window)
    {
        _window = window;
    }
}
