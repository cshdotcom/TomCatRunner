using System;
using System.Collections.Generic;
using UniRx;

namespace TomCatRunner.Common.Reactive
{
    /// <summary>
    /// 消息总线 - 类型安全的发布/订阅中心
    /// 基于 UniRx Subject 实现，用于系统间解耦通信
    /// </summary>
    public class MessageBroker : IDisposable
    {
        private readonly Dictionary<Type, IDisposable> _subjects = new Dictionary<Type, IDisposable>();
        private readonly object _lock = new object();

        /// <summary>
        /// 订阅指定类型的消息
        /// </summary>
        public IDisposable Subscribe<T>(Action<T> handler)
        {
            if (handler == null) return Disposable.Empty;

            Subject<T> subject = GetOrCreateSubject<T>();
            return subject.Subscribe(handler);
        }

        /// <summary>
        /// 发布指定类型的消息
        /// </summary>
        public void Publish<T>(T message)
        {
            Subject<T> subject;
            lock (_lock)
            {
                if (!_subjects.TryGetValue(typeof(T), out var disposable)) return;
                subject = disposable as Subject<T>;
            }

            // UniRx 的 Subject 在 Dispose 后 OnNext 为安全空操作
            subject?.OnNext(message);
        }

        /// <summary>
        /// 获取或创建指定消息类型的 Subject
        /// </summary>
        public Subject<T> GetOrCreateSubject<T>()
        {
            lock (_lock)
            {
                if (_subjects.TryGetValue(typeof(T), out var disposable) &&
                    disposable is Subject<T> subject)
                {
                    return subject;
                }

                var created = new Subject<T>();
                _subjects[typeof(T)] = created;
                return created;
            }
        }

        /// <summary>
        /// 清空所有订阅
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                foreach (var disposable in _subjects.Values)
                {
                    disposable.Dispose();
                }
                _subjects.Clear();
            }
        }

        public void Dispose()
        {
            Clear();
        }
    }

    /// <summary>
    /// 泛型消息包装 - 用于无参数载荷的简单通知
    /// </summary>
    public readonly struct UnitMessage
    {
        public readonly string EventId;
        public UnitMessage(string eventId) => EventId = eventId;
    }
}
