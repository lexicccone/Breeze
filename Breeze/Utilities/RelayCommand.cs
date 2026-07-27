using System.Windows.Input;

namespace Breeze.Utilities;

/// <summary>Minimal always-executable command; enablement is expressed through bindings.</summary>
public sealed class RelayCommand(Action execute) : ICommand
{
    event EventHandler? ICommand.CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute();
}
