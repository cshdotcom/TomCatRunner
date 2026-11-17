using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace TomCatRunner.Seasons
{
    // ===================== 枚举 =====================

    public enum SeasonPhase
    {
        Preheat,      // 预热期
        Active,       // 进行中
        Settling,     // 结算期
        Intermission  // 间歇期
    }

    public enum ChunkType
    {
        Straight,     // 直道
        Jump,         // 跳跃段
        Slide,        // 滑铲段
        Attack,       // 攻击段
        Coin,         // 金币段
        Powerup,      // 道具段
        Hazard,       // 危险段
        Mixed,        // 混合段
        Boss          // Boss段
    }

    public enum DifficultyTier
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
        Expert = 3
    }

    public enum RewardTrack
    {
        Free,      // 免费轨道
        Premium    // 付费轨道
    }

    public enum EventType
    {
        TokenCollect,   // 代币收集
        BossRush,       // Boss连战
        Puzzle,         // 解谜
        TimeTrial,      // 计时赛
        DailyChallenge  // 每日挑战
    }

    // ===================== 核心数据结构 =====================

    /// <summary>
    /// Chunk 定义 - 程序化关卡的基本构建块
    /// </summary>
    [Serializable]
    public class ChunkDefinition
    {
        public string id;
        public ChunkType type;
        public AssetReferenceGameObject prefab;  // Addressable 引用
        public float length;                     // 长度(米)
        public int weight = 100;                 // 权重
        public int minSegmentIndex = 0;          // 最小段落索引
        public int maxSegmentIndex = 999;        // 最大段落索引
        public DifficultyTier minDifficulty = DifficultyTier.Easy;
        public DifficultyTier maxDifficulty = DifficultyTier.Expert;
        public List<string> requiredTags = new List<string>();  // 必需标签
        public List<string> forbiddenTags = new List<string>(); // 禁用标签
        public ChunkVariation[] variations;      // 变体
    }

    [Serializable]
    public class ChunkVariation
    {
        public string name;
        public float weight = 1f;
        public AssetReferenceGameObject prefabOverride;
        public SerializableDictionary<string, float> parameterOverrides;
    }

    /// <summary>
    /// Segment 定义 - 由多个 Chunk 组成的关卡段落
    /// </summary>
    [Serializable]
    public class SegmentDefinition
    {
        public string id;
        public int index;                        // 段落索引
        public float length;                     // 总长度
        public List<ChunkInstance> chunks = new List<ChunkInstance>();
        public Vector3 startPosition;            // 起点位置
        public Vector3 endPosition;              // 终点位置
        public CheckpointData checkpoint;        // 检查点数据
    }

    [Serializable]
    public class ChunkInstance
    {
        public string chunkId;
        public int variationIndex = 0;
        public Vector3 localPosition;
        public Quaternion localRotation = Quaternion.identity;
        public SerializableDictionary<string, float> runtimeParameters;
    }

    [Serializable]
    public class CheckpointData
    {
        public float distanceFromStart;
        public Vector3 position;
        public Quaternion rotation;
        public int coinsCollected;
        public float timeElapsed;
    }

    /// <summary>
    /// 主题配置文件 - 定义一个主题的所有特征
    /// </summary>
    [CreateAssetMenu(fileName = "ThemeProfile", menuName = "TomCatRunner/Seasons/Theme Profile")]
    public class ThemeProfile : ScriptableObject
    {
        [Header("基本信息")]
        public string themeId;
        public string displayName;
        public string description;
        public int version = 1;
        public Sprite icon;
        public Color themeColor = Color.white;

        [Header("视觉风格坐标")]
        [Range(0f, 1f)] public float realismFactor = 0.2f;      // 写实 <-> 卡通
        [Range(0f, 1f)] public float intensityFactor = 0.5f;    // 轻松 <-> 紧张
        [Range(0f, 1f)] public float fantasyFactor = 0.7f;      // 现实 <-> 奇幻

        [Header("环境配置")]
        public ChunkDefinition[] chunkDefinitions;
        public AssetReferenceGameObject[] environmentPrefabs;   // 天空盒、地面、远景等
        public AssetReferenceGameObject skyboxPrefab;
        public Gradient fogGradient;
        public float fogDensity = 0.01f;

        [Header("程序化生成参数")]
        public AnimationCurve chunkWeightCurve;                 // 距离 -> Chunk权重调整
        public AnimationCurve obstacleDensityCurve;             // 距离 -> 障碍密度
        public AnimationCurve rewardDensityCurve;               // 距离 -> 奖励密度
        public AnimationCurve speedMultiplierCurve;             // 距离 -> 速度倍率
        public float baseSegmentLength = 300f;                  // 基础段落长度
        public int segmentsPerLevel = 10;                       // 每关段落数

        [Header("特色机制")]
        public ThemeMechanic[] specialMechanics;                // 特殊机制

        [Header("音频")]
        public AssetReference[] bgmTracks;                      // BGM 分层
        public AssetReference[] sfxPack;                        // SFX 包

        [Header("视觉特效")]
        public AssetReferenceGameObject vfxPrefab;              // 粒子特效预制体
        public AssetReference postProcessProfile;               // 后处理配置

        [Header("色板")]
        public ThemeColorPalette colorPalette;                  // 主题色板

        /// <summary>
        /// 获取指定难度下的 Chunk 权重表
        /// </summary>
        public Dictionary<ChunkType, float> GetChunkWeights(DifficultyTier difficulty, int segmentIndex)
        {
            var weights = new Dictionary<ChunkType, float>();
            foreach (var chunk in chunkDefinitions)
            {
                if (chunk.minDifficulty <= difficulty && chunk.maxDifficulty >= difficulty &&
                    chunk.minSegmentIndex <= segmentIndex && chunk.maxSegmentIndex >= segmentIndex)
                {
                    float weight = chunk.weight * chunkWeightCurve.Evaluate(segmentIndex / (float)segmentsPerLevel);
                    if (weights.ContainsKey(chunk.type))
                        weights[chunk.type] += weight;
                    else
                        weights[chunk.type] = weight;
                }
            }
            return weights;
        }
    }

    [Serializable]
    public class ThemeMechanic
    {
        public string mechanicId;
        public string displayName;
        public MechanicType type;
        public float activationChance = 0.3f;
        public int minSegmentIndex = 3;
        public float duration = 10f;
        public SerializableDictionary<string, float> parameters;
    }

    public enum MechanicType
    {
        WeatherStorm,       // 风暴天气
        VisualDistortion,   // 视觉扭曲
        SpeedZone,          // 变速区
        GravityShift,       // 重力翻转
        LaneShift,          // 跑道偏移
        TimeDilation,       // 时间膨胀
        MirrorMode          // 镜像模式
    }

    /// <summary>
    /// 赛季配置 - 一个完整赛季的静态配置
    /// </summary>
    [CreateAssetMenu(fileName = "SeasonConfig", menuName = "TomCatRunner/Seasons/Season Config")]
    public class SeasonConfig : ScriptableObject
    {
        [Header("基本信息")]
        public int seasonId;
        public string seasonName;
        public string shortCode;                    // 如 "S01", "S02"
        public Sprite seasonBanner;
        public ThemeProfile mainTheme;              // 主题题
        public ThemeProfile[] phaseThemes;          // 各阶段子主题

        [Header("时间配置 (UTC)")]
        public long preheatStartTimestamp;          // 预热开始
        public long seasonStartTimestamp;           // 赛季开始
        public long seasonEndTimestamp;             // 赛季结束
        public long settlingEndTimestamp;           // 结算结束

        [Header("阶段配置")]
        public SeasonPhaseConfig[] phases;

        [Header("通行证")]
        public BattlePassTable battlePassTable;

        [Header("活动")]
        public EventSchedule[] eventSchedules;

        [Header("难度曲线")]
        public DifficultyCurve difficultyCurve;

        [Header("版本控制")]
        public int configVersion = 1;
        public string addressableCatalogVersion;    // Addressables Catalog 版本

        /// <summary>
        /// 根据当前时间获取赛季阶段
        /// </summary>
        public SeasonPhase GetCurrentPhase(DateTime utcNow)
        {
            long now = ((DateTimeOffset)utcNow).ToUnixTimeSeconds();

            if (now < preheatStartTimestamp) return SeasonPhase.Intermission;
            if (now < seasonStartTimestamp) return SeasonPhase.Preheat;
            if (now < seasonEndTimestamp) return SeasonPhase.Active;
            if (now < settlingEndTimestamp) return SeasonPhase.Settling;
            return SeasonPhase.Intermission;
        }

        /// <summary>
        /// 获取当前活跃的子主题
        /// </summary>
        public ThemeProfile GetActiveTheme(DateTime utcNow)
        {
            long now = ((DateTimeOffset)utcNow).ToUnixTimeSeconds();

            if (now < seasonStartTimestamp || now >= seasonEndTimestamp)
                return mainTheme;

            foreach (var phase in phases)
            {
                long phaseStart = seasonStartTimestamp + phase.startOffsetDays * 86400L;
                long phaseEnd = phaseStart + phase.durationDays * 86400L;

                if (now >= phaseStart && now < phaseEnd)
                {
                    if (phase.themeIndex >= 0 && phase.themeIndex < phaseThemes.Length)
                        return phaseThemes[phase.themeIndex];
                }
            }
            return mainTheme;
        }
    }

    [Serializable]
    public class SeasonPhaseConfig
    {
        public string phaseName;
        public int themeIndex;                      // phaseThemes 数组索引
        public int startOffsetDays;                 // 赛季开始后第几天
        public int durationDays;                    // 持续天数
        public string[] unlockableContent;          // 解锁内容ID列表
        public EventType[] featuredEvents;          // 重点活动
    }

    /// <summary>
    /// 通行证奖励表
    /// </summary>
    [CreateAssetMenu(fileName = "BattlePassTable", menuName = "TomCatRunner/Seasons/Battle Pass Table")]
    public class BattlePassTable : ScriptableObject
    {
        public int maxLevel = 100;
        public AnimationCurve xpCurve;              // 等级 -> 所需XP
        public int weeklyXpCap = 50000;             // 周XP上限
        public BattlePassTier[] tiers;
    }

    [Serializable]
    public class BattlePassTier
    {
        public int level;
        public int xpRequired;
        public RewardBundle freeReward;
        public RewardBundle premiumReward;
        public bool isMilestone;                    // 里程碑等级(有特殊UI)
        public string milestoneTitle;
    }

    [Serializable]
    public class RewardBundle
    {
        public RewardItem[] items;
    }

    [Serializable]
    public class RewardItem
    {
        public RewardType type;
        public string itemId;                       // 皮肤ID、货币类型等
        public int quantity = 1;
        public Rarity rarity = Rarity.Common;
    }

    public enum RewardType
    {
        Currency,       // 货币
        Skin,           // 皮肤
        Mount,          // 坐骑
        Pet,            // 宠物
        Emote,          // 表情动作
        Title,          // 称号
        ProfileFrame,   // 头像框
        LootBox,        // 宝箱
        Experience      // 经验
    }

    public enum Rarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
        Mythic
    }

    /// <summary>
    /// 活动时间表
    /// </summary>
    [Serializable]
    public class EventSchedule
    {
        public string eventId;
        public EventType type;
        public int phaseIndex;                      // 所属阶段
        public int startOffsetDays;                 // 阶段开始后第几天
        public int durationDays;                    // 持续天数
        public string[] rewardIds;                  // 奖励ID
        public SerializableDictionary<string, float> parameters;
    }

    /// <summary>
    /// 难度曲线配置
    /// </summary>
    [CreateAssetMenu(fileName = "DifficultyCurve", menuName = "TomCatRunner/Seasons/Difficulty Curve")]
    public class DifficultyCurve : ScriptableObject
    {
        public AnimationCurve laneChangeFrequency;   // 换道频率
        public AnimationCurve obstacleDensity;       // 障碍密度
        public AnimationCurve speedMultiplier;       // 速度倍率
        public AnimationCurve reactionTimeWindow;    // 反应时间窗口
        public AnimationCurve powerupFrequency;      // 道具频率

        public DifficultyTier GetDifficultyForSegment(int segmentIndex, int totalSegments)
        {
            float t = segmentIndex / (float)Mathf.Max(1, totalSegments - 1);
            float difficultyValue = speedMultiplier.Evaluate(t);

            if (difficultyValue < 1.2f) return DifficultyTier.Easy;
            if (difficultyValue < 1.5f) return DifficultyTier.Normal;
            if (difficultyValue < 1.8f) return DifficultyTier.Hard;
            return DifficultyTier.Expert;
        }
    }

    // ===================== 运行时数据 =====================

    /// <summary>
    /// 赛季运行时数据 (从服务器同步)
    /// </summary>
    [Serializable]
    public class SeasonRuntimeData
    {
        public int seasonId;
        public SeasonPhase currentPhase;
        public long serverTimeUtc;
        public int battlePassLevel;
        public int battlePassXp;
        public bool isPremiumUnlocked;
        public List<int> claimedFreeTiers = new List<int>();
        public List<int> claimedPremiumTiers = new List<int>();
        public Dictionary<string, EventProgress> eventProgress = new Dictionary<string, EventProgress>();
        public Dictionary<string, int> dailyRewards = new Dictionary<string, int>();
        public long lastLoginTimestamp;
        public int currentStreak;
    }

    [Serializable]
    public class EventProgress
    {
        public string eventId;
        public int currentProgress;
        public int targetProgress;
        public bool isCompleted;
        public bool isClaimed;
        public Dictionary<string, int> subProgress = new Dictionary<string, int>();
    }

    /// <summary>
    /// 生成的关卡数据 (离线烘焙)
    /// </summary>
    [Serializable]
    public class LevelData
    {
        public string levelId;
        public int seasonId;
        public string themeId;
        public int segmentCount;
        public SegmentDefinition[] segments;
        public float totalLength;
        public DifficultyTier overallDifficulty;
        public int seed;                            // 生成种子
        public long generatedTimestamp;
        public string generatorVersion;
    }

    // ===================== 辅助类 =====================

    [Serializable]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField] private List<TKey> keys = new List<TKey>();
        [SerializeField] private List<TValue> values = new List<TValue>();

        public void OnBeforeSerialize()
        {
            keys.Clear();
            values.Clear();
            foreach (var kvp in this)
            {
                keys.Add(kvp.Key);
                values.Add(kvp.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            Clear();
            for (int i = 0; i < keys.Count; i++)
            {
                if (i < values.Count)
                    Add(keys[i], values[i]);
            }
        }
    }
}