using IconPicker.App.Services;
using IconPicker.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace IconPicker.App.Views;

/// <summary>"Batch": the rows, and the selected row's picker. Files dropped on a row go to that row's picker.</summary>
public sealed partial class BatchModeView : UserControl, IIconFileDropTarget
{
    /// <summary>Room the rows keep however wide the picker is made: a checkbox, both icons and a name.</summary>
    private const double RowsMinimumWidth = 300;

    /// <summary>One icon tile and the gap after it: the picker widens by whole tiles, one more to a row each step.</summary>
    private const double TilePitch = 128;

    /// <summary>The picker's width besides its tiles (its padding and border, and room for the scrollbar), so three to a row make the default width.</summary>
    private const double PanelOffset = AppSettings.DefaultPickerPanelWidth - (3 * TilePitch);

    private readonly SettingsStore _settings;

    internal BatchModeView(BatchModeViewModel viewModel)
    {
        ViewModel = viewModel;
        _settings = App.Services.GetRequiredService<SettingsStore>();
        InitializeComponent();
        PanelSizer.StepWidth = TilePitch;
        PanelSizer.StepOffset = PanelOffset;
        PanelSizer.TargetColumn = PanelColumn;
        PanelSizer.ResizesFollowingColumn = true;
        PanelSizer.DefaultWidth = AppSettings.DefaultPickerPanelWidth;
        PanelColumn.MinWidth = PanelOffset + TilePitch;
        PanelColumn.Width = new GridLength(PanelSizer.SnapToStep(_settings.Current.PickerPanelWidth));
        // "Rescan all" shows the rows under a header per library; a single folder lists them directly.
        RowsSource.IsSourceGrouped = viewModel.IsGrouped;
        RowsSource.Source = viewModel.IsGrouped ? viewModel.VisibleGroups : viewModel.VisibleRows;
        RowList.ItemsSource = RowsSource.View;
    }

    public BatchModeViewModel ViewModel { get; }

    bool IIconFileDropTarget.TakesDropOn(DependencyObject element)
    {
        return FindRow(element) is not null;
    }

    /// <summary>Selects the row the files were dropped on, so its picker shows them, and adds them there; picking one ticks the row.</summary>
    async Task IIconFileDropTarget.DropAsync(DependencyObject element, IReadOnlyList<string> iconFiles)
    {
        if (FindRow(element) is { } row)
        {
            RowList.SelectedItem = row;
            await row.Picker.AddFilesAsync(iconFiles);
        }
    }

    /// <summary>The row an element belongs to, or null outside the rows.</summary>
    private BatchRowViewModel? FindRow(DependencyObject element)
    {
        for (var current = element; current is not null && !ReferenceEquals(current, RowList); current = VisualTreeHelper.GetParent(current))
        {
            if (current is ListViewItem container)
            {
                return RowList.ItemFromContainer(container) as BatchRowViewModel;
            }
        }
        return null;
    }

    /// <summary>However wide the picker was made, the rows keep their minimum; a narrower window shrinks the picker for now, and widening gives it back.</summary>
    private void OnViewSizeChanged(object sender, SizeChangedEventArgs eventArgs)
    {
        var gaps = PanelSizer.Width + (2 * ViewGrid.ColumnSpacing);
        PanelColumn.MaxWidth = Math.Max(PanelColumn.MinWidth, PanelSizer.SnapDownToStep(eventArgs.NewSize.Width - gaps - RowsMinimumWidth));
    }

    private void OnPanelResized(object? sender, EventArgs eventArgs)
    {
        var width = Math.Round(PanelColumn.Width.Value);
        _settings.Update(settings => settings.PickerPanelWidth = width);
    }

    private void OnRowSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectedRow = RowList.SelectedItem as BatchRowViewModel;
    }
}
