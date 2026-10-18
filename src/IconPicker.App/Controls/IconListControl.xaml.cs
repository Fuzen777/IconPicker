using IconPicker.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IconPicker.App.Controls;

/// <summary>Saved icons, then the built-in icons, to pick from. Picking happens on click or Enter, never on selection, so arrow keys only move.</summary>
public sealed partial class IconListControl : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(nameof(ViewModel), typeof(IconListViewModel), typeof(IconListControl), new PropertyMetadata(null, OnViewModelChanged));

    public IconListControl()
    {
        InitializeComponent();
        ActualThemeChanged += (_, _) => SyncGlyphColor();
    }

    public IconListViewModel? ViewModel
    {
        get => (IconListViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelChanged(DependencyObject owner, DependencyPropertyChangedEventArgs eventArgs)
    {
        var control = (IconListControl)owner;
        control.SyncGlyphColor();
        control.Bindings.Update();
    }

    /// <summary>Glyphs are drawn in the theme's text color, so they show on dark backgrounds too.</summary>
    private void SyncGlyphColor()
    {
        if (ViewModel is not null)
        {
            ViewModel.GlyphColor = ActualTheme == ElementTheme.Dark ? "#FFFFFF" : "#000000";
        }
    }

    private void OnBuiltInContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.Item is not BuiltInIconViewModel builtInIcon)
        {
            return;
        }
        if (args.InRecycleQueue)
        {
            builtInIcon.ReleaseThumbnail();
        }
        else if (builtInIcon.Thumbnail is null)
        {
            builtInIcon.LoadThumbnail();
        }
    }

    private void OnBuiltInIconClick(object sender, ItemClickEventArgs eventArgs)
    {
        if (eventArgs.ClickedItem is BuiltInIconViewModel builtInIcon)
        {
            ViewModel?.PickBuiltInIcon(builtInIcon);
        }
    }

    private void OnSavedIconClick(object sender, ItemClickEventArgs eventArgs)
    {
        if (eventArgs.ClickedItem is SavedIconViewModel savedIcon)
        {
            ViewModel?.PickSavedIcon(savedIcon);
        }
    }
}
