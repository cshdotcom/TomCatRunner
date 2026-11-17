using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cysharp.Threading.Tasks;
using TomCatRunner.Common;

namespace TomCatRunner.Seasons
{
    /// <summary>
    /// 活动调度器 - 管理限时活动的启动、停止、进度同步
    /// </summary>
    public class EventScheduler : MonoSingleton<EventScheduler>
    {
        [Header("配置引用")]
        [SerializeField] private SeasonConfig _seasonConfig;

        [Header("调试")]
        [SerializeField] private bool _debugLog = true;
        [SerializeField] private bool _useMockServer = true;

        // 状态
        private readonly Dictionary<string, MiniEvent> _activeEvents = new Dictionary<string, MiniEvent>();
        private readonly Dictionary<string, MiniEvent> _registeredEvents = new Dictionary<string, MiniEvent>();
        private SeasonRuntimeData _runtimeData;
        private DateTime _serverTimeUtc;
        private Coroutine _updateCoroutine;
        private bool _isInitialized = false;

        // 事件
        public event Action<MiniEvent> OnEventStarted;
        public event Action<MiniEvent> OnEventEnded;
        public event Action<string, int> OnEventProgressChanged;
        public event Action<string> OnEventCompleted;

        // 属性
        public IReadOnlyDictionary<string, MiniEvent> ActiveEvents => _activeEvents;
        public bool IsInitialized => _isInitialized;

        protected override void OnInit()
        {
            base.OnInit();
        }

        /// <summary>
        /// 初始化活动调度器
        /// </summary>
        public async UniTask InitializeAsync(SeasonConfig config, SeasonRuntimeData runtimeData, DateTime serverTimeUtc)
        {
            _seasonConfig = config;
            _runtimeData = runtimeData;
            _serverTimeUtc = serverTimeUtc;

            // 注册所有活动类型
            RegisterBuiltinEvents();

            // 根据时间表启动活动
            await RefreshEventsAsync();

            // 启动更新循环
            _updateCoroutine = StartCoroutine(UpdateRoutine());

            _isInitialized = true;

            if (_debugLog) Debug.Log($"[EventScheduler] Initialized with {_activeEvents.Count} active events");
        }

        /// <summary>
        /// 注册内置活动类型
        /// </summary>
        private void RegisterBuiltinEvents()
        {
            // 代币收集活动
            RegisterEvent(new TokenCollectEvent());

            // Boss 连战活动
            RegisterEvent(new BossRushEvent());

            // 计时赛活动
            RegisterEvent(new TimeTrialEvent());

            // 每日挑战
            RegisterEvent(new DailyChallengeEvent());
        }

        /// <summary>
        /// 注册活动类型
        /// </summary>
        public void RegisterEvent(MiniEvent eventInstance)
        {
            if (_registeredEvents.ContainsKey(eventInstance.EventId))
            {
                Debug.LogWarning($"[EventScheduler] Event {eventInstance.EventId} already registered");
                return;
            }

            _registeredEvents[eventInstance.EventId] = eventInstance;
        }

        /// <summary>
        /// 根据时间表刷新活动状态
        /// </summary>
        public async UniTask RefreshEventsAsync()
        {
            if (_seasonConfig?.eventSchedules == null) return;

            var now = _serverTimeUtc;
            long nowTimestamp = ((DateTimeOffset)now).ToUnixTimeSeconds();

            foreach (var schedule in _seasonConfig.eventSchedules)
            {
                // 计算活动开始/结束时间
                long seasonStart = _seasonConfig.seasonStartTimestamp;
                long phaseStart = seasonStart;

                // 找到对应阶段的开始时间
                if (schedule.phaseIndex >= 0 && schedule.phaseIndex < _seasonConfig.phases.Length)
                {
                    phaseStart = seasonStart + _seasonConfig.phases[schedule.phaseIndex].startOffsetDays * 86400L;
                }

                long eventStart = phaseStart + schedule.startOffsetDays * 86400L;
                long eventEnd = eventStart + schedule.durationDays * 86400L;

                bool shouldBeActive = nowTimestamp >= eventStart && nowTimestamp < eventEnd;

                if (shouldBeActive && !_activeEvents.ContainsKey(schedule.eventId))
                {
                    await StartEventAsync(schedule);
                }
                else if (!shouldBeActive && _activeEvents.ContainsKey(schedule.eventId))
                {
                    await StopEventAsync(schedule.eventId);
                }
            }
        }

        private async UniTask StartEventAsync(EventSchedule schedule)
        {
            if (!_registeredEvents.TryGetValue(schedule.eventId, out var eventTemplate))
            {
                Debug.LogWarning($"[EventScheduler] Event type not registered: {schedule.eventId}");
                return;
            }

            // 创建活动实例
            var eventInstance = CreateEventInstance(eventTemplate, schedule);
            if (eventInstance == null) return;

            // 初始化进度
            if (_runtimeData.eventProgress.TryGetValue(schedule.eventId, out var progress))
            {
                eventInstance.CurrentProgress = progress.currentProgress;
                eventInstance.TargetProgress = progress.targetProgress;
                eventInstance.IsCompleted = progress.isCompleted;
                eventInstance.IsClaimed = progress.isClaimed;
            }
            else
            {
                eventInstance.TargetProgress = GetDefaultTargetProgress(schedule.type);
                _runtimeData.eventProgress[schedule.eventId] = new EventProgress
                {
                    eventId = schedule.eventId,
                    currentProgress = 0,
                    targetProgress = eventInstance.TargetProgress
                };
            }

            _activeEvents[schedule.eventId] = eventInstance;
            await eventInstance.OnEnterAsync();

            OnEventStarted?.Invoke(eventInstance);

            if (_debugLog) Debug.Log($"[EventScheduler] Started event: {schedule.eventId}");
        }

        private MiniEvent CreateEventInstance(MiniEvent template, EventSchedule schedule)
        {
            var instance = (MiniEvent)Activator.CreateInstance(template.GetType());
            instance.EventId = schedule.eventId;
            instance.EventType = schedule.type;
            instance.Parameters = schedule.parameters ?? new SerializableDictionary<string, float>();

            return instance;
        }

        private int GetDefaultTargetProgress(EventType type)
        {
            return type switch
            {
                EventType.TokenCollect => 100,
                EventType.BossRush => 5,
                EventType.TimeTrial => 1,
                EventType.DailyChallenge => 3,
                EventType.Puzzle => 10,
                _ => 100
            };
        }

        private async UniTask StopEventAsync(string eventId)
        {
            if (!_activeEvents.TryGetValue(eventId, out var eventInstance)) return;

            await eventInstance.OnExitAsync();
            _activeEvents.Remove(eventId);

            OnEventEnded?.Invoke(eventInstance);

            if (_debugLog) Debug.Log($"[EventScheduler] Stopped event: {eventId}");
        }

        private IEnumerator UpdateRoutine()
        {
            var wait = new WaitForSeconds(1f);

            while (true)
            {
                yield return wait;

                if (!_isInitialized) continue;

                // 更新活跃活动
                foreach (var eventInstance in _activeEvents.Values)
                {
                    if (!eventInstance.IsCompleted)
                    {
                        eventInstance.OnUpdate(Time.deltaTime);
                    }
                }

                // 每分钟检查一次时间表变化
                if (Time.frameCount % 3600 == 0)
                {
                    _ = RefreshEventsAsync();
                }
            }
        }

        // ===================== 公共 API =====================

        /// <summary>
        /// 获取活动实例
        /// </summary>
        public MiniEvent GetEvent(string eventId)
        {
            _activeEvents.TryGetValue(eventId, out var evt);
            return evt;
        }

        /// <summary>
        /// 增加活动进度
        /// </summary>
        public void AddEventProgress(string eventId, int amount, Dictionary<string, int> subProgress = null)
        {
            if (!_activeEvents.TryGetValue(eventId, out var eventInstance)) return;
            if (eventInstance.IsCompleted) return;

            int oldProgress = eventInstance.CurrentProgress;
            eventInstance.CurrentProgress = Mathf.Min(eventInstance.CurrentProgress + amount, eventInstance.TargetProgress);

            // 更新运行时数据
            if (_runtimeData.eventProgress.TryGetValue(eventId, out var progress))
            {
                progress.currentProgress = eventInstance.CurrentProgress;
                progress.isCompleted = eventInstance.CurrentProgress >= eventInstance.TargetProgress;

                if (subProgress != null)
                {
                    foreach (var kvp in subProgress)
                    {
                        progress.subProgress[kvp.Key] = progress.subProgress.GetValueOrDefault(kvp.Key) + kvp.Value;
                    }
                }
            }

            OnEventProgressChanged?.Invoke(eventId, eventInstance.CurrentProgress);

            // 检查完成
            if (eventInstance.CurrentProgress >= eventInstance.TargetProgress && !eventInstance.IsCompleted)
            {
                eventInstance.IsCompleted = true;
                OnEventCompleted?.Invoke(eventId);

                if (_debugLog) Debug.Log($"[EventScheduler] Event completed: {eventId}");
            }
        }

        /// <summary>
        /// 领取活动奖励
        /// </summary>
        public async UniTask<RewardBundle> ClaimEventRewardAsync(string eventId)
        {
            if (!_activeEvents.TryGetValue(eventId, out var eventInstance))
                throw new InvalidOperationException($"Event not active: {eventId}");

            if (!eventInstance.IsCompleted)
                throw new InvalidOperationException($"Event not completed: {eventId}");

            if (_runtimeData.eventProgress.TryGetValue(eventId, out var progress) && progress.isClaimed)
                throw new InvalidOperationException($"Reward already claimed: {eventId}");

            var reward = eventInstance.GetRewardBundle();
            progress.isClaimed = true;
            eventInstance.IsClaimed = true;

            // 发放奖励
            foreach (var item in reward.items)
            {
                // await GrantItemAsync(item);
            }

            return reward;
        }

        /// <summary>
        /// 获取所有活动的视图数据
        /// </summary>
        public List<EventViewData> GetAllEventsViewData()
        {
            var result = new List<EventViewData>();

            foreach (var kvp in _activeEvents)
            {
                var evt = kvp.Value;
                var progress = _runtimeData.eventProgress.GetValueOrDefault(kvp.Key);

                result.Add(new EventViewData
                {
                    eventId = evt.EventId,
                    eventType = evt.EventType,
                    displayName = evt.DisplayName,
                    description = evt.Description,
                    icon = evt.Icon,
                    currentProgress = evt.CurrentProgress,
                    targetProgress = evt.TargetProgress,
                    progressNormalized = evt.TargetProgress > 0 ? (float)evt.CurrentProgress / evt.TargetProgress : 0f,
                    isCompleted = evt.IsCompleted,
                    isClaimed = evt.IsClaimed,
                    canClaim = evt.IsCompleted && !evt.IsClaimed,
                    timeRemaining = evt.GetTimeRemaining(),
                    rewardBundle = evt.GetRewardBundle()
                });
            }

            return result;
        }

        protected override void OnDispose()
        {
            if (_updateCoroutine != null) StopCoroutine(_updateCoroutine);

            foreach (var evt in _activeEvents.Values)
            {
                evt.OnExitAsync().Forget();
            }
            _activeEvents.Clear();
            _registeredEvents.Clear();

            base.OnDispose();
        }
    }

    /// <summary>
    /// 活动视图数据
    /// </summary>
    [Serializable]
    public class EventViewData
    {
        public string eventId;
        public EventType eventType;
        public string displayName;
        public string description;
        public Sprite icon;
        public int currentProgress;
        public int targetProgress;
        public float progressNormalized;
        public bool isCompleted;
        public bool isClaimed;
        public bool canClaim;
        public TimeSpan timeRemaining;
        public RewardBundle rewardBundle;
    }

    // ===================== 活动基类 =====================

    /// <summary>
    /// 迷你活动基类
    /// </summary>
    public abstract class MiniEvent
    {
        public string EventId { get; set; }
        public EventType EventType { get; set; }
        public virtual string DisplayName => EventId;
        public virtual string Description => "";
        public virtual Sprite Icon => null;
        public SerializableDictionary<string, float> Parameters { get; set; } = new SerializableDictionary<string, float>();

        // 运行时状态
        public int CurrentProgress { get; set; }
        public int TargetProgress { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsClaimed { get; set; }

        public virtual UniTask OnEnterAsync() => UniTask.CompletedTask;
        public virtual UniTask OnExitAsync() => UniTask.CompletedTask;
        public virtual void OnUpdate(float deltaTime) { }
        public virtual RewardBundle GetRewardBundle() => null;
        public virtual TimeSpan GetTimeRemaining() => TimeSpan.Zero;
    }

    // ===================== 内置活动实现 =====================

    /// <summary>
    /// 代币收集活动
    /// </summary>
    public class TokenCollectEvent : MiniEvent
    {
        private float _tokenSpawnTimer;
        private float _tokenSpawnInterval = 5f;

        public override string DisplayName => "霓虹代币收集";
        public override string Description => "在跑酷中收集霓虹代币，集齐可兑换专属奖励";

        public override UniTask OnEnterAsync()
        {
            _tokenSpawnTimer = 0f;
            if (Parameters.TryGetValue("spawn_interval", out float interval))
                _tokenSpawnInterval = interval;
            return base.OnEnterAsync();
        }

        public override void OnUpdate(float deltaTime)
        {
            _tokenSpawnTimer += deltaTime;
            // 实际生成逻辑由 LevelStreamer/Chunk 处理，这里只计时
        }

        public void OnTokenCollected(int amount = 1)
        {
            // 由外部调用 (如 PlayerController)
            EventScheduler.Instance?.AddEventProgress(EventId, amount);
        }

        public override RewardBundle GetRewardBundle()
        {
            return new RewardBundle
            {
                items = new RewardItem[]
                {
                    new RewardItem { type = RewardType.Skin, itemId = "skin_neon_token_collector", quantity = 1, rarity = Rarity.Epic },
                    new RewardItem { type = RewardType.Currency, itemId = "gems", quantity = 500, rarity = Rarity.Rare },
                    new RewardItem { type = RewardType.ProfileFrame, itemId = "frame_neon_collector", quantity = 1, rarity = Rarity.Epic }
                }
            };
        }
    }

    /// <summary>
    /// Boss 连战活动
    /// </summary>
    public class BossRushEvent : MiniEvent
    {
        private int _bossesDefeated = 0;
        private int _requiredBosses = 5;

        public override string DisplayName => "赛博 Boss 连战";
        public override string Description => "连续挑战多个强化 Boss，测试你的极限操作";

        public override UniTask OnEnterAsync()
        {
            _bossesDefeated = 0;
            if (Parameters.TryGetValue("required_bosses", out float required))
                _requiredBosses = Mathf.RoundToInt(required);
            TargetProgress = _requiredBosses;
            return base.OnEnterAsync();
        }

        public void OnBossDefeated(string bossId)
        {
            _bossesDefeated++;
            EventScheduler.Instance?.AddEventProgress(EventId, 1);
        }

        public override RewardBundle GetRewardBundle()
        {
            return new RewardBundle
            {
                items = new RewardItem[]
                {
                    new RewardItem { type = RewardType.Mount, itemId = "mount_cyber_boss", quantity = 1, rarity = Rarity.Legendary },
                    new RewardItem { type = RewardType.Currency, itemId = "gems", quantity = 1000, rarity = Rarity.Epic },
                    new RewardItem { type = RewardType.Title, itemId = "title_boss_slayer", quantity = 1, rarity = Rarity.Legendary }
                }
            };
        }
    }

    /// <summary>
    /// 计时赛活动
    /// </summary>
    public class TimeTrialEvent : MiniEvent
    {
        private float _bestTime = float.MaxValue;
        private float _targetTime = 60f; // 秒

        public override string DisplayName => "极速计时赛";
        public override string Description => "在规定时间内冲刺终点，挑战最速记录";

        public override UniTask OnEnterAsync()
        {
            _bestTime = float.MaxValue;
            if (Parameters.TryGetValue("target_time", out float time))
                _targetTime = time;
            TargetProgress = 1; // 完成一次即可
            return base.OnEnterAsync();
        }

        public void OnTrialCompleted(float completionTime)
        {
            if (completionTime < _bestTime)
            {
                _bestTime = completionTime;
            }

            if (completionTime <= _targetTime)
            {
                EventScheduler.Instance?.AddEventProgress(EventId, 1);
            }
        }

        public override RewardBundle GetRewardBundle()
        {
            return new RewardBundle
            {
                items = new RewardItem[]
                {
                    new RewardItem { type = RewardType.Skin, itemId = "skin_speed_demon", quantity = 1, rarity = Rarity.Epic },
                    new RewardItem { type = RewardType.Currency, itemId = "coins", quantity = 10000, rarity = Rarity.Rare },
                    new RewardItem { type = RewardType.Emote, itemId = "emote_speed_lines", quantity = 1, rarity = Rarity.Rare }
                }
            };
        }
    }

    /// <summary>
    /// 每日挑战活动
    /// </summary>
    public class DailyChallengeEvent : MiniEvent
    {
        private string _currentChallengeId;
        private int _challengesCompleted = 0;
        private int _requiredChallenges = 3;

        public override string DisplayName => "每日挑战";
        public override string Description => "完成每日指定任务，累积进度领取丰厚奖励";

        public override UniTask OnEnterAsync()
        {
            _challengesCompleted = 0;
            if (Parameters.TryGetValue("required_challenges", out float required))
                _requiredChallenges = Mathf.RoundToInt(required);
            TargetProgress = _requiredChallenges;
            RefreshDailyChallenge();
            return base.OnEnterAsync();
        }

        public void OnChallengeCompleted(string challengeId)
        {
            if (challengeId == _currentChallengeId)
            {
                _challengesCompleted++;
                EventScheduler.Instance?.AddEventProgress(EventId, 1);
                RefreshDailyChallenge();
            }
        }

        private void RefreshDailyChallenge()
        {
            // 从挑战池中随机选择
            var challenges = new[] { "distance_1000", "coins_500", "enemies_20", "perfect_run", "no_damage" };
            _currentChallengeId = challenges[UnityEngine.Random.Range(0, challenges.Length)];
        }

        public override RewardBundle GetRewardBundle()
        {
            return new RewardBundle
            {
                items = new RewardItem[]
                {
                    new RewardItem { type = RewardType.Currency, itemId = "gems", quantity = 200, rarity = Rarity.Uncommon },
                    new RewardItem { type = RewardType.LootBox, itemId = "box_daily", quantity = 1, rarity = Rarity.Rare },
                    new RewardItem { type = RewardType.Experience, itemId = "bp_xp", quantity = 5000, rarity = Rarity.Common }
                }
            };
        }
    }
}