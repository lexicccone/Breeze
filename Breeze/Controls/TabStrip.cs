using System.Globalization;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Breeze.Models;

namespace Breeze.Controls;

/// <summary>Tab list with direct manipulation dragging: the pressed tab follows the pointer,
/// neighbours slide aside, and the list order is committed once on release.</summary>
public sealed class TabStrip : ListBox
{
    private const double DragThreshold = 6;
    private const int SlideMilliseconds = 140;
    private const double MinTabWidth = 72;
    private const double MaxTabWidth = 190;
    private const double TabGap = 2;

    /// <summary>Width kept free at the end of the strip for the new tab button.</summary>
    public static readonly StyledProperty<double> ReserveProperty =
        AvaloniaProperty.Register<TabStrip, double>(nameof(Reserve));

    private static readonly ITransform Rest = Translate(0);

    private ScrollViewer? _scroller;
    private double _limit = double.PositiveInfinity;
    private double _tabWidth = MaxTabWidth;
    private int _tabCount;
    private double _pressX;
    private int _from = -1;
    private int _to = -1;
    private double _slot;
    private bool _dragging;

    public TabStrip()
    {
        // Items mark the press as handled while selecting, so the strip listens ahead of them
        // on the tunnel pass and keeps receiving moves even after they are handled.
        AddHandler(PointerPressedEvent, OnStripPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnStripPointerMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnStripPointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerCaptureLostEvent, OnStripCaptureLost);
    }

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
            if (!_dragging)
            {
                container.Transitions = animate ? WidthTransitions() : null;
            }

            container.Width = width;
        }
    }

    private void OnStripPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _pressX = e.GetPosition(this).X;
        _from = IndexAt(_pressX);
        _to = _from;
        _dragging = false;
    }

    private void OnStripPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_from < 0)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            Cancel();
            return;
        }

        if (!_dragging && Math.Abs(point.Position.X - _pressX) < DragThreshold)
        {
            return;
        }

        if (!_dragging && !BeginDrag(e))
        {
            return;
        }

        Track(point.Position.X);
    }

    private void OnStripPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging)
        {
            Reset();
            return;
        }

        // Commit first: releasing the capture raises PointerCaptureLost, which would otherwise
        // cancel the drag before the new order is applied.
        Commit(Offset(e.GetPosition(this).X));
        Reset();
        e.Pointer.Capture(null);
    }

    private void OnStripCaptureLost(object? sender, PointerCaptureLostEventArgs e) => Cancel();

    private bool BeginDrag(PointerEventArgs e)
    {
        _slot = SlotWidth();
        if (_slot <= 0)
        {
            _from = -1;
            return false;
        }

        _dragging = true;
        e.Pointer.Capture(this);

        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is not Control container)
            {
                continue;
            }

            // The dragged tab tracks the pointer exactly; the others ease into place.
            container.Transitions = i == _from ? null : Slide();
            container.RenderTransform = Rest;
            container.ZIndex = i == _from ? 1 : 0;
        }

        return true;
    }

    private void Track(double x)
    {
        var offset = Offset(x);
        _to = Math.Clamp(_from + (int)Math.Round(offset / _slot), 0, ItemCount - 1);

        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is Control container)
            {
                container.RenderTransform = Translate(Displacement(i, offset));
            }
        }
    }

    /// <summary>How far a tab is pushed from its slot for the current drag offset.</summary>
    private double Displacement(int index, double offset)
    {
        if (index == _from)
        {
            return offset;
        }

        if (_to > _from && index > _from && index <= _to)
        {
            return -_slot;
        }

        return _to < _from && index >= _to && index < _from ? _slot : 0;
    }

    private void Commit(double offset)
    {
        var to = _to;
        var residual = offset - ((to - _from) * _slot);

        if (to != _from && DataContext is ITabReorder tabs)
        {
            tabs.MoveTab(_from, to);
        }

        ClearOffsets();

        // Layout has the tab in its new slot now; start from where the pointer left it and
        // ease the remainder so the drop does not snap.
        if (Math.Abs(residual) >= 1 && ContainerFromIndex(to) is Control settled)
        {
            settled.Transitions = null;
            settled.RenderTransform = Translate(residual);
            Dispatcher.UIThread.Post(() =>
            {
                settled.Transitions = Slide();
                settled.RenderTransform = Rest;
            }, DispatcherPriority.Render);
        }
    }

    private void Cancel()
    {
        if (_dragging)
        {
            ClearOffsets();
        }

        Reset();
    }

    private void ClearOffsets()
    {
        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is Control container)
            {
                container.Transitions = null;
                container.RenderTransform = null;
                container.ZIndex = 0;
            }
        }
    }

    private void Reset()
    {
        _from = -1;
        _to = -1;
        _dragging = false;
    }

    private double Offset(double x) =>
        Math.Clamp(x - _pressX, -_from * _slot, (ItemCount - 1 - _from) * _slot);

    private int IndexAt(double x)
    {
        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is Visual container &&
                container.TranslatePoint(default, this) is { } origin &&
                x >= origin.X && x <= origin.X + container.Bounds.Width)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Horizontal distance between neighbouring tabs, zero while a single tab is open.</summary>
    private double SlotWidth()
    {
        if (ItemCount < 2 ||
            ContainerFromIndex(0) is not Visual first ||
            ContainerFromIndex(1) is not Visual second ||
            first.TranslatePoint(default, this) is not { } left ||
            second.TranslatePoint(default, this) is not { } right)
        {
            return 0;
        }

        return Math.Abs(right.X - left.X);
    }

    private static Transitions WidthTransitions() =>
    [
        new DoubleTransition
        {
            Property = WidthProperty,
            Duration = TimeSpan.FromMilliseconds(SlideMilliseconds),
            Easing = new CubicEaseOut()
        }
    ];

    private static Transitions Slide() =>
    [
        new TransformOperationsTransition
        {
            Property = RenderTransformProperty,
            Duration = TimeSpan.FromMilliseconds(SlideMilliseconds),
            Easing = new CubicEaseOut()
        }
    ];

    private static ITransform Translate(double x) =>
        TransformOperations.Parse(string.Create(CultureInfo.InvariantCulture, $"translateX({x}px)"));
}
