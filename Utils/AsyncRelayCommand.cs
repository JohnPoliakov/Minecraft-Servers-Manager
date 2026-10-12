using System.Windows.Input;

namespace Minecraft_Server_Manager.Utils;

public sealed class AsyncRelayCommand(Func<object?, Task> execute, Action<Exception> onError, Predicate<object?>? canExecute = null) : ICommand
{
    private bool _executing;
    public bool CanExecute(object? parameter) => !_executing && (canExecute?.Invoke(parameter) ?? true);
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        _executing = true; CommandManager.InvalidateRequerySuggested();
        try { await execute(parameter); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { onError(ex); }
        finally { _executing = false; CommandManager.InvalidateRequerySuggested(); }
    }
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
