using IconPicker.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace IconPicker.App.Views;

public sealed partial class BatchModeView : UserControl
{
    internal BatchModeView(BatchModeViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        // "Rescan all" shows the rows under a header per library; a single folder lists them directly.
        RowsSource.IsSourceGrouped = viewModel.IsGrouped;
        RowsSource.Source = viewModel.IsGrouped ? viewModel.VisibleGroups : viewModel.VisibleRows;
        RowList.ItemsSource = RowsSource.View;
    }

    public BatchModeViewModel ViewModel { get; }

    private void OnRowSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectedRow = RowList.SelectedItem as BatchRowViewModel;
    }
}
