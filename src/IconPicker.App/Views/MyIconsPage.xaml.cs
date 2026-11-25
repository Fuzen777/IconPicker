using IconPicker.App.Services;
using IconPicker.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using ICommand = System.Windows.Input.ICommand;

namespace IconPicker.App.Views;

public sealed partial class MyIconsPage : Page, IHasUnappliedChanges, IIconFileDropTarget, IIconFilePasteTarget
{
    /// <summary>The builder's own minimum (see IconBuilderControl), kept free when the list is widened.</summary>
    private const double BuilderMinimumWidth = 300;

    /// <summary>One saved icon's tile and the gap after it: the list widens by whole tiles, one more to a row each step.</summary>
    private const double TilePitch = 116;

    /// <summary>The list's width besides its tiles, so three to a row make the default width.</summary>
    private const double ListOffset = AppSettings.DefaultMyIconsListWidth - (3 * TilePitch);

    private readonly SettingsStore _settings;

    public MyIconsPage()
    {
        ViewModel = new MyIconsViewModel(App.Services);
        _settings = App.Services.GetRequiredService<SettingsStore>();
        InitializeComponent();
        ListSizer.StepWidth = TilePitch;
        ListSizer.StepOffset = ListOffset;
        ListSizer.TargetColumn = ListColumn;
        ListSizer.DefaultWidth = AppSettings.DefaultMyIconsListWidth;
        ListColumn.MinWidth = ListOffset + TilePitch;
        ListColumn.Width = new GridLength(ListSizer.SnapToStep(_settings.Current.MyIconsListWidth));
    }

    public MyIconsViewModel ViewModel { get; }

    public bool HasUnappliedChanges => ViewModel.HasUnappliedChanges;

    public string DiscardChangesTitle => ViewModel.DiscardChangesTitle;

    public string DiscardChangesMessage => ViewModel.DiscardChangesMessage;

    /// <summary>The saved icons take dropped files as new icons; the builder takes them itself.</summary>
    bool IIconFileDropTarget.TakesDropOn(DependencyObject element)
    {
        return IconFileDrop.IsInside(element, SavedIconsPanel);
    }

    /// <summary>A pasted file becomes the builder's glyph, as a drop on the builder would.</summary>
    async Task<bool> IIconFilePasteTarget.PasteAsync(IReadOnlyList<string> iconFiles)
    {
        await ViewModel.Builder.OpenFileAsync(iconFiles[0]);
        return true;
    }

    /// <summary>A new icon from the first file, asking first when the builder has unsaved changes.</summary>
    Task IIconFileDropTarget.DropAsync(DependencyObject element, IReadOnlyList<string> iconFiles)
    {
        return ViewModel.NewIconFromFileAsync(iconFiles[0]);
    }

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
        ListColumn.MaxWidth = Math.Max(ListColumn.MinWidth, ListSizer.SnapDownToStep(eventArgs.NewSize.Width - padding - gaps - BuilderMinimumWidth));
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

    /// <summary>A right-click on a saved icon, or the menu key on one: selects it, so the menu acts on it as the toolbar would, and opens its menu at the pointer or by the tile.</summary>
    private void OnSavedIconContextRequested(UIElement sender, ContextRequestedEventArgs eventArgs)
    {
        var element = eventArgs.OriginalSource as DependencyObject;
        while (element is not null and not GridViewItem)
        {
            element = VisualTreeHelper.GetParent(element);
        }
        if (element is not GridViewItem container || SavedIconGrid.ItemFromContainer(container) is not SavedIconViewModel savedIcon)
        {
            return;
        }
        SavedIconGrid.SelectedItem = savedIcon;
        ViewModel.SelectedIcon = savedIcon;
        var options = new FlyoutShowOptions();
        if (eventArgs.TryGetPosition(container, out var position))
        {
            options.Position = position;
        }
        ((MenuFlyout)Resources["SavedIconMenu"]).ShowAt(container, options);
        eventArgs.Handled = true;
    }

    private void OnEditIconClick(object sender, RoutedEventArgs eventArgs)
    {
        Run(ViewModel.EditCommand);
    }

    private void OnDuplicateIconClick(object sender, RoutedEventArgs eventArgs)
    {
        Run(ViewModel.DuplicateCommand);
    }

    private void OnRenameIconClick(object sender, RoutedEventArgs eventArgs)
    {
        Run(ViewModel.RenameCommand);
    }

    private void OnDeleteIconClick(object sender, RoutedEventArgs eventArgs)
    {
        Run(ViewModel.DeleteCommand);
    }

    private void OnCopyIconPathClick(object sender, RoutedEventArgs eventArgs)
    {
        Run(ViewModel.CopyIconPathCommand);
    }

    private void OnCopyFolderPathClick(object sender, RoutedEventArgs eventArgs)
    {
        Run(ViewModel.CopyFolderPathCommand);
    }

    private void OnCopyIconClick(object sender, RoutedEventArgs eventArgs)
    {
        Run(ViewModel.CopyIconCommand);
    }

    private static void Run(ICommand command)
    {
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }
}
