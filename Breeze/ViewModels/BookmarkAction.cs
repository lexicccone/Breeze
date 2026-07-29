using System.Windows.Input;

namespace Breeze.ViewModels;

/// <summary>One entry of a bookmark context menu. The bar and the folder menus show the same list
/// for the same kind of entry, so the actions are described once, here, rather than in each view.</summary>
public sealed record BookmarkAction(string Label, ICommand Command);
