using System.Windows.Input;
using Breeze.Services;

namespace Breeze.Utilities;

/// <summary>Minimal always-executable command; enablement is expressed through bindings.
/// Failures are logged rather than allowed to escape into the dispatcher loop, which would
/// terminate the browser and every open tab.</summary>
public sealed class RelayCommand(Action execute) : ICommand
{
    event EventHandler? ICommand.CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter)
    {
        try
        {
            execute();
        }
        catch (Exception error)
        {
            ErrorLog.Write("command", error);
        }
    }
}
