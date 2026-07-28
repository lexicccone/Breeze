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

namespace Breeze.Controls;

/// <summary>Direct manipulation reordering for a horizontally arranged items control: the pressed
/// item follows the pointer exactly, its neighbours ease aside as it passes them, and the new order
/// is applied once, on release. Shared by the tab strip and the bookmark bar, so items may differ
/// in width; every position is measured at the start of a drag rather than assumed.</summary>
internal sealed class DragReorder
{
    /// <summary>How long a neighbour takes to slide aside, and the drop to settle.</summary>
    public const int SlideMilliseconds = 140;

    /// <summary>Movement below this is a click, not a drag.</summary>
    private const double DragThreshold = 6;

    private static readonly ITransform Rest = Translate(0);

    private readonly ItemsControl _owner;
    private readonly Action<int, int> _move;

    /// <summary>Left edge and width of every item, relative to the owner, captured when the drag
    /// starts. An item no panel has realized yet is left as NaN and never dragged past.</summary>
    private double[] _origins = [];
    private double[] _widths = [];

    private double _pressX;
    private double _pitch;
    private int _from = -1;
    private int _to = -1;

    private DragReorder(ItemsControl owner, Action<int, int> move)
    {
        _owner = owner;
        _move = move;

        // Items mark the press as handled while selecting or clicking, so the owner listens ahead
        // of them on the tunnel pass and keeps receiving moves even after they are handled.
        owner.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
        owner.AddHandler(InputElement.PointerMovedEvent, OnMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        owner.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        owner.AddHandler(InputElement.PointerCaptureLostEvent, OnCaptureLost);
    }

    /// <summary>Adds dragging to an items control. <paramref name="move" /> receives the old and new
    /// index once, on a successful drop.</summary>
    public static DragReorder Attach(ItemsControl owner, Action<int, int> move) => new(owner, move);

    public bool IsDragging { get; private set; }

    /// <summary>Eases a render transform, for neighbours sliding aside and for the drop.</summary>
    public static Transitions Slide() =>
    [
        new TransformOperationsTransition
        {
            Property = Visual.RenderTransformProperty,
            Duration = TimeSpan.FromMilliseconds(SlideMilliseconds),
            Easing = new CubicEaseOut()
        }
    ];

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(_owner).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _pressX = e.GetPosition(_owner).X;
        _from = IndexAt(_pressX);
        _to = _from;
        IsDragging = false;
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (_from < 0)
        {
            return;
        }

        var point = e.GetCurrentPoint(_owner);
        if (!point.Properties.IsLeftButtonPressed)
        {
            Cancel();
            return;
        }

        if (!IsDragging && Math.Abs(point.Position.X - _pressX) < DragThreshold)
        {
            return;
        }

        if (!IsDragging && !Begin(e))
        {
            return;
        }

        Track(point.Position.X);
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!IsDragging)
        {
            Reset();
            return;
        }

        // Commit first: releasing the capture raises PointerCaptureLost, which would otherwise
        // cancel the drag before the new order is applied.
        Commit(Offset(e.GetPosition(_owner).X));
        Reset();
        e.Pointer.Capture(null);
    }

    private void OnCaptureLost(object? sender, PointerCaptureLostEventArgs e) => Cancel();

    private bool Begin(PointerEventArgs e)
    {
        if (!Measure())
        {
            _from = -1;
            return false;
        }

        IsDragging = true;
        e.Pointer.Capture(_owner);

        for (var i = 0; i < _owner.ItemCount; i++)
        {
            if (_owner.ContainerFromIndex(i) is not Control container)
            {
                continue;
            }

            // The dragged item tracks the pointer exactly; the others ease into place.
            container.Transitions = i == _from ? null : Slide();
            container.RenderTransform = Rest;
            container.ZIndex = i == _from ? 1 : 0;
        }

        return true;
    }

    private bool Measure()
    {
        var count = _owner.ItemCount;
        _origins = new double[count];
        _widths = new double[count];
        var realized = 0;

        for (var i = 0; i < count; i++)
        {
            if (_owner.ContainerFromIndex(i) is Visual container &&
                container.Bounds.Width > 0 &&
                container.TranslatePoint(default, _owner) is { } origin)
            {
                _origins[i] = origin.X;
                _widths[i] = container.Bounds.Width;
                realized++;
            }
            else
            {
                _origins[i] = double.NaN;
            }
        }

        if (count < 2 || realized < 2 || _from >= count || double.IsNaN(_origins[_from]))
        {
            return false;
        }

        _pitch = _widths[_from] + Gap();
        return _pitch > 0;
    }

    /// <summary>Spacing between neighbouring items, taken from the first realized pair.</summary>
    private double Gap()
    {
        for (var i = 0; i + 1 < _origins.Length; i++)
        {
            if (!double.IsNaN(_origins[i]) && !double.IsNaN(_origins[i + 1]))
            {
                return Math.Max(_origins[i + 1] - _origins[i] - _widths[i], 0);
            }
        }

        return 0;
    }

    private void Track(double x)
    {
        var offset = Offset(x);
        _to = Target(offset);

        for (var i = 0; i < _owner.ItemCount; i++)
        {
            if (_owner.ContainerFromIndex(i) is Control container)
            {
                container.RenderTransform = Translate(Displacement(i, offset));
            }
        }
    }

    /// <summary>Where the dragged item would land: the last neighbour whose middle its leading edge
    /// has passed. The leading edge rather than the centre, because a wide item dragged against a
    /// narrower one at the end of the row could never reach that last place otherwise.</summary>
    private int Target(double offset)
    {
        var target = _from;

        if (offset > 0)
        {
            var leading = _origins[_from] + _widths[_from] + offset;

            while (target + 1 < _origins.Length &&
                   !double.IsNaN(_origins[target + 1]) &&
                   leading > _origins[target + 1] + (_widths[target + 1] / 2))
            {
                target++;
            }
        }
        else
        {
            var leading = _origins[_from] + offset;

            while (target - 1 >= 0 &&
                   !double.IsNaN(_origins[target - 1]) &&
                   leading < _origins[target - 1] + (_widths[target - 1] / 2))
            {
                target--;
            }
        }

        return target;
    }

    /// <summary>How far an item is pushed from its place for the current drag offset.</summary>
    private double Displacement(int index, double offset)
    {
        if (index == _from)
        {
            return offset;
        }

        if (_to > _from && index > _from && index <= _to)
        {
            return -_pitch;
        }

        return _to < _from && index >= _to && index < _from ? _pitch : 0;
    }

    private void Commit(double offset)
    {
        var to = _to;
        var residual = Residual(to, offset);

        if (to != _from)
        {
            _move(_from, to);
        }

        ClearOffsets();

        // Layout has the item in its new place now; start from where the pointer left it and ease
        // the remainder, so the drop settles instead of snapping.
        if (Math.Abs(residual) >= 1 && _owner.ContainerFromIndex(to) is Control settled)
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

    /// <summary>Distance between where the pointer left the item and where layout will put it.</summary>
    private double Residual(int to, double offset)
    {
        var landing = to > _from
            ? _origins[to] + _widths[to] - _widths[_from]
            : _origins[to];

        return double.IsNaN(landing) ? 0 : offset - (landing - _origins[_from]);
    }

    private void Cancel()
    {
        if (IsDragging)
        {
            ClearOffsets();
        }

        Reset();
    }

    private void ClearOffsets()
    {
        for (var i = 0; i < _owner.ItemCount; i++)
        {
            if (_owner.ContainerFromIndex(i) is Control container)
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
        IsDragging = false;
    }

    /// <summary>Pointer travel, clamped so the item cannot be dragged beyond either end.</summary>
    private double Offset(double x)
    {
        var travel = x - _pressX;

        if (_from < 0 || _from >= _origins.Length || double.IsNaN(_origins[_from]))
        {
            return travel;
        }

        var first = _origins.FirstOrDefault(origin => !double.IsNaN(origin), _origins[_from]);
        var last = _from;

        for (var i = _origins.Length - 1; i >= 0; i--)
        {
            if (!double.IsNaN(_origins[i]))
            {
                last = i;
                break;
            }
        }

        var min = Math.Min(first - _origins[_from], 0);
        var max = Math.Max(_origins[last] + _widths[last] - (_origins[_from] + _widths[_from]), 0);
        return Math.Clamp(travel, min, max);
    }

    private int IndexAt(double x)
    {
        for (var i = 0; i < _owner.ItemCount; i++)
        {
            if (_owner.ContainerFromIndex(i) is Visual container &&
                container.TranslatePoint(default, _owner) is { } origin &&
                x >= origin.X && x <= origin.X + container.Bounds.Width)
            {
                return i;
            }
        }

        return -1;
    }

    private static ITransform Translate(double x) =>
        TransformOperations.Parse(string.Create(CultureInfo.InvariantCulture, $"translateX({x}px)"));
}
