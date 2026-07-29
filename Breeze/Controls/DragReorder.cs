using System.Globalization;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;

namespace Breeze.Controls;

/// <summary>What an items control does with a finished drag. Reordering is the whole story for the
/// tab strip; the bookmark surfaces also accept an item dropped onto a folder, or released outside
/// the control altogether, which is how an entry leaves one surface for another.</summary>
internal interface IDragReorderHost
{
    Orientation Orientation { get; }

    void Move(int from, int to);

    /// <summary>True when the item at this index can swallow another, which folders can.</summary>
    bool CanDropInto(int index) => false;

    void DropInto(int from, int index)
    {
    }

    /// <summary>Handles an item released outside the control. True when the drop was taken, so the
    /// control leaves the item alone instead of reordering it.</summary>
    bool DropOutside(int from, PixelPoint screen) => false;
}

/// <summary>Direct manipulation reordering for an items control: the pressed item follows the
/// pointer exactly, its neighbours ease aside as it passes them, and the new order is applied once,
/// on release. Shared by the tab strip, the bookmark bar and the folder menus, so items may run
/// across or down and differ in size; every position is measured at the start of a drag rather than
/// assumed.</summary>
internal sealed class DragReorder
{
    /// <summary>How long a neighbour takes to slide aside, and the drop to settle.</summary>
    public const int SlideMilliseconds = 140;

    /// <summary>Marks the folder an item is about to be dropped into.</summary>
    public const string DropTargetClass = "droptarget";

    /// <summary>Movement below this is a click, not a drag.</summary>
    private const double DragThreshold = 6;

    /// <summary>Share of a folder's length, around its middle, that swallows a drop rather than
    /// making room beside it. The ends stay free so an item can still be placed next to a folder.</summary>
    private const double DropIntoShare = 0.6;

    private readonly ItemsControl _owner;
    private readonly IDragReorderHost _host;

    /// <summary>Leading edge and length of every item along the drag axis, relative to the owner,
    /// captured when the drag starts. An item no panel has realized yet is left as NaN and never
    /// dragged past.</summary>
    private double[] _origins = [];
    private double[] _lengths = [];

    private double _press;
    private double _pitch;
    private int _from = -1;
    private int _to = -1;
    private int _into = -1;

    private DragReorder(ItemsControl owner, IDragReorderHost host)
    {
        _owner = owner;
        _host = host;

        // Items mark the press as handled while selecting or clicking, so the owner listens ahead
        // of them on the tunnel pass and keeps receiving moves even after they are handled.
        owner.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
        owner.AddHandler(InputElement.PointerMovedEvent, OnMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        owner.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        owner.AddHandler(InputElement.PointerCaptureLostEvent, OnCaptureLost);
    }

    /// <summary>Adds dragging to an items control, with the host deciding what a drop means.</summary>
    public static DragReorder Attach(ItemsControl owner, IDragReorderHost host) => new(owner, host);

    /// <summary>Adds plain reordering, for a control where a drop can only ever move an item.</summary>
    public static DragReorder Attach(ItemsControl owner, Action<int, int> move) =>
        new(owner, new ReorderOnly(move));

    public bool IsDragging { get; private set; }

    /// <summary>The identity transform, written along the drag axis so a transition from it
    /// interpolates one operation rather than falling back to whole matrices.</summary>
    private ITransform Rest => Translate(0, _host.Orientation);

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

        _press = Along(e.GetPosition(_owner));
        _from = IndexAt(_press);
        _to = _from;
        _into = -1;
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

        if (!IsDragging && Math.Abs(Along(point.Position) - _press) < DragThreshold)
        {
            return;
        }

        if (!IsDragging && !Begin(e))
        {
            return;
        }

        Track(point.Position);
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
        Drop(e.GetPosition(_owner), e);
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
        _lengths = new double[count];
        var realized = 0;

        for (var i = 0; i < count; i++)
        {
            if (_owner.ContainerFromIndex(i) is Visual container &&
                Extent(container) > 0 &&
                container.TranslatePoint(default, _owner) is { } origin)
            {
                _origins[i] = Along(origin);
                _lengths[i] = Extent(container);
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

        _pitch = _lengths[_from] + Gap();
        return _pitch > 0;
    }

    /// <summary>Spacing between neighbouring items, taken from the first realized pair.</summary>
    private double Gap()
    {
        for (var i = 0; i + 1 < _origins.Length; i++)
        {
            if (!double.IsNaN(_origins[i]) && !double.IsNaN(_origins[i + 1]))
            {
                return Math.Max(_origins[i + 1] - _origins[i] - _lengths[i], 0);
            }
        }

        return 0;
    }

    private void Track(Point position)
    {
        var offset = Offset(Along(position));
        var into = FolderAt(position);

        if (into != _into)
        {
            Mark(_into, false);
            Mark(into, true);
            _into = into;
        }

        // Over a folder the row stays as it is: nothing needs to make room for an item that is
        // about to go inside something.
        _to = _into >= 0 ? _from : Target(offset);

        for (var i = 0; i < _owner.ItemCount; i++)
        {
            if (_owner.ContainerFromIndex(i) is Control container)
            {
                container.RenderTransform = Translate(Displacement(i, offset), _host.Orientation);
            }
        }
    }

    /// <summary>The folder the pointer is over, or -1. Only the middle of a folder counts, so the
    /// ends of it still reorder, and an item is never dropped into itself.</summary>
    private int FolderAt(Point position)
    {
        var along = Along(position);

        // Measured places, not live ones: the items are carrying drag transforms by now, so asking
        // where they are on screen would report where they have slid to rather than where they
        // belong, and the pointer would land on the wrong entry.
        var index = MeasuredIndexAt(along);

        if (index < 0 || index == _from || !_host.CanDropInto(index) || double.IsNaN(_origins[index]))
        {
            return -1;
        }

        var margin = _lengths[index] * ((1 - DropIntoShare) / 2);
        return along >= _origins[index] + margin && along <= _origins[index] + _lengths[index] - margin
            ? index
            : -1;
    }

    private void Mark(int index, bool marked)
    {
        if (index < 0 || _owner.ContainerFromIndex(index) is not Control container)
        {
            return;
        }

        if (marked)
        {
            container.Classes.Add(DropTargetClass);
        }
        else
        {
            container.Classes.Remove(DropTargetClass);
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
            var leading = _origins[_from] + _lengths[_from] + offset;

            while (target + 1 < _origins.Length &&
                   !double.IsNaN(_origins[target + 1]) &&
                   leading > _origins[target + 1] + (_lengths[target + 1] / 2))
            {
                target++;
            }
        }
        else
        {
            var leading = _origins[_from] + offset;

            while (target - 1 >= 0 &&
                   !double.IsNaN(_origins[target - 1]) &&
                   leading < _origins[target - 1] + (_lengths[target - 1] / 2))
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

    /// <summary>Applies the drop: into a folder, onto another surface, or as a reorder here.</summary>
    private void Drop(Point position, PointerEventArgs e)
    {
        var into = _into;
        Mark(into, false);
        _into = -1;

        if (into >= 0)
        {
            ClearOffsets();
            _host.DropInto(_from, into);
            return;
        }

        if (!_owner.Bounds.Contains(position) &&
            e.Source is Visual source &&
            source.PointToScreen(e.GetPosition(source)) is { } screen &&
            _host.DropOutside(_from, screen))
        {
            ClearOffsets();
            return;
        }

        Commit(Offset(Along(position)));
    }

    private void Commit(double offset)
    {
        var to = _to;

        // Where the dragged item's leading edge sits right now, in the owner's coordinates.
        var released = _origins[_from] + offset;

        if (to != _from)
        {
            _host.Move(_from, to);

            // A virtualizing panel does not update its index to element mapping until it measures,
            // so force the pass here. Without it, every lookup below addresses the wrong element:
            // the dragged one would keep its drag transform and never be reconciled.
            _owner.UpdateLayout();
        }

        ClearOffsets();

        // Layout has the item in its new place now, with no transform left on it, so its actual
        // position is known. Start from where the pointer left it and ease the difference, which
        // makes the drop settle instead of snapping.
        if (_owner.ContainerFromIndex(to) is not Control settled ||
            settled.TranslatePoint(default, _owner) is not { } placed)
        {
            return;
        }

        var residual = released - Along(placed);

        if (Math.Abs(residual) < 1)
        {
            return;
        }

        settled.RenderTransform = Translate(residual, _host.Orientation);

        Dispatcher.UIThread.Post(() =>
        {
            settled.Transitions = Slide();
            settled.RenderTransform = Rest;
        }, DispatcherPriority.Render);
    }

    private void Cancel()
    {
        if (IsDragging)
        {
            Mark(_into, false);
            _into = -1;
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
                container.Classes.Remove(DropTargetClass);
            }
        }
    }

    private void Reset()
    {
        _from = -1;
        _to = -1;
        _into = -1;
        IsDragging = false;
    }

    /// <summary>Pointer travel, clamped so the item cannot be dragged beyond either end.</summary>
    private double Offset(double along)
    {
        var travel = along - _press;

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
        var max = Math.Max(_origins[last] + _lengths[last] - (_origins[_from] + _lengths[_from]), 0);
        return Math.Clamp(travel, min, max);
    }

    /// <summary>The item whose measured place holds this coordinate, or -1.</summary>
    private int MeasuredIndexAt(double along)
    {
        for (var i = 0; i < _origins.Length; i++)
        {
            if (!double.IsNaN(_origins[i]) && along >= _origins[i] && along <= _origins[i] + _lengths[i])
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The item under a coordinate before a drag begins, when nothing has moved yet.</summary>
    private int IndexAt(double along)
    {
        for (var i = 0; i < _owner.ItemCount; i++)
        {
            if (_owner.ContainerFromIndex(i) is Visual container &&
                container.TranslatePoint(default, _owner) is { } origin &&
                along >= Along(origin) && along <= Along(origin) + Extent(container))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The coordinate that matters for this control: across for a row, down for a menu.</summary>
    private double Along(Point point) => _host.Orientation == Orientation.Horizontal ? point.X : point.Y;

    private double Extent(Visual visual) =>
        _host.Orientation == Orientation.Horizontal ? visual.Bounds.Width : visual.Bounds.Height;

    private static ITransform Translate(double offset, Orientation orientation) =>
        TransformOperations.Parse(orientation == Orientation.Horizontal
            ? string.Create(CultureInfo.InvariantCulture, $"translateX({offset}px)")
            : string.Create(CultureInfo.InvariantCulture, $"translateY({offset}px)"));

    /// <summary>Host for a control whose items can only be reordered, such as the tab strip.</summary>
    private sealed class ReorderOnly(Action<int, int> move) : IDragReorderHost
    {
        public Orientation Orientation => Orientation.Horizontal;

        public void Move(int from, int to) => move(from, to);
    }
}

