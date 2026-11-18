using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace TomCatRunner.Common.Reactive
{
    /// <summary>
    /// 游戏调度器 - 管理延迟调用、定时任务与帧驱动更新
    /// 由宿主（GameLifetimeScope / UIManager）每帧驱动 Tick
    /// </summary>
    public class Scheduler : IDisposable
    {
        private readonly List<ScheduledAction> _actions = new List<ScheduledAction>();
        private readonly object _lock = new object();
        private bool _disposed;

        private sealed class ScheduledAction
        {
            public Action Callback;
            public float DelaySeconds;
            public float Interval;       // <=0 表示一次性
            public float NextTriggerTime;
            public bool Removed;
        }

        private float _time;

        /// <summary>
        /// 当前调度器时间（秒）
        /// </summary>
        public float Now => _time;

        /// <summary>
        /// 每帧驱动（由宿主调用）
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_disposed) return;
            _time += deltaTime;

            List<ScheduledAction> pending = null;

            lock (_lock)
            {
                for (int i = 0; i < _actions.Count; i++)
                {
                    var action = _actions[i];
                    if (action.Removed || _time < action.NextTriggerTime) continue;

                    if (action.Interval > 0f)
                    {
                        action.NextTriggerTime = _time + action.Interval;
                    }
                    else
                    {
                        action.Removed = true;
                    }

                    pending = pending ?? new List<ScheduledAction>();
                    pending.Add(action);
                }
            }

            if (pending == null) return;

            foreach (var action in pending)
            {
                try
                {
                    action.Callback?.Invoke();
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"[Scheduler] Callback error: {e}");
                }
            }

            lock (_lock)
            {
                _actions.RemoveAll(a => a.Removed);
            }
        }

        /// <summary>
        /// 延迟调用（游戏时间缩放）
        /// </summary>
        public IDisposable Schedule(float delaySeconds, Action callback)
        {
            var action = new ScheduledAction
            {
                Callback = callback,
                DelaySeconds = delaySeconds,
                NextTriggerTime = _time + Mathf.Max(0f, delaySeconds)
            };
            lock (_lock)
            {
                _actions.Add(action);
            }
            return Disposable.Create(() => action.Removed = true);
        }

        /// <summary>
        /// 重复调用（interval 秒一次，返回 IDisposable 停止）
        /// </summary>
        public IDisposable ScheduleRepeating(float intervalSeconds, Action callback)
        {
            var action = new ScheduledAction
            {
                Callback = callback,
                Interval = Mathf.Max(0.01f, intervalSeconds),
                NextTriggerTime = _time + Mathf.Max(0.01f, intervalSeconds)
            };
            lock (_lock)
            {
                _actions.Add(action);
            }
            return Disposable.Create(() => action.Removed = true);
        }

        /// <summary>
        /// 清除所有调度
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _actions.Clear();
            }
        }

        public void Dispose()
        {
            _disposed = true;
            Clear();
        }
    }
}
