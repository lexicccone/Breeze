namespace Breeze.Models;

/// <summary>A settings row that can be put into recording mode and handed the combination the user
/// pressed. The control does the listening and the formatting; the row decides whether the
/// combination can be used and what to say when it cannot.</summary>
public interface IShortcutRecorder
{
    bool IsRecording { get; }

    void Begin();

    void Cancel();

    /// <summary>Takes a recorded combination in Avalonia gesture notation, such as
    /// <c>Ctrl+Shift+B</c>, or clears the shortcut when it is null.</summary>
    void Record(string? gesture);
}
