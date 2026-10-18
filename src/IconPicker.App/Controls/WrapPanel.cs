using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace IconPicker.App.Controls;

/// <summary>Lays its children out left to right, starting a new line when the next one doesn't fit.</summary>
public sealed partial class WrapPanel : Panel
{
    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(nameof(Spacing), typeof(double), typeof(WrapPanel), new PropertyMetadata(0.0, (owner, _) => ((WrapPanel)owner).InvalidateMeasure()));

    /// <summary>The gap between children, across and between lines.</summary>
    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        }
        return Layout(availableSize.Width, arrange: false);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Layout(finalSize.Width, arrange: true);
        return finalSize;
    }

    private Size Layout(double availableWidth, bool arrange)
    {
        double x = 0, y = 0, lineHeight = 0, widest = 0;
        foreach (var child in Children)
        {
            var size = child.DesiredSize;
            if (x > 0 && x + size.Width > availableWidth)
            {
                x = 0;
                y += lineHeight + Spacing;
                lineHeight = 0;
            }
            if (arrange)
            {
                child.Arrange(new Rect(x, y, size.Width, size.Height));
            }
            widest = Math.Max(widest, x + size.Width);
            x += size.Width + Spacing;
            lineHeight = Math.Max(lineHeight, size.Height);
        }
        return new Size(widest, y + lineHeight);
    }
}
