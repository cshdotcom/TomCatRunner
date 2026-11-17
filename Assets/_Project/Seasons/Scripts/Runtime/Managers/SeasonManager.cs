using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;
using TomCatRunner.Common;

namespace TomCatRunner.Seasons
{
    /// <summary>
    /// 赛季管理器 - 核心单例，管理赛季状态、配置加载、时间同步
    /// </summary>
    public class SeasonManager : MonoSingleton<SeasonManager>
    {
        [Header("配置引用")]
        [SerializeField] private AssetReference _seasonConfigRef;
        [SerializeField] private SeasonConfig _fallbackConfig;

        [Header("同步设置")]
        [SerializeField] private float _syncIntervalSeconds = 300f;  // 5分钟
        [SerializeField] private float _timeSyncToleranceMs = 1000f;

        [Header("调试")]
        [SerializeField] private bool _useMockServer = true;
        [SerializeField] private bool _debugLog = true;

        // 状态
        private SeasonConfig _currentConfig;
        private SeasonRuntimeData _runtimeData;
        private ThemeProfile _activeTheme;
        private DateTime _serverTimeUtc;
        private float _localTimeOffset;  // 本地时间与服务器时间的差值
        private Coroutine _syncCoroutine;
        private bool _isInitialized = false;

        // 事件
        public event Action<SeasonConfig> OnSeasonConfigLoaded;
        public event Action<SeasonPhase> OnPhaseChanged;
        public event Action<ThemeProfile> OnActiveThemeChanged;
        public event Action<SeasonRuntimeData> OnRuntimeDataUpdated;
        public event Action OnSeasonInitialized;

        // 属性
        public SeasonConfig CurrentConfig => _currentConfig;
        public SeasonRuntimeData RuntimeData => _runtimeData;
        public ThemeProfile ActiveTheme => _activeTheme;
        public SeasonPhase CurrentPhase => _runtimeData?.currentPhase ?? SeasonPhase.Intermission;
        public DateTime ServerTimeUtc => _serverTimeUtc;
        public bool IsInitialized => _isInitialized;
        public bool IsSeasonActive => CurrentPhase == SeasonPhase.Active;
        public bool IsInPreheat => CurrentPhase == SeasonPhase.Preheat;

        protected override void OnInit()
        {
            base.OnInit();
            _runtimeData = new SeasonRuntimeData();
            StartInitialization();
        }

        private async void StartInitialization()
        {
            try
            {
                await InitializeAsync();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SeasonManager] Initialization failed: {e}");
                // 使用 fallback 配置
                if (_fallbackConfig != null)
                {
                    ApplyConfig(_fallbackConfig);
                    _isInitialized = true;
                    OnSeasonInitialized?.Invoke();
                }
            }
        }

        public async UniTask InitializeAsync()
        {
            if (_debugLog) Debug.Log("[SeasonManager] Starting initialization...");

            // 1. 加载赛季配置
            await LoadSeasonConfigAsync();

            // 2. 同步服务器时间
            await SyncServerTimeAsync();

            // 3. 初始化运行时数据
            await InitializeRuntimeDataAsync();

            // 4. 确定当前阶段和主题
            UpdatePhaseAndTheme();

            // 5. 启动定期同步
            StartPeriodicSync();

            _isInitialized = true;
            OnSeasonInitialized?.Invoke();

            if (_debugLog) Debug.Log($"[SeasonManager] Initialized. Phase: {CurrentPhase}, Theme: {_activeTheme?.themeId}");
        }

        private async UniTask LoadSeasonConfigAsync()
        {
            if (_seasonConfigRef.RuntimeKeyIsValid())
            {
                var handle = _seasonConfigRef.LoadAssetAsync<SeasonConfig>();
                await handle.Task;

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    ApplyConfig(handle.Result);
                }
                else
                {
                    throw new Exception($"Failed to load SeasonConfig: {handle.OperationException}");
                }
            }
            else if (_fallbackConfig != null)
            {
                ApplyConfig(_fallbackConfig);
            }
            else
            {
                throw new Exception("No SeasonConfig available");
            }
        }

        private void ApplyConfig(SeasonConfig config)
        {
            _currentConfig = config;
            OnSeasonConfigLoaded?.Invoke(config);

            if (_debugLog) Debug.Log($"[SeasonManager] Config loaded: Season {config.seasonId} - {config.seasonName}");
        }

        private async UniTask SyncServerTimeAsync()
        {
            if (_useMockServer)
            {
                _serverTimeUtc = DateTime.UtcNow;
                _localTimeOffset = 0f;
                return;
            }

            // 真实服务器时间同步 (NTP 风格)
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var request = await UnityEngine.Networking.UnityWebRequest.Get("https://worldtimeapi.org/api/ip").SendWebRequest();
                stopwatch.Stop();

                if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<TimeApiResponse>(request.downloadHandler.text);
                    _serverTimeUtc = DateTime.Parse(response.utc_datetime).ToUniversalTime();
                    _localTimeOffset = (float)(stopwatch.ElapsedMilliseconds / 2.0);
                }
                else
                {
                    throw new Exception(request.error);
                }
            }
            catch
            {
                // 回退到本地时间
                _serverTimeUtc = DateTime.UtcNow;
                _localTimeOffset = 0f;
                Debug.LogWarning("[SeasonManager] Server time sync failed, using local time");
            }
        }

        private async UniTask InitializeRuntimeDataAsync()
        {
            if (_useMockServer)
            {
                // 生成模拟数据
                _runtimeData = MockServer.GenerateRuntimeData(_currentConfig.seasonId);
                OnRuntimeDataUpdated?.Invoke(_runtimeData);
                return;
            }

            // 真实服务器请求
            try
            {
                // await ApiClient.GetSeasonRuntimeData(_currentConfig.seasonId);
            }
            catch
            {
                _runtimeData = new SeasonRuntimeData { seasonId = _currentConfig.seasonId };
            }
            OnRuntimeDataUpdated?.Invoke(_runtimeData);
        }

        private void UpdatePhaseAndTheme()
        {
            if (_currentConfig == null) return;

            var newPhase = _currentConfig.GetCurrentPhase(_serverTimeUtc);
            var newTheme = _currentConfig.GetActiveTheme(_serverTimeUtc);

            bool phaseChanged = _runtimeData.currentPhase != newPhase;
            bool themeChanged = _activeTheme != newTheme;

            _runtimeData.currentPhase = newPhase;
            _activeTheme = newTheme;

            if (phaseChanged)
            {
                OnPhaseChanged?.Invoke(newPhase);
                if (_debugLog) Debug.Log($"[SeasonManager] Phase changed: {newPhase}");
            }

            if (themeChanged)
            {
                OnActiveThemeChanged?.Invoke(newTheme);
                if (_debugLog) Debug.Log($"[SeasonManager] Theme changed: {newTheme?.themeId}");
            }
        }

        private void StartPeriodicSync()
        {
            if (_syncCoroutine != null) StopCoroutine(_syncCoroutine);
            _syncCoroutine = StartCoroutine(PeriodicSyncRoutine());
        }

        private IEnumerator PeriodicSyncRoutine()
        {
            var wait = new WaitForSeconds(_syncIntervalSeconds);

            while (true)
            {
                yield return wait;

                if (!_isInitialized) continue;

                try
                {
                    // 同步时间
                    var oldServerTime = _serverTimeUtc;
                    var syncTask = SyncServerTimeAsync();
                    yield return new WaitUntil(() => syncTask.IsCompleted);

                    // 检查时间是否有显著变化
                    if (Math.Abs((_serverTimeUtc - oldServerTime).TotalSeconds) > _timeSyncToleranceMs / 1000f)
                    {
                        // 重新评估阶段
                        UpdatePhaseAndTheme();

                        // 同步运行时数据
                        var dataTask = InitializeRuntimeDataAsync();
                        yield return new WaitUntil(() => dataTask.IsCompleted);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SeasonManager] Periodic sync error: {e}");
                }
            }
        }

        // ===================== 公共 API =====================

        /// <summary>
        /// 获取当前服务器时间 (考虑本地偏移)
        /// </summary>
        public DateTime GetServerTimeNow()
        {
            return _serverTimeUtc.AddSeconds(Time.unscaledTime - _localTimeOffset);
        }

        /// <summary>
        /// 检查通行证奖励是否可领取
        /// </summary>
        public bool CanClaimReward(int tierLevel, RewardTrack track)
        {
            if (_runtimeData == null || _currentConfig?.battlePassTable == null) return false;
            if (tierLevel > _runtimeData.battlePassLevel) return false;

            var list = track == RewardTrack.Free ? _runtimeData.claimedFreeTiers : _runtimeData.claimedPremiumTiers;
            return !list.Contains(tierLevel);
        }

        /// <summary>
        /// 领取通行证奖励
        /// </summary>
        public async UniTask<RewardBundle> ClaimRewardAsync(int tierLevel, RewardTrack track)
        {
            if (!CanClaimReward(tierLevel, track))
                throw new InvalidOperationException("Cannot claim this reward");

            var tier = _currentConfig.battlePassTable.tiers.Find(t => t.level == tierLevel);
            if (tier == null) throw new ArgumentException($"Tier {tierLevel} not found");

            var reward = track == RewardTrack.Free ? tier.freeReward : tier.premiumReward;

            if (track == RewardTrack.Free)
                _runtimeData.claimedFreeTiers.Add(tierLevel);
            else
                _runtimeData.claimedPremiumTiers.Add(tierLevel);

            OnRuntimeDataUpdated?.Invoke(_runtimeData);

            // 同步到服务器
            if (!_useMockServer)
            {
                // await ApiClient.ClaimBattlePassReward(_currentConfig.seasonId, tierLevel, track);
            }

            return reward;
        }

        /// <summary>
        /// 增加通行证经验
        /// </summary>
        public void AddBattlePassXp(int xp, string source = "Gameplay")
        {
            if (_runtimeData == null || _currentConfig?.battlePassTable == null) return;

            // 检查周上限
            int newXp = _runtimeData.battlePassXp + xp;
            int maxXp = _currentConfig.battlePassTable.weeklyXpCap;

            if (_runtimeData.battlePassXp >= maxXp)
            {
                if (_debugLog) Debug.Log("[SeasonManager] Weekly XP cap reached");
                return;
            }

            _runtimeData.battlePassXp = Mathf.Min(newXp, maxXp);

            // 计算等级
            int newLevel = CalculateLevelFromXp(_runtimeData.battlePassXp);
            if (newLevel > _runtimeData.battlePassLevel)
            {
                _runtimeData.battlePassLevel = newLevel;
                if (_debugLog) Debug.Log($"[SeasonManager] Battle Pass Level Up: {newLevel}");
            }

            OnRuntimeDataUpdated?.Invoke(_runtimeData);
        }

        private int CalculateLevelFromXp(int xp)
        {
            var table = _currentConfig.battlePassTable;
            for (int i = table.tiers.Length - 1; i >= 0; i--)
            {
                if (xp >= table.tiers[i].xpRequired)
                    return table.tiers[i].level;
            }
            return 1;
        }

        /// <summary>
        /// 获取活动进度
        /// </summary>
        public EventProgress GetEventProgress(string eventId)
        {
            if (_runtimeData?.eventProgress.TryGetValue(eventId, out var progress) == true)
                return progress;
            return null;
        }

        /// <summary>
        /// 更新活动进度
        /// </summary>
        public void UpdateEventProgress(string eventId, int delta, Dictionary<string, int> subProgress = null)
        {
            if (_runtimeData == null) return;

            if (!_runtimeData.eventProgress.TryGetValue(eventId, out var progress))
            {
                progress = new EventProgress { eventId = eventId };
                _runtimeData.eventProgress[eventId] = progress;
            }

            progress.currentProgress = Mathf.Min(progress.currentProgress + delta, progress.targetProgress);
            progress.isCompleted = progress.currentProgress >= progress.targetProgress;

            if (subProgress != null)
            {
                foreach (var kvp in subProgress)
                {
                    progress.subProgress[kvp.Key] = progress.subProgress.GetValueOrDefault(kvp.Key) + kvp.Value;
                }
            }

            OnRuntimeDataUpdated?.Invoke(_runtimeData);
        }

        /// <summary>
        /// 获取距离赛季结束的剩余时间
        /// </summary>
        public TimeSpan GetTimeUntilSeasonEnd()
        {
            if (_currentConfig == null) return TimeSpan.Zero;
            var end = DateTimeOffset.FromUnixTimeSeconds(_currentConfig.seasonEndTimestamp).UtcDateTime;
            var remaining = end - GetServerTimeNow();
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        /// <summary>
        /// 获取距离下一阶段的剩余时间
        /// </summary>
        public TimeSpan GetTimeUntilNextPhase()
        {
            if (_currentConfig == null) return TimeSpan.Zero;

            long now = ((DateTimeOffset)GetServerTimeNow()).ToUnixTimeSeconds();

            switch (CurrentPhase)
            {
                case SeasonPhase.Preheat:
                    return TimeSpan.FromSeconds(_currentConfig.seasonStartTimestamp - now);
                case SeasonPhase.Active:
                    return TimeSpan.FromSeconds(_currentConfig.seasonEndTimestamp - now);
                case SeasonPhase.Settling:
                    return TimeSpan.FromSeconds(_currentConfig.settlingEndTimestamp - now);
                default:
                    return TimeSpan.Zero;
            }
        }

        protected override void OnDispose()
        {
            if (_syncCoroutine != null) StopCoroutine(_syncCoroutine);
            base.OnDispose();
        }

        // ===================== 调试/测试支持 =====================

        [ContextMenu("Force Sync")]
        public void ForceSync()
        {
            if (_isInitialized)
            {
                _ = SyncServerTimeAsync().ContinueWith(_ => UpdatePhaseAndTheme());
                _ = InitializeRuntimeDataAsync();
            }
        }

        [ContextMenu("Simulate Phase Change")]
        public void SimulatePhaseChange()
        {
            var phases = System.Enum.GetValues(typeof(SeasonPhase));
            int current = (int)CurrentPhase;
            int next = (current + 1) % phases.Length;
            _runtimeData.currentPhase = (SeasonPhase)phases.GetValue(next);
            UpdatePhaseAndTheme();
        }
    }

    // ===================== Mock Server (用于离线测试) =====================

    public static class MockServer
    {
        public static SeasonRuntimeData GenerateRuntimeData(int seasonId)
        {
            return new SeasonRuntimeData
            {
                seasonId = seasonId,
                currentPhase = SeasonPhase.Active,
                serverTimeUtc = DateTime.UtcNow,
                battlePassLevel = UnityEngine.Random.Range(1, 50),
                battlePassXp = UnityEngine.Random.Range(0, 50000),
                isPremiumUnlocked = UnityEngine.Random.value > 0.5f,
                claimedFreeTiers = new List<int>(),
                claimedPremiumTiers = new List<int>(),
                eventProgress = new Dictionary<string, EventProgress>
                {
                    ["daily_challenge"] = new EventProgress
                    {
                        eventId = "daily_challenge",
                        currentProgress = UnityEngine.Random.Range(0, 5),
                        targetProgress = 5,
                        isCompleted = false
                    },
                    ["token_collect"] = new EventProgress
                    {
                        eventId = "token_collect",
                        currentProgress = UnityEngine.Random.Range(0, 100),
                        targetProgress = 100,
                        isCompleted = false
                    }
                },
                dailyRewards = new Dictionary<string, int>(),
                lastLoginTimestamp = ((DateTimeOffset)DateTime.UtcNow.AddDays(-1)).ToUnixTimeSeconds(),
                currentStreak = UnityEngine.Random.Range(1, 30)
            };
        }

        public static SeasonConfig GenerateTestConfig()
        {
            var config = ScriptableObject.CreateInstance<SeasonConfig>();
            config.seasonId = 1;
            config.seasonName = "Neon Cyber Season";
            config.shortCode = "S01";
            config.preheatStartTimestamp = ((DateTimeOffset)DateTime.UtcNow.AddDays(-7)).ToUnixTimeSeconds();
            config.seasonStartTimestamp = ((DateTimeOffset)DateTime.UtcNow.AddDays(-1)).ToUnixTimeSeconds();
            config.seasonEndTimestamp = ((DateTimeOffset)DateTime.UtcNow.AddDays(30)).ToUnixTimeSeconds();
            config.settlingEndTimestamp = ((DateTimeOffset)DateTime.UtcNow.AddDays(37)).ToUnixTimeSeconds();

            config.phases = new SeasonPhaseConfig[]
            {
                new SeasonPhaseConfig { phaseName = "Launch", themeIndex = 0, startOffsetDays = 0, durationDays = 14 },
                new SeasonPhaseConfig { phaseName = "Mid-Season", themeIndex = 1, startOffsetDays = 14, durationDays = 14 },
                new SeasonPhaseConfig { phaseName = "Finale", themeIndex = 2, startOffsetDays = 28, durationDays = 7 }
            };

            config.difficultyCurve = ScriptableObject.CreateInstance<DifficultyCurve>();

            return config;
        }
    }

    [Serializable]
    private class TimeApiResponse
    {
        public string utc_datetime;
        public string timezone;
    }
}