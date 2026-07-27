namespace Breeze.Models;

/// <summary>Implemented by view models whose tab list can be reordered by dragging.</summary>
public interface ITabReorder
{
    void MoveTab(int oldIndex, int newIndex);
}
