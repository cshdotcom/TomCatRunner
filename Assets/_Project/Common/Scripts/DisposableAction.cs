using System;

namespace TomCatRunner.Common
{
    /// <summary>
    /// 简单 IDisposable 包装 - 构造时传入释放回调
    /// </summary>
    public sealed class DisposableAction : IDisposable
    {
        private Action _dispose;
        private bool _disposed;

        public DisposableAction(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _dispose?.Invoke();
            _dispose = null;
        }
    }
}
