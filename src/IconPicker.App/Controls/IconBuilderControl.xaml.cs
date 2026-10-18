using IconPicker.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace IconPicker.App.Controls;

/// <summary>The icon builder, shared by the My icons tab and the dialog opened from a folder's picker.</summary>
public sealed partial class IconBuilderControl : UserControl
{
    /// <summary>The preview's 208 px, the 24 px gap and the settings' 300 px: below this, the preview goes above the settings.</summary>
    private const double SideBySideMinimumWidth = 532;

    private bool _isStacked;

    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(nameof(ViewModel), typeof(IconBuilderViewModel), typeof(IconBuilderControl), new PropertyMetadata(null, OnViewModelChanged));

    public IconBuilderControl()
    {
        InitializeComponent();
    }

    public IconBuilderViewModel? ViewModel
    {
        get => (IconBuilderViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
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

    private void OnModuleIconClick(object sender, ItemClickEventArgs eventArgs)
    {
        if (eventArgs.ClickedItem is CandidateViewModel moduleIcon)
        {
            ViewModel?.PickModuleIcon(moduleIcon);
        }
    }
}
