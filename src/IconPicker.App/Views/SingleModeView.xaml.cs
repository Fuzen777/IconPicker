using IconPicker.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace IconPicker.App.Views;

public sealed partial class SingleModeView : UserControl
{
    internal SingleModeView(SingleModeViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public SingleModeViewModel ViewModel { get; }
}
