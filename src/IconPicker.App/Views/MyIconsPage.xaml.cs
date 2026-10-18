using IconPicker.App.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace IconPicker.App.Views;

public sealed partial class MyIconsPage : Page, IHasUnappliedChanges
{
    public MyIconsPage()
    {
        ViewModel = new MyIconsViewModel(App.Services);
        InitializeComponent();
    }

    public MyIconsViewModel ViewModel { get; }

    public bool HasUnappliedChanges => ViewModel.HasUnappliedChanges;

    public string DiscardChangesTitle => ViewModel.DiscardChangesTitle;

    public string DiscardChangesMessage => ViewModel.DiscardChangesMessage;

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.Dispose();
    }

    private void OnSavedIconSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectedIcon = SavedIconGrid.SelectedItem as SavedIconViewModel;
    }

    private void OnSavedIconDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.EditCommand.CanExecute(null))
        {
            ViewModel.EditCommand.Execute(null);
        }
    }
}
