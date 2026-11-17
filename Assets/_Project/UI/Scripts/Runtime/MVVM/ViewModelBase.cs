using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using UniRx;
using Cysharp.Threading.Tasks;

namespace TomCatRunner.UI
{
    /// <summary>
    /// ViewModel 基类 - 支持 INotifyPropertyChanged、命令、生命周期
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged, IDisposable
    {
        private readonly CompositeDisposable _disposables = new CompositeDisposable();
        private bool _isDisposed = false;
        private bool _isInitialized = false;

        public event PropertyChangedEventHandler PropertyChanged;

        public bool IsInitialized => _isInitialized;
        public bool IsDisposed => _isDisposed;

        /// <summary>
        /// 初始化 ViewModel
        /// </summary>
        public virtual async UniTask InitializeAsync()
        {
            if (_isInitialized) return;
            await OnInitializeAsync();
            _isInitialized = true;
        }

        protected virtual UniTask OnInitializeAsync() => UniTask.CompletedTask;

        /// <summary>
        /// 绑定属性变更通知
        /// </summary>
        protected void SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            OnPropertyChanged(propertyName);
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 添加可释放资源
        /// </summary>
        protected void AddDisposable(IDisposable disposable)
        {
            _disposables.Add(disposable);
        }

        /// <summary>
        /// 创建响应式命令
        /// </summary>
        protected ReactiveCommand<T> CreateCommand<T>(Func<T, UniTask> execute, Func<T, bool> canExecute = null)
        {
            var cmd = new ReactiveCommand<T>(execute, canExecute);
            AddDisposable(cmd);
            return cmd;
        }

        protected ReactiveCommand CreateCommand(Func<UniTask> execute, Func<bool> canExecute = null)
        {
            var cmd = new ReactiveCommand(execute, canExecute);
            AddDisposable(cmd);
            return cmd;
        }

        /// <summary>
        /// 创建响应式属性
        /// </summary>
        protected ReactiveProperty<T> CreateProperty<T>(T initialValue = default)
        {
            var prop = new ReactiveProperty<T>(initialValue);
            AddDisposable(prop);
            return prop;
        }

        protected ReadOnlyReactiveProperty<T> CreateReadOnlyProperty<T>(IObservable<T> source, T initialValue = default)
        {
            var prop = source.ToReadOnlyReactiveProperty(initialValue);
            AddDisposable(prop);
            return prop;
        }

        public virtual void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _disposables.Dispose();
            OnDispose();
        }

        protected virtual void OnDispose() { }
    }

    /// <summary>
    /// 响应式命令
    /// </summary>
    public class ReactiveCommand : IDisposable
    {
        private readonly Func<UniTask> _execute;
        private readonly Func<bool> _canExecute;
        private readonly ReactiveProperty<bool> _isExecuting = new ReactiveProperty<bool>(false);
        private readonly Subject<Unit> _executed = new Subject<Unit>();
        private bool _disposed = false;

        public IReadOnlyReactiveProperty<bool> IsExecuting => _isExecuting;
        public IObservable<Unit> Executed => _executed;

        public ReactiveCommand(Func<UniTask> execute, Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute ?? (() => true);
        }

        public bool CanExecute() => !_isExecuting.Value && _canExecute();

        public async UniTask ExecuteAsync()
        {
            if (!CanExecute()) return;

            _isExecuting.Value = true;
            try
            {
                await _execute();
                _executed.OnNext(Unit.Default);
            }
            finally
            {
                _isExecuting.Value = false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _isExecuting.Dispose();
            _executed.Dispose();
        }
    }

    public class ReactiveCommand<T> : IDisposable
    {
        private readonly Func<T, UniTask> _execute;
        private readonly Func<T, bool> _canExecute;
        private readonly ReactiveProperty<bool> _isExecuting = new ReactiveProperty<bool>(false);
        private readonly Subject<T> _executed = new Subject<T>();
        private bool _disposed = false;

        public IReadOnlyReactiveProperty<bool> IsExecuting => _isExecuting;
        public IObservable<T> Executed => _executed;

        public ReactiveCommand(Func<T, UniTask> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute ?? (_ => true);
        }

        public bool CanExecute(T parameter) => !_isExecuting.Value && _canExecute(parameter);

        public async UniTask ExecuteAsync(T parameter)
        {
            if (!CanExecute(parameter)) return;

            _isExecuting.Value = true;
            try
            {
                await _execute(parameter);
                _executed.OnNext(parameter);
            }
            finally
            {
                _isExecuting.Value = false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _isExecuting.Dispose();
            _executed.Dispose();
        }
    }
}