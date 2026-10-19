using IconPicker.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace IconPicker.App.Views;

public sealed partial class SingleModeView : UserControl
{
    /// <summary>The recent icon whose menu is open.</summary>
    private CandidateViewModel? _recentIconForMenu;

    internal SingleModeView(SingleModeViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public SingleModeViewModel ViewModel { get; }

    private void OnRecentIconClick(object sender, ItemClickEventArgs eventArgs)
    {
        if (eventArgs.ClickedItem is CandidateViewModel recentIcon)
        {
            ViewModel.PickRecentIcon(recentIcon);
        }
    }

    /// <summary>A right-click on a recent icon, or the menu key on one: its menu, at the pointer or by the tile.</summary>
    private void OnRecentIconContextRequested(UIElement sender, ContextRequestedEventArgs eventArgs)
    {
        var element = eventArgs.OriginalSource as DependencyObject;
        while (element is not null and not GridViewItem)
        {
            element = VisualTreeHelper.GetParent(element);
        }
        if (element is not GridViewItem container || RecentIconsGrid.ItemFromContainer(container) is not CandidateViewModel recentIcon)
        {
            return;
        }
        _recentIconForMenu = recentIcon;
        var options = new FlyoutShowOptions();
        if (eventArgs.TryGetPosition(container, out var position))
        {
            options.Position = position;
        }
        ((MenuFlyout)Resources["RecentIconMenu"]).ShowAt(container, options);
        eventArgs.Handled = true;
    }

    private async void OnRemoveRecentIconClick(object sender, RoutedEventArgs eventArgs)
    {
        if (_recentIconForMenu is { } recentIcon)
        {
            _recentIconForMenu = null;
            await ViewModel.RemoveRecentIconAsync(recentIcon);
        }
    }
}
