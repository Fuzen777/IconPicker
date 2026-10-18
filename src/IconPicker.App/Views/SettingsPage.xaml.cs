using IconPicker.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace IconPicker.App.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        ViewModel = new SettingsViewModel(App.Services);
        InitializeComponent();
    }

    public SettingsViewModel ViewModel { get; }
}
