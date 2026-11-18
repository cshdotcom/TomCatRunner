using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TomCatRunner.Core.Networking
{
    /// <summary>
    /// Core 层模拟服务器 - 为 SeasonManager 提供离线时间同步与赛季配置来源（Networking 命名空间，避免与 UnityEngine.AddressableAssets 的 Addressables 类冲突）
    /// （Seasons 模块拥有自己的完整 MockServer，本类仅服务 Core 流式加载管线）
    /// </summary>
    public class MockServer : IDisposable
    {
        private readonly TimeSpan _simulatedClockOffset;
        private readonly SynchronizationContext _syncContext;
        private bool _disposed;

        /// <summary>模拟服务器时间同步回调（在主线程触发）</summary>
        public event Action<DateTime> OnTimeSynced;

        /// <summary>模拟网络延迟（毫秒）</summary>
        public float SimulatedLatencyMs { get; set; } = 80f;

        public MockServer()
        {
            _simulatedClockOffset = TimeSpan.FromSeconds(UnityEngine.Random.Range(-30f, 30f));
            _syncContext = SynchronizationContext.Current;
        }

        /// <summary>当前模拟服务器时间（含偏移）</summary>
        public DateTime CurrentServerTime => DateTime.UtcNow + _simulatedClockOffset;

        /// <summary>
        /// 立即触发一次时间同步回调
        /// </summary>
        public async UniTask SyncTimeAsync()
        {
            await SimulateNetworkDelay();

            if (_disposed) return;

            var serverTime = CurrentServerTime;
            if (_syncContext != null && _syncContext != SynchronizationContext.Current)
            {
                _syncContext.Post(_ => OnTimeSynced?.Invoke(serverTime), null);
            }
            else
            {
                OnTimeSynced?.Invoke(serverTime);
            }
        }

        /// <summary>
        /// 模拟拉取赛季配置列表（离线：返回空列表，由 Resources 兜底）
        /// </summary>
        public async UniTask<IList<Data.SeasonConfig>> FetchSeasonConfigsAsync()
        {
            await SimulateNetworkDelay();
            return Array.Empty<Data.SeasonConfig>();
        }

        private async UniTask SimulateNetworkDelay()
        {
            int ms = Mathf.Max(1, (int)SimulatedLatencyMs);
            await UniTask.Delay(ms);
        }

        public void Dispose()
        {
            _disposed = true;
            OnTimeSynced = null;
        }
    }
}
