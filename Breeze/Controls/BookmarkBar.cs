using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Breeze.Models;

namespace Breeze.Controls;

/// <summary>Bookmark bar. Reordering by dragging works as it does in the tab strip, both going
/// through <see cref="DragReorder" />; entries differ in width, which that handles. A folder on the
/// bar also takes a drop, which puts the dragged entry inside it, and the bar itself takes entries
/// dragged out of an open folder menu.</summary>
public sealed class BookmarkBar : ItemsControl, IDragReorderHost
{
    /// <summary>What a drag on this bar means. Set from the view, so the control needs no knowledge
    /// of any view model type.</summary>
    public static readonly StyledProperty<IBookmarkSurface?> SurfaceProperty =
        AvaloniaProperty.Register<BookmarkBar, IBookmarkSurface?>(nameof(Surface));

    /// <summary>Bars currently on screen, so a folder menu can find the one under the pointer. One
    /// window means one bar today; a list keeps that from being an assumption.</summary>
    private static readonly List<BookmarkBar> Live = [];

    public BookmarkBar() => DragReorder.Attach(this, this);

    public IBookmarkSurface? Surface
    {
        get => GetValue(SurfaceProperty);
        set => SetValue(SurfaceProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(ItemsControl);

    Orientation IDragReorderHost.Orientation => Orientation.Horizontal;

    void IDragReorderHost.Move(int from, int to) => Surface?.Reorder(from, to);

    bool IDragReorderHost.CanDropInto(int index) => Surface?.IsFolder(index) == true;

    void IDragReorderHost.DropInto(int from, int index) => Surface?.DropInto(from, index);

    /// <summary>Puts an entry dragged from a folder menu onto the bar, at the place it was released.
    /// False when the pointer was not over a bar, which abandons the drag.</summary>
    internal static bool TryDrop(string id, PixelPoint screen) => Live.Any(bar => bar.Adopt(id, screen));

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Live.Add(this);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Live.Remove(this);
    }

    private bool Adopt(string id, PixelPoint screen)
    {
        if (Surface is not { } surface || !this.IsAttachedToVisualTree())
        {
            return false;
        }

        var local = this.PointToClient(screen);

        if (!new Rect(Bounds.Size).Contains(local))
        {
            return false;
        }

        surface.Adopt(id, InsertAt(local.X));
        return true;
    }

    /// <summary>Where an entry released at this position belongs: before the first entry whose
    /// middle the pointer has not reached, or at the end of the bar.</summary>
    private int InsertAt(double x)
    {
        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is Visual container &&
                container.TranslatePoint(default, this) is { } origin &&
                x < origin.X + (container.Bounds.Width / 2))
            {
                return i;
            }
        }

        return ItemCount;
    }
}
