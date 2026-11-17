using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer;
using TomCatRunner.Common.Reactive;

namespace TomCatRunner.Core.Season
{
    /// <summary>
    /// 赛季管理器 - 核心系统入口
    /// 负责：赛季状态机、时间同步、配置热更、主题切换事件广播
    /// </summary>
    public class SeasonManager : IInitializable, ITickable, IDisposable, IPausable
    {
        public event Action<SeasonConfig> OnSeasonChanged;
        public event Action<ThemeProfile> OnThemeChanged;
        public event Action<float> OnSeasonProgressChanged; // 0-1
        public event Action OnSeasonExpired;

        private readonly MockServer _mockServer;
        private readonly MessageBroker _messageBroker;
        private readonly GameLifetimeScope.GameConfig _config;

        private SeasonConfig _currentSeason;
        private ThemeProfile _currentTheme;
        private CancellationTokenSource _cts;
        private bool _isPaused;
        private DateTime _serverTimeOffset;
        private bool _initialized;

        // 缓存的所有赛季配置
        private readonly Dictionary<string, SeasonConfig> _seasonCache = new();
        private AsyncOperationHandle<IList<SeasonConfig>> _loadHandle;

        [Inject]
        public SeasonManager(MockServer mockServer, MessageBroker messageBroker, GameLifetimeScope.GameConfig config)
        {
            _mockServer = mockServer;
            _messageBroker = messageBroker;
            _config = config;
        }

        public void Initialize()
        {
            if (_initialized) return;
            _cts = new CancellationTokenSource();

            // 订阅模拟服务器时间同步
            _mockServer.OnTimeSynced += HandleTimeSynced;

            // 异步加载所有赛季配置
            LoadAllSeasonsAsync().Forget();

            _initialized = true;
            Debug.Log($"[SeasonManager] Initialized. OfflineMode: {_config.OfflineMode}");
        }

        public void Tick()
        {
            if (_isPaused || !_initialized || _currentSeason == null) return;

            UpdateSeasonProgress();
            CheckSeasonExpiration();
        }

        public void Pause(bool pause)
        {
            _isPaused = pause;
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();

            _mockServer.OnTimeSynced -= HandleTimeSynced;

            if (_loadHandle.IsValid())
                Addressables.Release(_loadHandle);

            foreach (var season in _seasonCache.Values)
            {
                // SeasonConfig 是 ScriptableObject，不需要手动释放
            }
            _seasonCache.Clear();
        }

        /// <summary>
        /// 获取当前赛季配置
        /// </summary>
        public SeasonConfig CurrentSeason => _currentSeason;

        /// <summary>
        /// 获取当前主题配置
        /// </summary>
        public ThemeProfile CurrentTheme => _currentTheme;

        /// <summary>
        /// 当前赛季剩余时间 (秒)
        /// </summary>
        public TimeSpan TimeRemaining => _currentSeason?.EndTime - GetCurrentTime() ?? TimeSpan.Zero;

        /// <summary>
        /// 赛季进度 0-1
        /// </summary>
        public float SeasonProgress
        {
            get
            {
                if (_currentSeason == null) return 0f;
                var now = GetCurrentTime();
                var total = _currentSeason.EndTime - _currentSeason.StartTime;
                var elapsed = now - _currentSeason.StartTime;
                return Mathf.Clamp01((float)elapsed.TotalSeconds / (float)total.TotalSeconds);
            }
        }

        /// <summary>
        /// 强制切换赛季 (测试/GM 命令用)
        /// </summary>
        public async UniTask<bool> ForceSwitchSeason(string seasonId)
        {
            if (!_seasonCache.TryGetValue(seasonId, out var season))
            {
                Debug.LogWarning($"[SeasonManager] Season not found: {seasonId}");
                return false;
            }

            await SwitchSeasonInternal(season);
            return true;
        }

        /// <summary>
        /// 热更赛季配置 (Addressables 更新后调用)
        /// </summary>
        public async UniTask ReloadSeasonsAsync()
        {
            Debug.Log("[SeasonManager] Reloading season configs...");
            _seasonCache.Clear();

            if (_loadHandle.IsValid())
                Addressables.Release(_loadHandle);

            await LoadAllSeasonsAsync();

            // 重新评估当前赛季
            EvaluateCurrentSeason();
        }

        #region Private Methods

        private async UniTask LoadAllSeasonsAsync()
        {
            try
            {
                _loadHandle = Addressables.LoadAssetsAsync<SeasonConfig>("SeasonConfig", OnSeasonLoaded);
                await _loadHandle.Task;

                Debug.Log($"[SeasonManager] Loaded {_seasonCache.Count} season configs");
                EvaluateCurrentSeason();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SeasonManager] Failed to load seasons: {e.Message}");
                // 离线模式：使用 Resources 备选
                if (_config.OfflineMode)
                    LoadFromResourcesFallback();
            }
        }

        private void OnSeasonLoaded(SeasonConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.SeasonId)) return;

            if (_seasonCache.ContainsKey(config.SeasonId))
            {
                Debug.LogWarning($"[SeasonManager] Duplicate season ID: {config.SeasonId}");
                return;
            }

            _seasonCache[config.SeasonId] = config;
            Debug.Log($"[SeasonManager] Loaded season: {config.SeasonId} ({config.DisplayName})");
        }

        private void LoadFromResourcesFallback()
        {
            var seasons = Resources.LoadAll<SeasonConfig>("Seasons/Configs");
            foreach (var s in seasons)
            {
                _seasonCache[s.SeasonId] = s;
            }
            Debug.Log($"[SeasonManager] Fallback loaded {seasons.Length} seasons from Resources");
            EvaluateCurrentSeason();
        }

        private void EvaluateCurrentSeason()
        {
            var now = GetCurrentTime();
            var activeSeason = _seasonCache.Values
                .Where(s => s.StartTime <= now && s.EndTime > now)
                .OrderBy(s => s.StartTime)
                .FirstOrDefault();

            if (activeSeason != null && activeSeason != _currentSeason)
            {
                SwitchSeasonInternal(activeSeason).Forget();
            }
            else if (activeSeason == null && _currentSeason != null)
            {
                // 当前赛季已结束，寻找下一个
                var nextSeason = _seasonCache.Values
                    .Where(s => s.StartTime > now)
                    .OrderBy(s => s.StartTime)
                    .FirstOrDefault();

                if (nextSeason != null)
                {
                    Debug.Log($"[SeasonManager] Current season expired. Next: {nextSeason.SeasonId}");
                    OnSeasonExpired?.Invoke();
                }
            }
        }

        private async UniTask SwitchSeasonInternal(SeasonConfig newSeason)
        {
            Debug.Log($"[SeasonManager] Switching to season: {newSeason.SeasonId}");

            var oldTheme = _currentTheme;
            _currentSeason = newSeason;
            _currentTheme = newSeason.ThemeProfile;

            // 广播赛季变更
            OnSeasonChanged?.Invoke(newSeason);
            _messageBroker.Publish(new SeasonChangedMessage(newSeason));

            // 如果主题变化，加载新主题资源并广播
            if (_currentTheme != oldTheme && _currentTheme != null)
            {
                await LoadThemeResourcesAsync(_currentTheme);
                OnThemeChanged?.Invoke(_currentTheme);
                _messageBroker.Publish(new ThemeChangedMessage(_currentTheme));
            }

            // 通知其他系统
            _messageBroker.Publish(new SeasonProgressMessage(SeasonProgress));
        }

        private async UniTask LoadThemeResourcesAsync(ThemeProfile theme)
        {
            // 预加载主题相关的 Addressables
            var labels = new[] { $"Theme_{theme.ThemeId}", "Shared_Base" };
            var handle = Addressables.DownloadDependenciesAsync(labels, true);
            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                Debug.Log($"[SeasonManager] Theme resources preloaded: {theme.ThemeId}");
            }
            else
            {
                Debug.LogWarning($"[SeasonManager] Theme preload failed: {theme.ThemeId}");
            }
        }

        private void UpdateSeasonProgress()
        {
            float progress = SeasonProgress;
            OnSeasonProgressChanged?.Invoke(progress);
            _messageBroker.Publish(new SeasonProgressMessage(progress));
        }

        private void CheckSeasonExpiration()
        {
            if (_currentSeason == null) return;

            var now = GetCurrentTime();
            if (now >= _currentSeason.EndTime)
            {
                Debug.Log($"[SeasonManager] Season expired: {_currentSeason.SeasonId}");
                OnSeasonExpired?.Invoke();
                EvaluateCurrentSeason();
            }
        }

        private void HandleTimeSynced(DateTime serverTime)
        {
            _serverTimeOffset = serverTime - DateTime.UtcNow;
            Debug.Log($"[SeasonManager] Time synced. Offset: {_serverTimeOffset.TotalSeconds:F1}s");
            EvaluateCurrentSeason();
        }

        private DateTime GetCurrentTime()
        {
            if (_config.OfflineMode)
                return DateTime.UtcNow + _serverTimeOffset;
            return _mockServer.CurrentServerTime;
        }

        #endregion

        #region Messages

        public readonly struct SeasonChangedMessage
        {
            public readonly SeasonConfig Season;
            public SeasonChangedMessage(SeasonConfig season) => Season = season;
        }

        public readonly struct ThemeChangedMessage
        {
            public readonly ThemeProfile Theme;
            public ThemeChangedMessage(ThemeProfile theme) => Theme = theme;
        }

        public readonly struct SeasonProgressMessage
        {
            public readonly float Progress;
            public SeasonProgressMessage(float progress) => Progress = progress;
        }

        #endregion
    }

    /// <summary>
    /// 可暂停接口
    /// </summary>
    public interface IPausable
    {
        void Pause(bool pause);
    }
}