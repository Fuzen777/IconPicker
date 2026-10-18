using IconPicker.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IconPicker.App.Controls;

/// <summary>The icon builder, shared by the My icons tab and the dialog opened from a folder's picker.</summary>
public sealed partial class IconBuilderControl : UserControl
{
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
