using IconPicker.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IconPicker.App.Controls;

/// <summary>The remembered libraries, with Rescan on Home and Remove in Settings.</summary>
public sealed partial class LibraryListControl : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(nameof(ViewModel), typeof(LibrariesViewModel), typeof(LibraryListControl), new PropertyMetadata(null));

    public LibraryListControl()
    {
        InitializeComponent();
    }

    public LibrariesViewModel? ViewModel
    {
        get => (LibrariesViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private async void OnRescanClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null && ((FrameworkElement)sender).DataContext is LibraryItemViewModel library)
        {
            await ViewModel.RescanAsync(library);
        }
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null && ((FrameworkElement)sender).DataContext is LibraryItemViewModel library)
        {
            ViewModel.Remove(library);
        }
    }
}
