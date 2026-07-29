namespace Breeze.Models;

/// <summary>One list of bookmark entries a drag can rearrange: the bar itself, or the inside of one
/// folder. Positions are what the surface shows; ids are how an entry is named to the store, so a
/// drop that crosses from one surface to another still moves the entry the user picked up.
///
/// The controls talk to this rather than to a concrete view model, as the tab strip does with
/// <see cref="ITabReorder" />.</summary>
public interface IBookmarkSurface
{
    /// <summary>Id of the entry shown at this position, or null when there is none.</summary>
    string? IdAt(int index);

    /// <summary>True when the entry at this position is a folder, so a drop can go inside it.</summary>
    bool IsFolder(int index);

    /// <summary>Moves an entry to another position on this same surface. The index counts places
    /// once the entry has been taken out, which is where the drop left it.</summary>
    void Reorder(int from, int to);

    /// <summary>Puts the entry at <paramref name="from" /> inside the folder at
    /// <paramref name="index" />, at the end of it.</summary>
    void DropInto(int from, int index);

    /// <summary>Takes an entry from anywhere else and puts it at this position.</summary>
    void Adopt(string id, int index);
}
