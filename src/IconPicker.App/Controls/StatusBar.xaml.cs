using IconPicker.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IconPicker.App.Controls;

/// <summary>The message bar shown at the top of single and batch mode.</summary>
public sealed partial class StatusBar : UserControl
{
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(nameof(Status), typeof(StatusMessage), typeof(StatusBar), new PropertyMetadata(null));

    public StatusBar()
    {
        InitializeComponent();
    }

    public StatusMessage? Status
    {
        get => (StatusMessage?)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }
}
