using IconPicker.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace IconPicker.App.Controls;

/// <summary>Picks a color: a preset swatch, a recent color, or any color from Windows' color picker.</summary>
public sealed partial class ColorPickerButton : UserControl
{
    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(nameof(Color), typeof(Color), typeof(ColorPickerButton), new PropertyMetadata(default(Color), OnColorChanged));

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ColorPickerButton), new PropertyMetadata(""));

    public static readonly DependencyProperty PresetColorsProperty = DependencyProperty.Register(nameof(PresetColors), typeof(IReadOnlyList<ColorSwatchViewModel>), typeof(ColorPickerButton), new PropertyMetadata(null));

    public static readonly DependencyProperty RecentColorsProperty = DependencyProperty.Register(nameof(RecentColors), typeof(IReadOnlyList<ColorSwatchViewModel>), typeof(ColorPickerButton), new PropertyMetadata(null, OnRecentColorsChanged));

    private bool _isSyncingPicker;

    public ColorPickerButton()
    {
        InitializeComponent();
    }

    public Color Color
    {
        get => (Color)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public IReadOnlyList<ColorSwatchViewModel>? PresetColors
    {
        get => (IReadOnlyList<ColorSwatchViewModel>?)GetValue(PresetColorsProperty);
        set => SetValue(PresetColorsProperty, value);
    }

    public IReadOnlyList<ColorSwatchViewModel>? RecentColors
    {
        get => (IReadOnlyList<ColorSwatchViewModel>?)GetValue(RecentColorsProperty);
        set => SetValue(RecentColorsProperty, value);
    }

    public Brush ColorBrush => new SolidColorBrush(Color);

    public bool HasRecentColors => RecentColors is { Count: > 0 };

    private static void OnColorChanged(DependencyObject owner, DependencyPropertyChangedEventArgs eventArgs)
    {
        ((ColorPickerButton)owner).Bindings.Update();
    }

    private static void OnRecentColorsChanged(DependencyObject owner, DependencyPropertyChangedEventArgs eventArgs)
    {
        ((ColorPickerButton)owner).Bindings.Update();
    }

    private void OnFlyoutOpening(object? sender, object eventArgs)
    {
        _isSyncingPicker = true;
        Picker.Color = Color;
        _isSyncingPicker = false;
    }

    private void OnSwatchClick(object sender, ItemClickEventArgs eventArgs)
    {
        if (eventArgs.ClickedItem is ColorSwatchViewModel swatch)
        {
            Color = swatch.Color;
            _isSyncingPicker = true;
            Picker.Color = swatch.Color;
            _isSyncingPicker = false;
        }
    }

    private void OnPickerColorChanged(ColorPicker sender, ColorChangedEventArgs eventArgs)
    {
        if (!_isSyncingPicker)
        {
            Color = Color.FromArgb(255, eventArgs.NewColor.R, eventArgs.NewColor.G, eventArgs.NewColor.B);
        }
    }
}
