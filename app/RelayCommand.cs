using System.Windows.Input;

// Minimal ICommand for wiring tray click commands to methods.
sealed class RelayCommand : ICommand
{
    readonly Action _execute;

    public RelayCommand(Action execute) => _execute = execute;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _execute();
}
