using System.Windows.Input;
using Breeze.Models;
using Breeze.Services;
using Breeze.Utilities;

namespace Breeze.ViewModels;

/// <summary>One row on the keyboard shortcuts page: what the shortcut does, the gesture in force,
/// and the recording state while the user is choosing a new one. Built from a catalog definition, so
/// a shortcut added to the catalog becomes editable here without any change to the page.</summary>
public sealed class ShortcutRowViewModel : ViewModelBase, IShortcutRecorder
{
    private readonly ShortcutDefinition _definition;
    private readonly Action _changed;

    private bool _isRecording;
    private string? _error;
    private bool _isVisible = true;

    internal ShortcutRowViewModel(ShortcutDefinition definition, Action changed)
    {
        _definition = definition;
        _changed = changed;

        EditCommand = new RelayCommand(Begin);
        ResetCommand = new RelayCommand(Reset);
    }

    public string Label => _definition.Label;

    /// <summary>The gesture in force, or that there is none.</summary>
    public string Gesture => KeyboardShortcuts.Text(_definition.Id);

    /// <summary>What the recording button shows: the gesture, or the invitation to press one.</summary>
    public string Display => _isRecording ? "Press a combination..." : Gesture;

    public bool IsDefault => KeyboardShortcuts.IsDefault(_definition.Id);

    public ICommand EditCommand { get; }

    public ICommand ResetCommand { get; }

    public bool IsRecording
    {
        get => _isRecording;
        private set
        {
            if (SetProperty(ref _isRecording, value))
            {
                OnPropertyChanged(nameof(Display));
            }
        }
    }

    /// <summary>Why the last combination was refused, or null. Shown under the row while recording
    /// continues, so the user can simply press something else.</summary>
    public string? Error
    {
        get => _error;
        private set
        {
            if (SetProperty(ref _error, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => _error is not null;

    /// <summary>False while the search box filters this row out.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    /// <summary>True when the row's label contains the text the user is searching for.</summary>
    public bool Matches(string search) =>
        search.Length == 0 || Label.Contains(search, StringComparison.OrdinalIgnoreCase);

    public void Begin()
    {
        Error = null;
        IsRecording = true;
    }

    public void Cancel()
    {
        Error = null;
        IsRecording = false;
    }

    public void Record(string? gesture)
    {
        if (gesture is null)
        {
            KeyboardShortcuts.Clear(_definition.Id);
            Finish();
            return;
        }

        var problem = KeyboardShortcuts.Assign(_definition.Id, gesture, out var usedBy);

        if (problem == ShortcutProblem.None)
        {
            Finish();
            return;
        }

        // Recording continues, so a refused combination costs one more key press rather than
        // another trip through the Edit button.
        Error = problem switch
        {
            ShortcutProblem.NeedsModifier => $"{gesture} needs Ctrl, Alt or Shift as well.",
            ShortcutProblem.AlreadyUsed => $"{gesture} is already used by \"{usedBy}\".",
            _ => $"{gesture} cannot be used as a shortcut."
        };
    }

    private void Reset()
    {
        KeyboardShortcuts.Reset(_definition.Id);
        Finish();
    }

    private void Finish()
    {
        IsRecording = false;
        Error = null;
        Refresh();
        _changed();
    }

    /// <summary>Re-reads what the catalog says, after this row or another one changed.</summary>
    internal void Refresh()
    {
        OnPropertyChanged(nameof(Gesture));
        OnPropertyChanged(nameof(Display));
        OnPropertyChanged(nameof(IsDefault));
    }
}
