using IconPicker.App.Services;
using IconPicker.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace IconPicker.App.Views;

public sealed partial class SingleModeView : UserControl
{
    /// <summary>Room the picker keeps however wide the column beside it is made: two icon tiles and a scrollbar.</summary>
    private const double PickerMinimumWidth = 300;

    /// <summary>One recent icon's tile and the gap after it: the column widens by whole tiles, one more recent icon to a row each step.</summary>
    private const double RecentIconPitch = 60;

    /// <summary>The column's narrowest: five recent icons to a row, which also fits the current and new icons side by side.</summary>
    private const int MinimumRecentIconsPerRow = 5;

    /// <summary>The column's width besides its recent icons (the scrollbar's room, and slack), so five to a row make the default width.</summary>
    private const double SideOffset = AppSettings.DefaultSingleModeColumnWidth - (MinimumRecentIconsPerRow * RecentIconPitch);

    private readonly SettingsStore _settings;

    /// <summary>The recent icon whose menu is open.</summary>
    private CandidateViewModel? _recentIconForMenu;

    internal SingleModeView(SingleModeViewModel viewModel)
    {
        ViewModel = viewModel;
        _settings = App.Services.GetRequiredService<SettingsStore>();
        InitializeComponent();
        SideSizer.StepWidth = RecentIconPitch;
        SideSizer.StepOffset = SideOffset;
        SideSizer.TargetColumn = SideColumn;
        SideSizer.DefaultWidth = AppSettings.DefaultSingleModeColumnWidth;
        SideColumn.MinWidth = AppSettings.DefaultSingleModeColumnWidth;
        SideColumn.Width = new GridLength(Math.Max(SideColumn.MinWidth, SideSizer.SnapToStep(_settings.Current.SingleModeColumnWidth)));
    }

    public SingleModeViewModel ViewModel { get; }

    /// <summary>However wide the column was made, the picker keeps its minimum; a narrower window shrinks the column for now, and widening gives it back.</summary>
    private void OnViewSizeChanged(object sender, SizeChangedEventArgs eventArgs)
    {
        var gaps = SideSizer.Width + (2 * ViewGrid.ColumnSpacing);
        SideColumn.MaxWidth = Math.Max(SideColumn.MinWidth, SideSizer.SnapDownToStep(eventArgs.NewSize.Width - gaps - PickerMinimumWidth));
    }

    private void OnSideResized(object? sender, EventArgs eventArgs)
    {
        var width = Math.Round(SideColumn.Width.Value);
        _settings.Update(settings => settings.SingleModeColumnWidth = width);
    }

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
