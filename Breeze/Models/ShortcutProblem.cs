namespace Breeze.Models;

/// <summary>Why a recorded key combination was refused, or that it was taken.</summary>
public enum ShortcutProblem
{
    None,

    /// <summary>Not a combination Breeze can act on, such as a key it cannot name.</summary>
    Unusable,

    /// <summary>A bare key. Shortcuts reach Breeze while a page has focus, so one without a
    /// modifier would fire whenever the user typed that letter.</summary>
    NeedsModifier,

    /// <summary>Another action already answers to it.</summary>
    AlreadyUsed
}
