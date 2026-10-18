using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace IconPicker.App.ViewModels;

/// <summary>One color to pick in a color button's flyout.</summary>
public sealed class ColorSwatchViewModel(Color color)
{
    public Color Color { get; } = color;

    public Brush Brush { get; } = new SolidColorBrush(color);

    /// <summary>The color as #RRGGBB, which screen readers read for the swatch.</summary>
    public string Name { get; } = ColorText.ToHex(color);

    public override string ToString()
    {
        return Name;
    }
}
