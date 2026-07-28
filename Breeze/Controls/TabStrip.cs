using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Breeze.Models;

namespace Breeze.Controls;

/// <summary>Tab list that shares the space available to it, and reorders by dragging through
/// <see cref="DragReorder" />.</summary>
public sealed class TabStrip : ListBox
{
    private const double MinTabWidth = 72;
    private const double MaxTabWidth = 190;
    private const double TabGap = 2;

    /// <summary>Width kept free at the end of the strip for the new tab button.</summary>
    public static readonly StyledProperty<double> ReserveProperty =
        AvaloniaProperty.Register<TabStrip, double>(nameof(Reserve));

    private readonly DragReorder _drag;

    private ScrollViewer? _scroller;
    private double _limit = double.PositiveInfinity;
    private double _tabWidth = MaxTabWidth;
    private int _tabCount;

    public TabStrip() =>
        _drag = DragReorder.Attach(this, (from, to) => (DataContext as ITabReorder)?.MoveTab(from, to));

    protected override Type StyleKeyOverride => typeof(ListBox);

    public double Reserve
    {
        get => GetValue(ReserveProperty);
        set => SetValue(ReserveProperty, value);
    }

    /// <summary>Tabs share the space left over after the new tab button, shrinking to a floor
    /// where the strip starts scrolling instead of growing past the available width.</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var limit = double.IsInfinity(availableSize.Width)
            ? double.PositiveInfinity
            : Math.Max(availableSize.Width - Reserve, 0);

        _limit = limit;
        SetTabWidth(limit);

        var size = base.MeasureOverride(new Size(limit, availableSize.Height));
        return double.IsInfinity(limit) ? size : new Size(Math.Min(size.Width, limit), size.Height);
    }

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        container.Transitions = null;
        container.Width = _tabWidth;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Opening or closing tabs does not always re-measure the strip, so resize them here too.
        if (change.Property == ItemCountProperty)
        {
            SetTabWidth(_limit);
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        var scroller = Scroller();
        if (scroller is not null && scroller.Extent.Width > scroller.Viewport.Width)
        {
            var step = e.Delta.Y != 0 ? e.Delta.Y : e.Delta.X;
            scroller.Offset = scroller.Offset - new Vector(step * _tabWidth / 2, 0);
            e.Handled = true;
            return;
        }

        base.OnPointerWheelChanged(e);
    }

    private ScrollViewer? Scroller() =>
        _scroller ??= Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(this)
            .OfType<ScrollViewer>()
            .FirstOrDefault();

    private void SetTabWidth(double limit)
    {
        var count = ItemCount;

        // Whole pixel slots, otherwise layout rounding pushes the total past the available width.
        var width = double.IsInfinity(limit) || count == 0
            ? MaxTabWidth
            : Math.Clamp(Math.Floor(limit / count) - TabGap, MinTabWidth, MaxTabWidth);

        var animate = count != _tabCount;
        _tabCount = count;

        if (Math.Abs(width - _tabWidth) < 0.5)
        {
            return;
        }

        _tabWidth = width;

        for (var i = 0; i < count; i++)
        {
            if (ContainerFromIndex(i) is not Control container)
            {
                continue;
            }

            // Opening or closing a tab eases the new width in; window resizing applies it at once.
            if (!_drag.IsDragging)
            {
                container.Transitions = animate ? WidthTransitions() : null;
            }

            container.Width = width;
        }
    }

    private static Transitions WidthTransitions() =>
    [
        new DoubleTransition
        {
            Property = WidthProperty,
            Duration = TimeSpan.FromMilliseconds(DragReorder.SlideMilliseconds),
            Easing = new CubicEaseOut()
        }
    ];
}
