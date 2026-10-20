using IconPicker.App.Services;
using IconPicker.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

namespace IconPicker.App.Controls;

/// <summary>The icon builder, shared by the My icons tab and the dialog opened from a folder's picker. A file dropped on it becomes the glyph, as with Browse.</summary>
public sealed partial class IconBuilderControl : UserControl, IIconFileDropTarget
{
    /// <summary>The preview's 208 px, the 24 px gap and the settings' 300 px: below this, the preview goes above the settings.</summary>
    private const double SideBySideMinimumWidth = 532;

    private bool _isStacked;

    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(nameof(ViewModel), typeof(IconBuilderViewModel), typeof(IconBuilderControl), new PropertyMetadata(null, OnViewModelChanged));

    public IconBuilderControl()
    {
        InitializeComponent();
        // Handled events too: the slider's own parts may take them first.
        IconSizeSlider.AddHandler(PointerWheelChangedEvent, new PointerEventHandler(OnIconSizeWheel), handledEventsToo: true);
        IconSizeSlider.AddHandler(RightTappedEvent, new RightTappedEventHandler(OnIconSizeRightTapped), handledEventsToo: true);
    }

    public IconBuilderViewModel? ViewModel
    {
        get => (IconBuilderViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    bool IIconFileDropTarget.TakesDropOn(DependencyObject element)
    {
        return ViewModel is not null;
    }

    /// <summary>The builder holds one glyph: the first file is used.</summary>
    Task IIconFileDropTarget.DropAsync(DependencyObject element, IReadOnlyList<string> iconFiles)
    {
        return ViewModel?.OpenFileAsync(iconFiles[0]) ?? Task.CompletedTask;
    }

    /// <summary>
    /// Picks the layout from the width offered, before laying out: once squeezed below its minimum, the control would still report that minimum as its size, so its actual size can't tell.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var isStacked = availableSize.Width < SideBySideMinimumWidth;
        if (isStacked != _isStacked)
        {
            _isStacked = isStacked;
            VisualStateManager.GoToState(this, isStacked ? "Stacked" : "SideBySide", useTransitions: false);
        }
        return base.MeasureOverride(availableSize);
    }

    private static void OnViewModelChanged(DependencyObject owner, DependencyPropertyChangedEventArgs eventArgs)
    {
        ((IconBuilderControl)owner).Bindings.Update();
    }

    private void OnIconSizeWheel(object sender, PointerRoutedEventArgs eventArgs)
    {
        var wheelDelta = eventArgs.GetCurrentPoint(IconSizeSlider).Properties.MouseWheelDelta;
        if (wheelDelta != 0)
        {
            ViewModel?.NudgeIconSize(Math.Sign(wheelDelta));
            // The settings pane would scroll otherwise.
            eventArgs.Handled = true;
        }
    }

    private void OnIconSizeRightTapped(object sender, RightTappedRoutedEventArgs eventArgs)
    {
        ViewModel?.ResetIconSize();
        eventArgs.Handled = true;
    }

    private void OnModuleIconClick(object sender, ItemClickEventArgs eventArgs)
    {
        if (eventArgs.ClickedItem is CandidateViewModel moduleIcon)
        {
            ViewModel?.PickModuleIcon(moduleIcon);
        }
    }
}
