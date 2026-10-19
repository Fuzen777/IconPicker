using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.System;

namespace IconPicker.App.Controls;

/// <summary>
/// A divider between two grid columns that resizes the column before it: dragged with the mouse, or moved with the arrow keys once focused.
/// The width stays within the column's MinWidth and MaxWidth.
/// </summary>
public sealed partial class ColumnSizer : Grid
{
    private const double KeyboardStep = 16;

    private readonly Rectangle _grip;
    private double _dragStartX;
    private double _dragStartWidth;
    private bool _isDragging;

    public ColumnSizer()
    {
        Width = 16;
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        IsTabStop = true;
        UseSystemFocusVisuals = true;
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
        Children.Add(new Rectangle
        {
            Width = 1,
            HorizontalAlignment = HorizontalAlignment.Center,
            Fill = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
        });
        // Shown while pointed at or dragged, so the divider reads as something to grab.
        _grip = new Rectangle
        {
            Width = 4,
            Height = 32,
            RadiusX = 2,
            RadiusY = 2,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Fill = (Brush)Application.Current.Resources["ControlStrongFillColorDefaultBrush"],
            Opacity = 0,
        };
        Children.Add(_grip);
        PointerEntered += (_, _) => _grip.Opacity = 1;
        PointerExited += (_, _) => _grip.Opacity = _isDragging ? 1 : 0;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += (_, _) => EndDrag();
        KeyDown += OnKeyDown;
    }

    /// <summary>The column the divider resizes, the one before it.</summary>
    public ColumnDefinition? TargetColumn { get; set; }

    /// <summary>A drag or a key press finished resizing, so the new width can be kept.</summary>
    public event EventHandler? ResizeCompleted;

    private void OnPointerPressed(object sender, PointerRoutedEventArgs eventArgs)
    {
        if (TargetColumn is null || !CapturePointer(eventArgs.Pointer))
        {
            return;
        }
        _isDragging = true;
        _dragStartX = eventArgs.GetCurrentPoint(null).Position.X;
        _dragStartWidth = TargetColumn.ActualWidth;
        eventArgs.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs eventArgs)
    {
        if (_isDragging)
        {
            Resize(_dragStartWidth + eventArgs.GetCurrentPoint(null).Position.X - _dragStartX);
        }
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs eventArgs)
    {
        ReleasePointerCapture(eventArgs.Pointer);
        EndDrag();
    }

    private void EndDrag()
    {
        if (!_isDragging)
        {
            return;
        }
        _isDragging = false;
        _grip.Opacity = 0;
        ResizeCompleted?.Invoke(this, EventArgs.Empty);
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs eventArgs)
    {
        if (TargetColumn is null || eventArgs.Key is not (VirtualKey.Left or VirtualKey.Right))
        {
            return;
        }
        Resize(TargetColumn.ActualWidth + (eventArgs.Key == VirtualKey.Right ? KeyboardStep : -KeyboardStep));
        ResizeCompleted?.Invoke(this, EventArgs.Empty);
        eventArgs.Handled = true;
    }

    private void Resize(double width)
    {
        if (TargetColumn is not null)
        {
            TargetColumn.Width = new GridLength(Math.Clamp(width, TargetColumn.MinWidth, Math.Max(TargetColumn.MinWidth, TargetColumn.MaxWidth)));
        }
    }
}
