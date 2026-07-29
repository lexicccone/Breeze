using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Breeze.Models;

namespace Breeze.Controls;

/// <summary>The menu a bookmark folder shows: its entries beneath the folder it belongs to, with
/// folders inside it opening as submenus to the side. Built here rather than from a template because
/// it is recursive, and because the entries inside it can be dragged, which a menu control does not
/// allow: dropping one on a folder puts it inside, and dragging one out onto the bar takes it out.
///
/// A popup is a window of its own, so it draws above the engine's native child window, which an
/// overlay in the page area would not. Only one chain is open at a time, and it belongs to the
/// folder that opened it, so moving along the bar hands the menu from one folder to the next.</summary>
internal sealed class BookmarkMenu : IDragReorderHost
{
    /// <summary>Past this the menu scrolls rather than growing off the screen.</summary>
    private const double MaxMenuHeight = 420;

    private const double MinMenuWidth = 190;

    /// <summary>How long the pointer rests on a folder before its submenu opens. Short enough to
    /// feel immediate, long enough that passing over a folder on the way down does not open it.</summary>
    private const int HoverDelayMilliseconds = 200;

    private static BookmarkMenu? _chain;

    private readonly IBookmarkFolder _folder;
    private readonly Control _anchor;
    private readonly Popup _popup;
    private readonly ItemsControl _rows;
    private readonly DragReorder _drag;
    private readonly DispatcherTimer _hover;

    private BookmarkMenu? _child;
    private Control? _pending;

    private BookmarkMenu(Control anchor, IBookmarkFolder folder, PlacementMode placement, bool lightDismiss)
    {
        _anchor = anchor;
        _folder = folder;

        _rows = new ItemsControl { ItemsSource = folder.Entries };

        foreach (var key in new[] { "BookmarkMenuBookmark", "BookmarkMenuFolder" })
        {
            if (anchor.TryFindResource(key, out var found) && found is IDataTemplate template)
            {
                _rows.DataTemplates.Add(template);
            }
        }

        _rows.AddHandler(Button.ClickEvent, OnRowClick);
        _rows.PointerMoved += OnPointerMoved;
        _drag = DragReorder.Attach(_rows, this);

        _hover = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(HoverDelayMilliseconds) };
        _hover.Tick += OnHoverElapsed;

        var surface = new Border
        {
            Classes = { "bookmarkmenu" },
            MinWidth = MinMenuWidth,
            Child = folder.IsEmpty
                ? new TextBlock { Classes = { "bookmarkempty" }, Text = "Empty" }
                : new ScrollViewer
                {
                    Content = _rows,
                    MaxHeight = MaxMenuHeight,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                }
        };

        _popup = new Popup
        {
            Child = surface,
            PlacementTarget = anchor,
            Placement = placement,
            VerticalOffset = placement == PlacementMode.BottomEdgeAlignedLeft ? 5 : -5,
            HorizontalOffset = placement == PlacementMode.BottomEdgeAlignedLeft ? 0 : 3,
            IsLightDismissEnabled = lightDismiss
        };

        ((ISetLogicalParent)_popup).SetParent(anchor);
        _popup.Closed += OnPopupClosed;
        _popup.IsOpen = true;
    }

    /// <summary>True while any folder menu is showing.</summary>
    public static bool IsOpen => _chain is not null;

    /// <summary>True when the open menu is the one this control opened.</summary>
    public static bool IsOpenFor(Control anchor) => _chain?._anchor == anchor;

    /// <summary>Shows a folder's menu, or closes it when it is already showing.</summary>
    public static void Toggle(Control anchor, IBookmarkFolder? folder)
    {
        if (IsOpenFor(anchor))
        {
            CloseAll();
            return;
        }

        Show(anchor, folder);
    }

    /// <summary>Shows a folder's menu, replacing whichever one was open.</summary>
    public static void Show(Control anchor, IBookmarkFolder? folder)
    {
        CloseAll();

        if (folder is not null)
        {
            _chain = new BookmarkMenu(anchor, folder, PlacementMode.BottomEdgeAlignedLeft, lightDismiss: true);
        }
    }

    public static void CloseAll()
    {
        var open = _chain;
        _chain = null;
        open?.Close();
    }

    Orientation IDragReorderHost.Orientation => Orientation.Vertical;

    void IDragReorderHost.Move(int from, int to) => _folder.Reorder(from, to);

    bool IDragReorderHost.CanDropInto(int index) => _folder.IsFolder(index);

    void IDragReorderHost.DropInto(int from, int index)
    {
        _folder.DropInto(from, index);

        // The entry has left this list for one inside it, so what the menu shows is no longer what
        // the user was looking at. Closing is what a native menu does after a drop of its own.
        CloseAll();
    }

    /// <summary>Takes an entry out of the folder: released over the bar, it moves there, at the
    /// place it was let go. Released anywhere else, the drag is simply abandoned.</summary>
    bool IDragReorderHost.DropOutside(int from, PixelPoint screen)
    {
        if (_folder.IdAt(from) is not { } id || !BookmarkBar.TryDrop(id, screen))
        {
            return false;
        }

        CloseAll();
        return true;
    }

    private void Close()
    {
        _hover.Stop();
        CloseChild();

        _popup.Closed -= OnPopupClosed;
        _popup.IsOpen = false;
        ((ISetLogicalParent)_popup).SetParent(null);
    }

    private void CloseChild()
    {
        var child = _child;
        _child = null;
        child?.Close();
    }

    /// <summary>The head of the chain lost its popup, to a click elsewhere for instance, so the
    /// whole chain goes with it.</summary>
    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (_chain == this)
        {
            CloseAll();
            return;
        }

        CloseChild();
    }

    private void OnRowClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Control clicked)
        {
            return;
        }

        if (clicked.DataContext is IBookmarkFolder folder)
        {
            _hover.Stop();
            Open(Container(clicked), folder);
            return;
        }

        // A bookmark row opened its page through its own command; the menu has done its job.
        CloseAll();
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_drag.IsDragging)
        {
            _hover.Stop();
            return;
        }

        if (ContainerAt(e.GetPosition(_rows)) is not { } container || container == _child?._anchor)
        {
            return;
        }

        if (_pending == container)
        {
            return;
        }

        // Moving onto another row drops the submenu at once, so the pointer is never chased by a
        // menu belonging to a folder it has already left.
        _hover.Stop();
        CloseChild();

        _pending = container.DataContext is IBookmarkFolder ? container : null;

        if (_pending is not null)
        {
            _hover.Start();
        }
    }

    private void OnHoverElapsed(object? sender, EventArgs e)
    {
        _hover.Stop();

        if (_pending is { DataContext: IBookmarkFolder folder } row)
        {
            Open(row, folder);
        }
    }

    private void Open(Control? row, IBookmarkFolder folder)
    {
        if (row is null || _child?._anchor == row)
        {
            return;
        }

        CloseChild();
        _child = new BookmarkMenu(row, folder, PlacementMode.RightEdgeAlignedTop, lightDismiss: false);
    }

    /// <summary>The row container the pointer is over, or null between rows.</summary>
    private Control? ContainerAt(Point position)
    {
        for (var i = 0; i < _rows.ItemCount; i++)
        {
            if (_rows.ContainerFromIndex(i) is Control container &&
                container.TranslatePoint(default, _rows) is { } origin &&
                position.Y >= origin.Y &&
                position.Y <= origin.Y + container.Bounds.Height)
            {
                return container;
            }
        }

        return null;
    }

    /// <summary>The row container holding a clicked element, which is what a submenu is placed
    /// against: the row, not the button inside it.</summary>
    private Control? Container(Control clicked)
    {
        for (var i = 0; i < _rows.ItemCount; i++)
        {
            if (_rows.ContainerFromIndex(i) is Control container &&
                (container == clicked || container.IsVisualAncestorOf(clicked)))
            {
                return container;
            }
        }

        return null;
    }
}

