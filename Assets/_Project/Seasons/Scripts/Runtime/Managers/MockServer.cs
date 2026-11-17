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

            // 生成测试用赛季配置（如果未指定）
            if (_testSeasonConfig == null)
            {
                _testSeasonConfig = GenerateTestSeasonConfig();
            }

            if (_testBattlePassTable == null)
            {
                _testBattlePassTable = GenerateTestBattlePassTable();
            }

            if (_testThemeProfiles == null || _testThemeProfiles.Length == 0)
            {
                _testThemeProfiles = GenerateTestThemeProfiles();
            }

            _isInitialized = true;

            OnServerTimeSynced?.Invoke(ServerTimeUtc);
            OnRuntimeDataRefreshed?.Invoke(_mockRuntimeData);

            Debug.Log($"[MockServer] Initialized. Server time: {ServerTimeUtc:yyyy-MM-dd HH:mm:ss} UTC");
        }

        /// <summary>
        /// 模拟网络延迟
        /// </summary>
        private async UniTask SimulateNetworkDelay()
        {
            if (_networkLatencyMs > 0)
            {
                await UniTask.Delay(TimeSpan.FromMilliseconds(_networkLatencyMs));
            }
        }

        /// <summary>
        /// 创建模拟运行时数据
        /// </summary>
        private SeasonRuntimeData CreateMockRuntimeData()
        {
            var data = new SeasonRuntimeData
            {
                seasonId = "season_001",
                seasonNumber = 1,
                currentPhaseIndex = 0,
                phaseProgress = 0.3f,
                seasonStartTimestamp = ((DateTimeOffset)DateTime.UtcNow.AddDays(-10)).ToUnixTimeSeconds(),
                lastSyncTimestamp = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds(),
                currentThemeId = "neon_cyber",
                battlePass = new BattlePassRuntimeData
                {
                    currentTier = 15,
                    currentXP = 3750,
                    isPremiumUnlocked = true,
                    premiumPurchaseTimestamp = ((DateTimeOffset)DateTime.UtcNow.AddDays(-5)).ToUnixTimeSeconds(),
                    claimedFreeTiers = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 },
                    claimedPremiumTiers = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 },
                    weeklyXPEarned = 12500,
                    weeklyCap = 20000,
                    lastWeeklyReset = ((DateTimeOffset)DateTime.UtcNow.AddDays(-2)).ToUnixTimeSeconds()
                },
                eventProgress = new Dictionary<string, EventProgress>
                {
                    ["token_collect_01"] = new EventProgress
                    {
                        eventId = "token_collect_01",
                        currentProgress = 67,
                        targetProgress = 100,
                        isCompleted = false,
                        isClaimed = false,
                        startedTimestamp = ((DateTimeOffset)DateTime.UtcNow.AddDays(-3)).ToUnixTimeSeconds(),
                        subProgress = new Dictionary<string, int> { ["neon_token"] = 67 }
                    },
                    ["boss_rush_01"] = new EventProgress
                    {
                        eventId = "boss_rush_01",
                        currentProgress = 3,
                        targetProgress = 5,
                        isCompleted = false,
                        isClaimed = false,
                        startedTimestamp = ((DateTimeOffset)DateTime.UtcNow.AddDays(-1)).ToUnixTimeSeconds(),
                        subProgress = new Dictionary<string, int> { ["boss_cyber"] = 1, ["boss_void"] = 1, ["boss_neon"] = 1 }
                    },
                    ["daily_challenge_01"] = new EventProgress
                    {
                        eventId = "daily_challenge_01",
                        currentProgress = 2,
                        targetProgress = 3,
                        isCompleted = false,
                        isClaimed = false,
                        startedTimestamp = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds(),
                        subProgress = new Dictionary<string, int> { ["distance_1000"] = 1, ["coins_500"] = 1 }
                    }
                },
                unlockedContent = new HashSet<string>
                {
                    "skin_default", "skin_neon_runner", "mount_hoverboard",
                    "emote_dance", "emote_wave", "trail_neon",
                    "frame_season_1", "title_season_runner"
                },
                playerStats = new PlayerSeasonStats
                {
                    totalDistance = 125000f,
                    totalRuns = 847,
                    bestDistance = 15600f,
                    totalCoinsCollected = 1250000,
                    totalGemsCollected = 12500,
                    enemiesDefeated = 3420,
                    powerupsUsed = 890,
                    perfectRuns = 23,
                    totalPlayTimeSeconds = 125000,
                    favoriteTheme = "neon_cyber",
                    seasonJoinTimestamp = ((DateTimeOffset)DateTime.UtcNow.AddDays(-10)).ToUnixTimeSeconds()
                }
            };

            return data;
        }

        /// <summary>
        /// 生成测试用赛季配置
        /// </summary>
        private SeasonConfig GenerateTestSeasonConfig()
        {
            var config = ScriptableObject.CreateInstance<SeasonConfig>();
            config.seasonId = "season_001";
            config.seasonNumber = 1;
            config.displayName = "霓虹觉醒";
            config.description = "赛博朋克世界的霓虹之夜，等待你的探索";
            config.seasonStartTimestamp = ((DateTimeOffset)DateTime.UtcNow.AddDays(-10)).ToUnixTimeSeconds();
            config.seasonDurationDays = 60;
            config.phaseCount = 4;

            // 阶段配置
            config.phases = new SeasonPhase[4];
            config.phases[0] = new SeasonPhase
            {
                phaseIndex = 0,
                name = "霓虹初现",
                description = "初入赛博城市，感受霓虹脉动",
                startOffsetDays = 0,
                durationDays = 14,
                themeProfileId = "neon_cyber",
                difficultyMultiplier = 0.8f,
                xpMultiplier = 1.0f,
                unlockContent = new string[] { "skin_neon_runner", "trail_neon_blue" }
            };
            config.phases[1] = new SeasonPhase
            {
                phaseIndex = 1,
                name = "数据洪流",
                description = "深入数据核心，对抗流动的代码",
                startOffsetDays = 14,
                durationDays = 16,
                themeProfileId = "cyberpunk",
                difficultyMultiplier = 1.0f,
                xpMultiplier = 1.2f,
                unlockContent = new string[] { "skin_data_diver", "mount_data_stream" }
            };
            config.phases[2] = new SeasonPhase
            {
                phaseIndex = 2,
                name = "虚空回响",
                description = "虚空中的回响，考验真正的跑者",
                startOffsetDays = 30,
                durationDays = 15,
                themeProfileId = "void_darkness",
                difficultyMultiplier = 1.3f,
                xpMultiplier = 1.5f,
                unlockContent = new string[] { "skin_void_walker", "emote_void_touch" }
            };
            config.phases[3] = new SeasonPhase
            {
                phaseIndex = 3,
                name = "终极绽放",
                description = "赛季终章，所有光芒汇聚于此",
                startOffsetDays = 45,
                durationDays = 15,
                themeProfileId = "ultimate_rainbow",
                difficultyMultiplier = 1.5f,
                xpMultiplier = 2.0f,
                unlockContent = new string[] { "skin_ultimate_tom", "mount_phoenix", "title_season_champion" }
            };

            // 活动时间表
            config.eventSchedules = new EventSchedule[]
            {
                new EventSchedule
                {
                    eventId = "token_collect_01",
                    type = EventType.TokenCollect,
                    phaseIndex = 0,
                    startOffsetDays = 2,
                    durationDays = 12,
                    parameters = new SerializableDictionary<string, float>
                    {
                        ["spawn_interval"] = 5f,
                        ["token_value"] = 1f,
                        ["bonus_multiplier"] = 1.5f
                    }
                },
                new EventSchedule
                {
                    eventId = "boss_rush_01",
                    type = EventType.BossRush,
                    phaseIndex = 1,
                    startOffsetDays = 3,
                    durationDays = 10,
                    parameters = new SerializableDictionary<string, float>
                    {
                        ["required_bosses"] = 5f,
                        ["boss_health_multiplier"] = 1.5f,
                        ["time_limit"] = 300f
                    }
                },
                new EventSchedule
                {
                    eventId = "time_trial_01",
                    type = EventType.TimeTrial,
                    phaseIndex = 2,
                    startOffsetDays = 5,
                    durationDays = 7,
                    parameters = new SerializableDictionary<string, float>
                    {
                        ["target_time"] = 60f,
                        ["checkpoint_count"] = 5f
                    }
                },
                new EventSchedule
                {
                    eventId = "daily_challenge_01",
                    type = EventType.DailyChallenge,
                    phaseIndex = 0,
                    startOffsetDays = 0,
                    durationDays = 60,
                    parameters = new SerializableDictionary<string, float>
                    {
                        ["required_challenges"] = 3f,
                        ["refresh_hour"] = 4f
                    }
                }
            };

            return config;
        }

        /// <summary>
        /// 生成测试用通行证表
        /// </summary>
        private BattlePassTable GenerateTestBattlePassTable()
        {
            var table = ScriptableObject.CreateInstance<BattlePassTable>();
            table.tableId = "bp_season_001";
            table.seasonId = "season_001";
            table.maxTier = 50;
            table.baseXPPerTier = 250;
            table.xpGrowthFactor = 1.08f;
            table.weeklyXPCap = 20000;
            table.premiumPriceUSD = 9.99f;
            table.premiumPriceCNY = 68f;

            table.tiers = new BattlePassTier[50];

            var freeRewards = new RewardItem[]
            {
                new RewardItem { type = RewardType.Currency, itemId = "coins", quantity = 500, rarity = Rarity.Common },
                new RewardItem { type = RewardType.Currency, itemId = "gems", quantity = 50, rarity = Rarity.Uncommon },
                new RewardItem { type = RewardType.Experience, itemId = "bp_xp", quantity = 500, rarity = Rarity.Common },
                new RewardItem { type = RewardType.LootBox, itemId = "box_common", quantity = 1, rarity = Rarity.Common },
            };

            var premiumRewards = new RewardItem[]
            {
                new RewardItem { type = RewardType.Skin, itemId = "skin_premium_01", quantity = 1, rarity = Rarity.Rare },
                new RewardItem { type = RewardType.Mount, itemId = "mount_premium_01", quantity = 1, rarity = Rarity.Epic },
                new RewardItem { type = RewardType.Currency, itemId = "gems", quantity = 200, rarity = Rarity.Rare },
                new RewardItem { type = RewardType.ProfileFrame, itemId = "frame_premium_01", quantity = 1, rarity = Rarity.Epic },
                new RewardItem { type = RewardType.Emote, itemId = "emote_premium_01", quantity = 1, rarity = Rarity.Rare },
            };

            for (int i = 0; i < 50; i++)
            {
                int tier = i + 1;
                float xpRequired = CalculateTierXP(tier, table.baseXPPerTier, table.xpGrowthFactor);

                var freeReward = freeRewards[UnityEngine.Random.Range(0, freeRewards.Length)];
                freeReward = new RewardItem
                {
                    type = freeReward.type,
                    itemId = freeReward.itemId,
                    quantity = freeReward.quantity + tier / 10,
                    rarity = freeReward.rarity
                };

                RewardItem premiumReward = null;
                if (tier % 5 == 0) // 每5级给大奖
                {
                    premiumReward = premiumRewards[(tier / 5 - 1) % premiumRewards.Length];
                    premiumReward = new RewardItem
                    {
                        type = premiumReward.type,
                        itemId = premiumReward.itemId + "_" + tier,
                        quantity = 1,
                        rarity = premiumReward.rarity
                    };
                }
                else
                {
                    premiumReward = new RewardItem
                    {
                        type = RewardType.Currency,
                        itemId = "gems",
                        quantity = 20 + tier * 2,
                        rarity = Rarity.Uncommon
                    };
                }

                table.tiers[i] = new BattlePassTier
                {
                    tier = tier,
                    xpRequired = Mathf.RoundToInt(xpRequired),
                    freeReward = freeReward,
                    premiumReward = premiumReward,
                    isMilestone = tier % 10 == 0,
                    milestoneTitle = tier % 10 == 0 ? $"里程碑 {tier / 10}" : "",
                    unlockDescription = tier % 5 == 0 ? "解锁专属外观" : ""
                };
            }

            return table;
        }

        private int CalculateTierXP(int tier, int baseXP, float growthFactor)
        {
            float xp = baseXP;
            for (int i = 1; i < tier; i++)
            {
                xp *= growthFactor;
            }
            return Mathf.RoundToInt(xp);
        }

        /// <summary>
        /// 生成测试用主题配置
        /// </summary>
        private ThemeProfile[] GenerateTestThemeProfiles()
        {
            var profiles = new ThemeProfile[4];

            // 霓虹赛博
            profiles[0] = CreateThemeProfile(
                "neon_cyber", "霓虹赛博", ThemeType.NeonCyber,
                new Color(0.0f, 0.9f, 1.0f), new Color(1.0f, 0.0f, 0.6f),
                new Color(0.02f, 0.02f, 0.08f), new Color(0.08f, 0.08f, 0.15f)
            );

            // 赛博朋克
            profiles[1] = CreateThemeProfile(
                "cyberpunk", "赛博朋克", ThemeType.Cyberpunk,
                new Color(1.0f, 0.0f, 0.8f), new Color(0.0f, 1.0f, 1.0f),
                new Color(0.08f, 0.0f, 0.12f), new Color(0.15f, 0.05f, 0.2f)
            );

            // 虚空黑暗
            profiles[2] = CreateThemeProfile(
                "void_darkness", "虚空黑暗", ThemeType.VoidDarkness,
                new Color(0.4f, 0.0f, 0.8f), new Color(0.0f, 0.8f, 0.8f),
                new Color(0.03f, 0.0f, 0.05f), new Color(0.08f, 0.02f, 0.12f)
            );

            // 终极彩虹
            profiles[3] = CreateThemeProfile(
                "ultimate_rainbow", "终极彩虹", ThemeType.UltimateRainbow,
                new Color(1.0f, 0.5f, 0.0f), new Color(0.0f, 1.0f, 0.5f),
                new Color(0.1f, 0.05f, 0.15f), new Color(0.2f, 0.1f, 0.25f)
            );

            return profiles;
        }

        private ThemeProfile CreateThemeProfile(string id, string name, ThemeType type,
            Color primary, Color secondary, Color bg, Color surface)
        {
            var profile = ScriptableObject.CreateInstance<ThemeProfile>();
            profile.themeId = id;
            profile.displayName = name;
            profile.themeType = type;
            profile.primaryColor = primary;
            profile.secondaryColor = secondary;
            profile.backgroundColor = bg;
            profile.surfaceColor = surface;
            profile.accentColor = Color.Lerp(primary, secondary, 0.5f);
            profile.glowColor = new Color(primary.r, primary.g, primary.b, 0.8f);
            profile.particleColor = new Color(secondary.r, secondary.g, secondary.b, 0.9f);
            profile.uiShaderVariant = ShaderVariant.NeonGlow;
            profile.skyboxMaterial = null; // 运行时赋值
            profile.musicTrack = null;
            profile.ambientSound = null;

            // Chunk 权重
            profile.chunkWeights = new ChunkWeight[]
            {
                new ChunkWeight { chunkId = "straight", weight = 30 },
                new ChunkWeight { chunkId = "jump", weight = 20 },
                new ChunkWeight { chunkId = "slide", weight = 15 },
                new ChunkWeight { chunkId = "attack", weight = 10 },
                new ChunkWeight { chunkId = "coin_line", weight = 15 },
                new ChunkWeight { chunkId = "powerup", weight = 5 },
                new ChunkWeight { chunkId = "hazard_spikes", weight = 5 },
            };

            // 难度曲线
            profile.difficultyCurve = new DifficultyCurve
            {
                curveId = id + "_difficulty",
                baseSpeed = 15f,
                maxSpeed = 35f,
                speedGrowthRate = 0.002f,
                obstacleDensityBase = 0.3f,
                obstacleDensityMax = 0.8f,
                densityGrowthRate = 0.001f,
                jumpFrequencyBase = 0.2f,
                jumpFrequencyMax = 0.5f,
                slideFrequencyBase = 0.15f,
                slideFrequencyMax = 0.4f,
                attackFrequencyBase = 0.1f,
                attackFrequencyMax = 0.3f,
                powerupFrequency = 0.05f,
                coinDensity = 0.4f,
                seedOffset = UnityEngine.Random.Range(0, 10000)
            };

            return profile;
        }

        // ===================== 公共 API =====================

        /// <summary>
        /// 获取赛季配置
        /// </summary>
        public async UniTask<SeasonConfig> GetSeasonConfigAsync(string seasonId)
        {
            await SimulateNetworkDelay();
            return _testSeasonConfig;
        }

        /// <summary>
        /// 获取通行证表
        /// </summary>
        public async UniTask<BattlePassTable> GetBattlePassTableAsync(string seasonId)
        {
            await SimulateNetworkDelay();
            return _testBattlePassTable;
        }

        /// <summary>
        /// 获取主题配置
        /// </summary>
        public async UniTask<ThemeProfile> GetThemeProfileAsync(string themeId)
        {
            await SimulateNetworkDelay();
            return _testThemeProfiles.FirstOrDefault(p => p.themeId == themeId);
        }

        /// <summary>
        /// 获取所有主题配置
        /// </summary>
        public async UniTask<ThemeProfile[]> GetAllThemeProfilesAsync()
        {
            await SimulateNetworkDelay();
            return _testThemeProfiles;
        }

        /// <summary>
        /// 同步运行时数据
        /// </summary>
        public async UniTask<SeasonRuntimeData> SyncRuntimeDataAsync()
        {
            await SimulateNetworkDelay();
            _mockRuntimeData.lastSyncTimestamp = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();
            OnRuntimeDataRefreshed?.Invoke(_mockRuntimeData);
            return _mockRuntimeData;
        }

        /// <summary>
        /// 提交战斗通行证经验
        /// </summary>
        public async UniTask<BattlePassRuntimeData> SubmitBattlePassXPAsync(int xpAmount, Dictionary<string, int> xpSources)
        {
            await SimulateNetworkDelay();

            var bp = _mockRuntimeData.battlePass;
            bp.currentXP += xpAmount;

            // 检查升级
            if (_testBattlePassTable != null)
            {
                while (bp.currentTier < _testBattlePassTable.maxTier)
                {
                    int nextTier = bp.currentTier + 1;
                    int requiredXP = _testBattlePassTable.tiers[nextTier - 1].xpRequired;
                    if (bp.currentXP >= requiredXP)
                    {
                        bp.currentTier = nextTier;
                    }
                    else break;
                }
            }

            // 更新周上限
            bp.weeklyXPEarned += xpAmount;

            OnRuntimeDataRefreshed?.Invoke(_mockRuntimeData);
            return bp;
        }

        /// <summary>
        /// 购买高级通行证
        /// </summary>
        public async UniTask<BattlePassRuntimeData> PurchasePremiumPassAsync(string receipt)
        {
            await SimulateNetworkDelay();

            var bp = _mockRuntimeData.battlePass;
            if (!bp.isPremiumUnlocked)
            {
                bp.isPremiumUnlocked = true;
                bp.premiumPurchaseTimestamp = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();

                // 补发已解锁等级的高级奖励
                for (int i = 1; i <= bp.currentTier; i++)
                {
                    if (!bp.claimedPremiumTiers.Contains(i))
                    {
                        bp.claimedPremiumTiers.Add(i);
                    }
                }

                OnRuntimeDataRefreshed?.Invoke(_mockRuntimeData);
            }

            return bp;
        }

        /// <summary>
        /// 领取通行证奖励
        /// </summary>
        public async UniTask<RewardBundle> ClaimBattlePassRewardAsync(int tier, bool isPremium)
        {
            await SimulateNetworkDelay();

            var bp = _mockRuntimeData.battlePass;
            if (isPremium)
            {
                if (!bp.claimedPremiumTiers.Contains(tier))
                {
                    bp.claimedPremiumTiers.Add(tier);
                }
            }
            else
            {
                if (!bp.claimedFreeTiers.Contains(tier))
                {
                    bp.claimedFreeTiers.Add(tier);
                }
            }

            var reward = _testBattlePassTable.tiers[tier - 1];
            var bundle = new RewardBundle
            {
                items = isPremium ? new[] { reward.premiumReward } : new[] { reward.freeReward }
            };

            OnRuntimeDataRefreshed?.Invoke(_mockRuntimeData);
            return bundle;
        }

        /// <summary>
        /// 更新活动进度
        /// </summary>
        public async UniTask<EventProgress> UpdateEventProgressAsync(string eventId, int progressDelta, Dictionary<string, int> subProgress)
        {
            await SimulateNetworkDelay();

            if (_mockRuntimeData.eventProgress.TryGetValue(eventId, out var progress))
            {
                progress.currentProgress = Mathf.Min(progress.currentProgress + progressDelta, progress.targetProgress);
                progress.isCompleted = progress.currentProgress >= progress.targetProgress;

                if (subProgress != null)
                {
                    foreach (var kvp in subProgress)
                    {
                        progress.subProgress[kvp.Key] = progress.subProgress.GetValueOrDefault(kvp.Key) + kvp.Value;
                    }
                }
            }

            OnRuntimeDataRefreshed?.Invoke(_mockRuntimeData);
            return _mockRuntimeData.eventProgress[eventId];
        }

        /// <summary>
        /// 领取活动奖励
        /// </summary>
        public async UniTask<RewardBundle> ClaimEventRewardAsync(string eventId)
        {
            await SimulateNetworkDelay();

            if (_mockRuntimeData.eventProgress.TryGetValue(eventId, out var progress))
            {
                progress.isClaimed = true;
                OnRuntimeDataRefreshed?.Invoke(_mockRuntimeData);

                return new RewardBundle
                {
                    items = new RewardItem[]
                    {
                        new RewardItem { type = RewardType.Currency, itemId = "gems", quantity = 200, rarity = Rarity.Rare },
                        new RewardItem { type = RewardType.LootBox, itemId = "box_event", quantity = 1, rarity = Rarity.Epic }
                    }
                };
            }

            return null;
        }

        /// <summary>
        /// 获取商店商品
        /// </summary>
        public async UniTask<ShopProduct[]> GetShopProductsAsync()
        {
            await SimulateNetworkDelay();

            return new ShopProduct[]
            {
                new ShopProduct
                {
                    productId = "bp_premium_season_001",
                    name = "赛季高级通行证",
                    description = "解锁 50 级专属奖励，包含传说皮肤、坐骑、头衔",
                    priceUSD = 9.99f,
                    priceCNY = 68f,
                    currencyType = "real_money",
                    rewardBundle = new RewardBundle
                    {
                        items = new RewardItem[]
                        {
                            new RewardItem { type = RewardType.BattlePassPremium, itemId = "season_001", quantity = 1, rarity = Rarity.Legendary }
                        }
                    },
                    tags = new[] { "battlepass", "season_001" },
                    isLimited = true,
                    limitCount = 1
                },
                new ShopProduct
                {
                    productId = "gems_500",
                    name = "500 宝石",
                    description = "可用于购买皮肤、坐骑、加速道具",
                    priceUSD = 4.99f,
                    priceCNY = 30f,
                    currencyType = "real_money",
                    rewardBundle = new RewardBundle
                    {
                        items = new RewardItem[]
                        {
                            new RewardItem { type = RewardType.Currency, itemId = "gems", quantity = 500, rarity = Rarity.Rare }
                        }
                    },
                    tags = new[] { "currency", "gems" }
                },
                new ShopProduct
                {
                    productId = "gems_1200",
                    name = "1200 宝石 (赠送 200)",
                    description = "热门推荐，性价比最高",
                    priceUSD = 9.99f,
                    priceCNY = 68f,
                    currencyType = "real_money",
                    rewardBundle = new RewardBundle
                    {
                        items = new RewardItem[]
                        {
                            new RewardItem { type = RewardType.Currency, itemId = "gems", quantity = 1400, rarity = Rarity.Epic }
                        }
                    },
                    tags = new[] { "currency", "gems", "bonus" },
                    isLimited = false
                },
                new ShopProduct
                {
                    productId = "skin_neon_bundle",
                    name = "霓虹套装包",
                    description = "包含霓虹跑者皮肤、霓虹滑板、霓虹尾迹、专属表情",
                    priceUSD = 14.99f,
                    priceCNY = 98f,
                    currencyType = "real_money",
                    rewardBundle = new RewardBundle
                    {
                        items = new RewardItem[]
                        {
                            new RewardItem { type = RewardType.Skin, itemId = "skin_neon_runner", quantity = 1, rarity = Rarity.Epic },
                            new RewardItem { type = RewardType.Mount, itemId = "mount_neon_board", quantity = 1, rarity = Rarity.Epic },
                            new RewardItem { type = RewardType.Trail, itemId = "trail_neon", quantity = 1, rarity = Rarity.Rare },
                            new RewardItem { type = RewardType.Emote, itemId = "emote_neon_dance", quantity = 1, rarity = Rarity.Rare }
                        }
                    },
                    tags = new[] { "bundle", "skin", "neon_cyber" },
                    isLimited = true,
                    limitCount = 1
                }
            };
        }

        /// <summary>
        /// 购买商品
        /// </summary>
        public async UniTask<PurchaseResult> PurchaseProductAsync(string productId, string receipt)
        {
            await SimulateNetworkDelay();

            var products = await GetShopProductsAsync();
            var product = products.FirstOrDefault(p => p.productId == productId);

            if (product == null)
            {
                return new PurchaseResult { success = false, errorMessage = "Product not found" };
            }

            // 模拟验证收据
            bool validReceipt = !string.IsNullOrEmpty(receipt);

            if (!validReceipt)
            {
                return new PurchaseResult { success = false, errorMessage = "Invalid receipt" };
            }

            // 发放奖励
            foreach (var item in product.rewardBundle.items)
            {
                // await GrantItemAsync(item);
            }

            // 解锁内容
            if (product.tags.Contains("battlepass"))
            {
                await PurchasePremiumPassAsync(receipt);
            }

            return new PurchaseResult
            {
                success = true,
                productId = productId,
                grantedItems = product.rewardBundle.items,
                transactionId = Guid.NewGuid().ToString("N")[..16],
                timestamp = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds()
            };
        }

        /// <summary>
        /// 设置时间偏移（测试用）
        /// </summary>
        public void SetTimeOffset(float minutes)
        {
            _timeOffsetMinutes = minutes;
            OnServerTimeSynced?.Invoke(ServerTimeUtc);
        }

        /// <summary>
        /// 重置模拟数据（测试用）
        /// </summary>
        public void ResetMockData()
        {
            _mockRuntimeData = CreateMockRuntimeData();
            OnRuntimeDataRefreshed?.Invoke(_mockRuntimeData);
            Debug.Log("[MockServer] Mock data reset");
        }

        protected override void OnDispose()
        {
            _testSeasonConfig = null;
            _testBattlePassTable = null;
            _testThemeProfiles = null;
            _mockRuntimeData = null;
            base.OnDispose();
        }
    }

    // ===================== 辅助数据结构 =====================

    [Serializable]
    public class ShopProduct
    {
        public string productId;
        public string name;
        public string description;
        public float priceUSD;
        public float priceCNY;
        public string currencyType; // "real_money", "gems", "coins"
        public RewardBundle rewardBundle;
        public string[] tags;
        public bool isLimited;
        public int limitCount;
    }

    [Serializable]
    public class PurchaseResult
    {
        public bool success;
        public string productId;
        public RewardItem[] grantedItems;
        public string transactionId;
        public long timestamp;
        public string errorMessage;
    }
}