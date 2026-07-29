using System.Windows.Input;
using Breeze.Utilities;

namespace Breeze.ViewModels;

/// <summary>A folder the user can pick as a destination. A null id means the bookmark bar itself.</summary>
public sealed record BookmarkFolderChoice(string Label, string? Id);

/// <summary>The one small dialog the bookmark actions need: a name, a name and an address, a folder
/// to move into, or a plain question. Which fields appear is decided by what the caller fills in,
/// so renaming, adding and confirming share a single window rather than three near copies.</summary>
public sealed class BookmarkPromptViewModel : ViewModelBase
{
    private readonly Func<BookmarkPromptViewModel, string?> _commit;

    private string _name = string.Empty;
    private string _url = string.Empty;
    private BookmarkFolderChoice? _selectedFolder;
    private string? _error;

    /// <summary><paramref name="commit" /> returns null when it accepted the values, or the reason
    /// it did not, which the dialog shows while staying open.</summary>
    internal BookmarkPromptViewModel(string header, string confirmLabel, Func<BookmarkPromptViewModel, string?> commit)
    {
        Header = header;
        ConfirmLabel = confirmLabel;
        _commit = commit;

        ConfirmCommand = new RelayCommand(Confirm);
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>Raised when the dialog is finished with, whether it was accepted or cancelled.</summary>
    public event EventHandler? CloseRequested;

    public string Header { get; }

    public string ConfirmLabel { get; }

    /// <summary>Explanatory text, used by the delete question.</summary>
    public string? Message { get; init; }

    public bool HasMessage => Message is not null;

    public bool HasName { get; init; }

    public bool HasUrl { get; init; }

    public IReadOnlyList<BookmarkFolderChoice> Folders { get; init; } = [];

    public bool HasFolders => Folders.Count > 0;

    public ICommand ConfirmCommand { get; }

    public ICommand CancelCommand { get; }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Url
    {
        get => _url;
        set => SetProperty(ref _url, value);
    }

    public BookmarkFolderChoice? SelectedFolder
    {
        get => _selectedFolder;
        set => SetProperty(ref _selectedFolder, value);
    }

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

    private void Confirm()
    {
        Error = _commit(this);

        if (Error is null)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
