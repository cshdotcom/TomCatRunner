using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cysharp.Threading.Tasks;
using TomCatRunner.Common;

namespace TomCatRunner.Seasons
{
    /// <summary>
    /// 模拟服务器 - 离线开发模式下模拟后端数据
    /// 支持：时间同步、赛季配置、活动进度、通行证数据、商品信息
    /// 数据结构与 SeasonData.cs (Schema A) 完全一致
    /// </summary>
    public class MockServer : MonoSingleton<MockServer>
    {
        [Header("模拟配置")]
        [SerializeField] private bool _enableMockServer = true;
        [SerializeField] private float _networkLatencyMs = 100f;  // 模拟网络延迟
        [SerializeField] private float _timeOffsetMinutes = 0f;   // 服务器时间偏移

        [Header("测试数据")]
        [SerializeField] private SeasonConfig _testSeasonConfig;
        [SerializeField] private BattlePassTable _testBattlePassTable;
        [SerializeField] private ThemeProfile[] _testThemeProfiles;

        // 内部状态
        private DateTime _serverStartTimeUtc;
        private SeasonRuntimeData _mockRuntimeData;
        private bool _isInitialized = false;

        // 事件
        public event Action<DateTime> OnServerTimeSynced;
        public event Action<SeasonRuntimeData> OnRuntimeDataRefreshed;

        // 属性
        public bool IsMockMode => _enableMockServer;
        public DateTime ServerTimeUtc => _serverStartTimeUtc.AddMinutes(_timeOffsetMinutes).AddSeconds(Time.realtimeSinceStartup);
        public SeasonRuntimeData RuntimeData => _mockRuntimeData;
        public bool IsInitialized => _isInitialized;

        protected override void OnInit()
        {
            base.OnInit();
            _serverStartTimeUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// 初始化模拟服务器
        /// </summary>
        public async UniTask InitializeAsync()
        {
            if (!_enableMockServer)
            {
                Debug.Log("[MockServer] Mock mode disabled");
                return;
            }

            await SimulateNetworkDelay();

            // 创建或加载运行时数据
            _mockRuntimeData = CreateMockRuntimeData();

            // 生成测试用配置（如果未指定）
            if (_testSeasonConfig == null)
            {
                _testSeasonConfig = GenerateTestSeasonConfig();
            }

            if (_testBattlePassTable == null)
            {
                _testBattlePassTable = GenerateTestBattlePassTable();
            }

            _testSeasonConfig.battlePassTable = _testBattlePassTable;

            if (_testThemeProfiles == null || _testThemeProfiles.Length == 0)
            {
                _testThemeProfiles = GenerateTestThemeProfiles();
            }

            _isInitialized = true;
            OnServerTimeSynced?.Invoke(ServerTimeUtc);

            Debug.Log($"[MockServer] Initialized. Season: {_testSeasonConfig.seasonName}, " +
                      $"Phase: {_mockRuntimeData.currentPhase}, BP Level: {_mockRuntimeData.battlePassLevel}");
        }

        private async UniTask SimulateNetworkDelay()
        {
            int ms = Mathf.Max(1, (int)_networkLatencyMs);
            await UniTask.Delay(ms);
        }

        /// <summary>
        /// 创建模拟运行时数据（Schema A: SeasonRuntimeData）
        /// </summary>
        private SeasonRuntimeData CreateMockRuntimeData()
        {
            var config = _testSeasonConfig != null ? _testSeasonConfig : GenerateTestSeasonConfig();
            var claimedFree = new List<int>();
            var claimedPremium = new List<int>();
            int level = UnityEngine.Random.Range(10, 40);
            for (int i = 1; i <= level; i++)
            {
                claimedFree.Add(i);
                if (UnityEngine.Random.value > 0.4f) claimedPremium.Add(i);
            }

            return new SeasonRuntimeData
            {
                seasonId = config.seasonId,
                currentPhase = SeasonPhase.Active,
                serverTimeUtc = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds(),
                battlePassLevel = level,
                battlePassXp = UnityEngine.Random.Range(0, 20000),
                isPremiumUnlocked = UnityEngine.Random.value > 0.5f,
                claimedFreeTiers = claimedFree,
                claimedPremiumTiers = claimedPremium,
                eventProgress = new Dictionary<string, EventProgress>
                {
                    ["token_collect_01"] = new EventProgress
                    {
                        eventId = "token_collect_01",
                        currentProgress = 67,
                        targetProgress = 100,
                        isCompleted = false,
                        isClaimed = false,
                        subProgress = new Dictionary<string, int> { ["neon_token"] = 67 }
                    },
                    ["boss_rush_01"] = new EventProgress
                    {
                        eventId = "boss_rush_01",
                        currentProgress = 3,
                        targetProgress = 5,
                        isCompleted = false,
                        isClaimed = false,
                        subProgress = new Dictionary<string, int> { ["boss_cyber"] = 1, ["boss_void"] = 1, ["boss_neon"] = 1 }
                    },
                    ["daily_challenge_01"] = new EventProgress
                    {
                        eventId = "daily_challenge_01",
                        currentProgress = 2,
                        targetProgress = 3,
                        isCompleted = false,
                        isClaimed = false,
                        subProgress = new Dictionary<string, int> { ["distance_1000"] = 1, ["coins_500"] = 1 }
                    }
                },
                dailyRewards = new Dictionary<string, int> { ["streak_day"] = 3 },
                lastLoginTimestamp = ((DateTimeOffset)DateTime.UtcNow.AddDays(-1)).ToUnixTimeSeconds(),
                currentStreak = UnityEngine.Random.Range(1, 30)
            };
        }

        /// <summary>
        /// 生成测试用赛季配置
        /// </summary>
        private SeasonConfig GenerateTestSeasonConfig()
        {
            var config = ScriptableObject.CreateInstance<SeasonConfig>();
            config.seasonId = 1;
            config.seasonName = "霓虹觉醒";
            config.shortCode = "S01";
            config.preheatStartTimestamp = ((DateTimeOffset)DateTime.UtcNow.AddDays(-14)).ToUnixTimeSeconds();
            config.seasonStartTimestamp = ((DateTimeOffset)DateTime.UtcNow.AddDays(-10)).ToUnixTimeSeconds();
            config.seasonEndTimestamp = ((DateTimeOffset)DateTime.UtcNow.AddDays(50)).ToUnixTimeSeconds();
            config.settlingEndTimestamp = ((DateTimeOffset)DateTime.UtcNow.AddDays(57)).ToUnixTimeSeconds();
            config.configVersion = 1;

            config.phases = new SeasonPhaseConfig[]
            {
                new SeasonPhaseConfig
                {
                    phaseName = "霓虹初现",
                    themeIndex = 0,
                    startOffsetDays = 0,
                    durationDays = 14,
                    unlockableContent = new string[] { "skin_neon_runner", "trail_neon_blue" },
                    featuredEvents = new EventType[] { EventType.DailyChallenge }
                },
                new SeasonPhaseConfig
                {
                    phaseName = "数据洪流",
                    themeIndex = 0,
                    startOffsetDays = 14,
                    durationDays = 16,
                    unlockableContent = new string[] { "skin_data_diver", "mount_data_stream" },
                    featuredEvents = new EventType[] { EventType.TokenCollect, EventType.BossRush }
                },
                new SeasonPhaseConfig
                {
                    phaseName = "虚空回响",
                    themeIndex = 0,
                    startOffsetDays = 30,
                    durationDays = 15,
                    unlockableContent = new string[] { "skin_void_walker", "emote_void_touch" },
                    featuredEvents = new EventType[] { EventType.TimeTrial, EventType.BossRush }
                },
                new SeasonPhaseConfig
                {
                    phaseName = "终末冲刺",
                    themeIndex = 0,
                    startOffsetDays = 45,
                    durationDays = 5,
                    unlockableContent = new string[] { "title_season_finale" },
                    featuredEvents = new EventType[] { EventType.Puzzle }
                }
            };

            config.eventSchedules = new EventSchedule[]
            {
                new EventSchedule
                {
                    eventId = "token_collect_01",
                    type = EventType.TokenCollect,
                    phaseIndex = 0,
                    startOffsetDays = 0,
                    durationDays = 60,
                    rewardIds = new string[] { "skin_neon_token_collector", "gems_500" },
                    parameters = new SerializableDictionary<string, float> { ["spawn_interval"] = 5f }
                },
                new EventSchedule
                {
                    eventId = "boss_rush_01",
                    type = EventType.BossRush,
                    phaseIndex = 1,
                    startOffsetDays = 2,
                    durationDays = 30,
                    rewardIds = new string[] { "mount_cyber_boss", "gems_1000" },
                    parameters = new SerializableDictionary<string, float> { ["required_bosses"] = 5f }
                },
                new EventSchedule
                {
                    eventId = "daily_challenge_01",
                    type = EventType.DailyChallenge,
                    phaseIndex = 0,
                    startOffsetDays = 0,
                    durationDays = 60,
                    rewardIds = new string[] { "box_daily", "gems_200" },
                    parameters = new SerializableDictionary<string, float> { ["required_challenges"] = 3f }
                }
            };

            config.difficultyCurve = GenerateTestDifficultyCurve();

            return config;
        }

        /// <summary>
        /// 生成测试难度曲线
        /// </summary>
        private DifficultyCurve GenerateTestDifficultyCurve()
        {
            var curve = ScriptableObject.CreateInstance<DifficultyCurve>();
            curve.laneChangeFrequency = AnimationCurve.Linear(0f, 0.2f, 1f, 0.9f);
            curve.obstacleDensity = AnimationCurve.Linear(0f, 0.15f, 1f, 0.85f);
            curve.speedMultiplier = AnimationCurve.Linear(0f, 1f, 1f, 1.9f);
            curve.reactionTimeWindow = AnimationCurve.Linear(0f, 1f, 1f, 0.4f);
            curve.powerupFrequency = AnimationCurve.Constant(0f, 1f, 0.25f);
            return curve;
        }

        /// <summary>
        /// 生成测试用通行证奖励表
        /// </summary>
        private BattlePassTable GenerateTestBattlePassTable()
        {
            var table = ScriptableObject.CreateInstance<BattlePassTable>();
            table.maxLevel = 50;
            table.weeklyXpCap = 20000;
            table.xpCurve = AnimationCurve.Linear(1, 500, 50, 15000);

            table.tiers = new BattlePassTier[50];
            for (int i = 0; i < 50; i++)
            {
                int level = i + 1;
                int xpRequired = Mathf.RoundToInt(table.xpCurve.Evaluate(level));

                table.tiers[i] = new BattlePassTier
                {
                    level = level,
                    xpRequired = xpRequired,
                    freeReward = new RewardBundle
                    {
                        items = level % 5 == 0
                            ? new[]
                            {
                                new RewardItem { type = RewardType.Currency, itemId = "coins", quantity = level * 100, rarity = Rarity.Common },
                                new RewardItem { type = RewardType.LootBox, itemId = "box_daily", quantity = 1, rarity = Rarity.Rare }
                            }
                            : new[]
                            {
                                new RewardItem { type = RewardType.Experience, itemId = "bp_xp", quantity = 100, rarity = Rarity.Common }
                            }
                    },
                    premiumReward = new RewardBundle
                    {
                        items = new RewardItem[]
                        {
                            new RewardItem { type = RewardType.Currency, itemId = "gems", quantity = level * 10, rarity = Rarity.Uncommon },
                            level % 10 == 0
                                ? new RewardItem { type = RewardType.Skin, itemId = $"skin_neon_{level}", quantity = 1, rarity = Rarity.Epic }
                                : new RewardItem { type = RewardType.Currency, itemId = "coins", quantity = level * 200, rarity = Rarity.Common }
                        }
                    },
                    isMilestone = level % 10 == 0,
                    milestoneTitle = level % 10 == 0 ? $"里程碑 Tier {level / 10}" : ""
                };
            }

            return table;
        }

        /// <summary>
        /// 生成测试主题配置
        /// </summary>
        private ThemeProfile[] GenerateTestThemeProfiles()
        {
            var neon = CreateThemeProfile("neon_cyber", "霓虹赛博", "赛博朋克霓虹之夜",
                fogColor: new Color(0.05f, 0.02f, 0.12f));
            var voidDark = CreateThemeProfile("void_darkness", "虚空暗影", "虚空深处的回响",
                fogColor: new Color(0.02f, 0.02f, 0.05f));

            return new ThemeProfile[] { neon, voidDark };
        }

        private ThemeProfile CreateThemeProfile(string id, string name, string desc, Color fogColor)
        {
            var theme = ScriptableObject.CreateInstance<ThemeProfile>();
            theme.themeId = id;
            theme.displayName = name;
            theme.description = desc;
            theme.themeColor = fogColor;
            theme.fogDensity = 0.012f;

            theme.chunkWeightCurve = AnimationCurve.Constant(0f, 1f, 1f);
            theme.obstacleDensityCurve = AnimationCurve.Linear(0f, 0.15f, 1f, 0.9f);
            theme.rewardDensityCurve = AnimationCurve.Constant(0f, 1f, 0.3f);
            theme.speedMultiplierCurve = AnimationCurve.Linear(0f, 1f, 1f, 1.8f);

            return theme;
        }

        // ===================== 模拟 API =====================

        /// <summary>
        /// 拉取赛季配置
        /// </summary>
        public async UniTask<SeasonConfig> GetSeasonConfigAsync(string seasonId)
        {
            await SimulateNetworkDelay();
            return _testSeasonConfig;
        }

        /// <summary>
        /// 拉取通行证奖励表
        /// </summary>
        public async UniTask<BattlePassTable> GetBattlePassTableAsync(string seasonId)
        {
            await SimulateNetworkDelay();
            return _testBattlePassTable;
        }

        /// <summary>
        /// 拉取主题配置
        /// </summary>
        public async UniTask<ThemeProfile> GetThemeProfileAsync(string themeId)
        {
            await SimulateNetworkDelay();
            return _testThemeProfiles != null && _testThemeProfiles.Length > 0
                ? _testThemeProfiles[0]
                : null;
        }

        /// <summary>
        /// 拉取全部主题配置
        /// </summary>
        public async UniTask<ThemeProfile[]> GetAllThemeProfilesAsync()
        {
            await SimulateNetworkDelay();
            return _testThemeProfiles ?? Array.Empty<ThemeProfile>();
        }

        /// <summary>
        /// 同步运行时数据（登录/切前台时调用）
        /// </summary>
        public async UniTask<SeasonRuntimeData> SyncRuntimeDataAsync()
        {
            await SimulateNetworkDelay();
            _mockRuntimeData = _mockRuntimeData ?? CreateMockRuntimeData();
            OnRuntimeDataRefreshed?.Invoke(_mockRuntimeData);
            return _mockRuntimeData;
        }

        /// <summary>
        /// 提交通行证经验
        /// </summary>
        public async UniTask<SeasonRuntimeData> SubmitBattlePassXPAsync(int xpAmount)
        {
            await SimulateNetworkDelay();

            if (_mockRuntimeData != null)
            {
                int cap = _testBattlePassTable != null ? _testBattlePassTable.weeklyXpCap : 20000;
                _mockRuntimeData.battlePassXp = Mathf.Min(_mockRuntimeData.battlePassXp + xpAmount, cap);

                // 依据 XP 曲线重算等级
                if (_testBattlePassTable?.tiers != null)
                {
                    for (int i = _testBattlePassTable.tiers.Length - 1; i >= 0; i--)
                    {
                        if (_mockRuntimeData.battlePassXp >= _testBattlePassTable.tiers[i].xpRequired)
                        {
                            _mockRuntimeData.battlePassLevel = _testBattlePassTable.tiers[i].level;
                            break;
                        }
                    }
                }
            }

            return _mockRuntimeData;
        }

        /// <summary>
        /// 购买高级通行证
        /// </summary>
        public async UniTask<SeasonRuntimeData> PurchasePremiumPassAsync(string receipt)
        {
            await SimulateNetworkDelay();

            if (_mockRuntimeData != null)
            {
                _mockRuntimeData.isPremiumUnlocked = true;
                OnRuntimeDataRefreshed?.Invoke(_mockRuntimeData);
            }

            return _mockRuntimeData;
        }

        /// <summary>
        /// 领取通行证奖励
        /// </summary>
        public async UniTask<RewardBundle> ClaimBattlePassRewardAsync(int tier, RewardTrack track)
        {
            await SimulateNetworkDelay();

            var table = _testBattlePassTable;
            if (table?.tiers != null)
            {
                var tierData = table.tiers.FirstOrDefault(t => t.level == tier);
                if (tierData != null)
                {
                    var list = track == RewardTrack.Free
                        ? _mockRuntimeData.claimedFreeTiers
                        : _mockRuntimeData.claimedPremiumTiers;
                    if (!list.Contains(tier)) list.Add(tier);

                    return track == RewardTrack.Free ? tierData.freeReward : tierData.premiumReward;
                }
            }

            return null;
        }

        /// <summary>
        /// 更新活动进度
        /// </summary>
        public async UniTask<EventProgress> UpdateEventProgressAsync(string eventId, int progressDelta,
            Dictionary<string, int> subProgress = null)
        {
            await SimulateNetworkDelay();

            if (_mockRuntimeData == null) return null;

            if (!_mockRuntimeData.eventProgress.TryGetValue(eventId, out var progress))
            {
                progress = new EventProgress { eventId = eventId, targetProgress = 100 };
                _mockRuntimeData.eventProgress[eventId] = progress;
            }

            progress.currentProgress = Mathf.Min(progress.currentProgress + progressDelta, progress.targetProgress);
            progress.isCompleted = progress.currentProgress >= progress.targetProgress;

            if (subProgress != null)
            {
                foreach (var kvp in subProgress)
                {
                    progress.subProgress[kvp.Key] = progress.subProgress.GetValueOrDefault(kvp.Key) + kvp.Value;
                }
            }

            return progress;
        }

        /// <summary>
        /// 领取活动奖励
        /// </summary>
        public async UniTask<RewardBundle> ClaimEventRewardAsync(string eventId)
        {
            await SimulateNetworkDelay();

            if (_mockRuntimeData != null &&
                _mockRuntimeData.eventProgress.TryGetValue(eventId, out var progress) &&
                progress.isCompleted)
            {
                progress.isClaimed = true;
                return new RewardBundle
                {
                    items = new[]
                    {
                        new RewardItem { type = RewardType.Currency, itemId = "gems", quantity = 200, rarity = Rarity.Rare }
                    }
                };
            }

            return null;
        }

        /// <summary>
        /// 拉取商店商品
        /// </summary>
        public async UniTask<ShopProduct[]> GetShopProductsAsync()
        {
            await SimulateNetworkDelay();

            return new ShopProduct[]
            {
                new ShopProduct
                {
                    productId = "gems_small",
                    title = "宝石 x60",
                    description = "购买 60 宝石",
                    price = "$0.99",
                    currency = "USD",
                    reward = new RewardBundle
                    {
                        items = new[] { new RewardItem { type = RewardType.Currency, itemId = "gems", quantity = 60, rarity = Rarity.Common } }
                    }
                },
                new ShopProduct
                {
                    productId = "gems_large",
                    title = "宝石 x500",
                    description = "购买 500 宝石（含首购加赠）",
                    price = "$4.99",
                    currency = "USD",
                    reward = new RewardBundle
                    {
                        items = new[] { new RewardItem { type = RewardType.Currency, itemId = "gems", quantity = 500, rarity = Rarity.Epic } }
                    }
                },
                new ShopProduct
                {
                    productId = "starter_pack",
                    title = "新手礼包",
                    description = "皮肤 + 金币 + 宝石组合包",
                    price = "$2.99",
                    currency = "USD",
                    reward = new RewardBundle
                    {
                        items = new[]
                        {
                            new RewardItem { type = RewardType.Skin, itemId = "skin_neon_runner", quantity = 1, rarity = Rarity.Rare },
                            new RewardItem { type = RewardType.Currency, itemId = "coins", quantity = 5000, rarity = Rarity.Common },
                            new RewardItem { type = RewardType.Currency, itemId = "gems", quantity = 120, rarity = Rarity.Rare }
                        }
                    }
                }
            };
        }

        /// <summary>
        /// 购买商品
        /// </summary>
        public async UniTask<PurchaseResult> PurchaseProductAsync(string productId, string receipt)
        {
            await SimulateNetworkDelay();

            return new PurchaseResult
            {
                success = true,
                productId = productId,
                transactionId = Guid.NewGuid().ToString("N").Substring(0, 16),
                message = "[MockServer] Purchase simulated"
            };
        }

        // ===================== 调试工具 =====================

        public void SetTimeOffset(float minutes)
        {
            _timeOffsetMinutes = minutes;
            OnServerTimeSynced?.Invoke(ServerTimeUtc);
        }

        public void ResetMockData()
        {
            _mockRuntimeData = CreateMockRuntimeData();
            OnRuntimeDataRefreshed?.Invoke(_mockRuntimeData);
            Debug.Log("[MockServer] Mock data reset");
        }
    }

    /// <summary>
    /// 商店商品
    /// </summary>
    [Serializable]
    public class ShopProduct
    {
        public string productId;
        public string title;
        public string description;
        public string price;
        public string currency;
        public RewardBundle reward;
    }

    /// <summary>
    /// 购买结果
    /// </summary>
    [Serializable]
    public class PurchaseResult
    {
        public bool success;
        public string productId;
        public string transactionId;
        public string message;
    }
}
