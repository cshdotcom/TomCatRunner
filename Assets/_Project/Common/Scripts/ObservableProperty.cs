using System;

namespace TomCatRunner.Common
{
    /// <summary>
    /// 可观察属性 - 支持绑定和变更通知
    /// </summary>
    [Serializable]
    public class ObservableProperty<T>
    {
        [UnityEngine.SerializeField] private T _value;
        
        public event Action<T, T> OnValueChanged;

        public T Value
        {
            get => _value;
            set
            {
                if (Equals(_value, value)) return;
                var oldValue = _value;
                _value = value;
                OnValueChanged?.Invoke(oldValue, value);
            }
        }

        public ObservableProperty(T initialValue = default)
        {
            _value = initialValue;
        }

        public static implicit operator T(ObservableProperty<T> prop) => prop.Value;
        public static implicit operator ObservableProperty<T>(T value) => new ObservableProperty<T>(value);

        public override string ToString() => _value?.ToString() ?? "null";
    }

    /// <summary>
    /// 只读可观察属性
    /// </summary>
    public class ReadOnlyObservableProperty<T>
    {
        private readonly ObservableProperty<T> _source;

        public T Value => _source.Value;
        public event Action<T, T> OnValueChanged
        {
            add => _source.OnValueChanged += value;
            remove => _source.OnValueChanged -= value;
        }

        public ReadOnlyObservableProperty(ObservableProperty<T> source)
        {
            _source = source;
        }

        public static implicit operator T(ReadOnlyObservableProperty<T> prop) => prop.Value;
    }

    /// <summary>
    /// 响应式命令
    /// </summary>
    public class ReactiveCommand
    {
        public event Action OnExecuted;
        private bool _canExecute = true;

        public bool CanExecute
        {
            get => _canExecute;
            set
            {
                _canExecute = value;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler CanExecuteChanged;

        public void Execute()
        {
            if (!_canExecute) return;
            OnExecuted?.Invoke();
        }

        public void Execute(object parameter)
        {
            if (!_canExecute) return;
            OnExecuted?.Invoke();
        }
    }

    /// <summary>
    /// 带参数的响应式命令
    /// </summary>
    public class ReactiveCommand<T>
    {
        public event Action<T> OnExecuted;
        private bool _canExecute = true;

        public bool CanExecute
        {
            get => _canExecute;
            set
            {
                _canExecute = value;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler CanExecuteChanged;

        public void Execute(T parameter)
        {
            if (!_canExecute) return;
            OnExecuted?.Invoke(parameter);
        }
    }
}