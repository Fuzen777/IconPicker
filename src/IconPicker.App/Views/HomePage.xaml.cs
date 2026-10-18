using IconPicker.App.Services;
using IconPicker.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IconPicker.App.Views;

public sealed partial class HomePage : Page
{
    private readonly PickerService _pickers = App.Services.GetRequiredService<PickerService>();
    private readonly Navigator _navigator = App.Services.GetRequiredService<Navigator>();

    public HomePage()
    {
        Libraries = new LibrariesViewModel(App.Services, isForRescanning: true);
        InitializeComponent();
    }

    public LibrariesViewModel Libraries { get; }

    private async void OnChooseFolderClick(object sender, RoutedEventArgs e)
    {
        var folderPath = await _pickers.PickFolderAsync();
        if (folderPath is not null)
        {
            await _navigator.OpenFolderAsync(FolderRequest.ForFolder(folderPath));
        }
    }
}
