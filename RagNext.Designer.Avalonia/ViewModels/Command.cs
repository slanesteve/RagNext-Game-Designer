using System;
using System.Windows.Input;

namespace RagNext.Designer.Avalonia.ViewModels
{
    public class Command : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public Command(Action execute) : this(o => execute()) { }
        
        public Command(Action<object?> execute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }
        
        public Command(Func<System.Threading.Tasks.Task> execute) : this(o => { _ = execute(); }) { }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);

        public void Execute(object? parameter) => _execute(parameter);

        public void ChangeCanExecute() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public class Command<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public Command(Action<T?> execute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            if (_canExecute == null) return true;
            if (parameter is T val) return _canExecute(val);
            if (parameter == null && !typeof(T).IsValueType) return _canExecute(default);
            return false;
        }

        public void Execute(object? parameter)
        {
            if (parameter is T val) _execute(val);
            else if (parameter == null && !typeof(T).IsValueType) _execute(default);
        }

        public void ChangeCanExecute() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
