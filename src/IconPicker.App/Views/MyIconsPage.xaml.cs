using IconPicker.App.Services;
using IconPicker.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace IconPicker.App.Views;

public sealed partial class MyIconsPage : Page, IHasUnappliedChanges
{
    /// <summary>The builder's own minimum (see IconBuilderControl), kept free when the list is widened.</summary>
    private const double BuilderMinimumWidth = 300;

    private readonly SettingsStore _settings;

    public MyIconsPage()
    {
        ViewModel = new MyIconsViewModel(App.Services);
        _settings = App.Services.GetRequiredService<SettingsStore>();
        InitializeComponent();
        ListColumn.Width = new GridLength(_settings.Current.MyIconsListWidth);
        ListSizer.TargetColumn = ListColumn;
        ListSizer.DefaultWidth = AppSettings.DefaultMyIconsListWidth;
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

    /// <summary>However wide the list was made, the builder keeps its minimum; a narrower window shrinks the list for now, and widening gives it back.</summary>
    private void OnPageSizeChanged(object sender, SizeChangedEventArgs eventArgs)
    {
        var padding = PageGrid.Padding.Left + PageGrid.Padding.Right;
        var gaps = ListSizer.Width + (2 * PageGrid.ColumnSpacing);
        ListColumn.MaxWidth = Math.Max(ListColumn.MinWidth, eventArgs.NewSize.Width - padding - gaps - BuilderMinimumWidth);
    }

    private void OnListResized(object? sender, EventArgs eventArgs)
    {
        var width = Math.Round(ListColumn.Width.Value);
        _settings.Update(settings => settings.MyIconsListWidth = width);
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
