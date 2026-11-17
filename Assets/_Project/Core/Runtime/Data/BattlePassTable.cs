using System;
using UnityEngine;

namespace TomCatRunner.Core.Data
{
    /// <summary>
    /// 战斗通行证配置表 - 双轨奖励系统
    /// 免费轨道 + 付费轨道，支持 XP 曲线、周上限、赛季过期处理
    /// </summary>
    [CreateAssetMenu(fileName = "BattlePassTable_", menuName = "TomCatRunner/Battle Pass Table", order = 3)]
    public class BattlePassTable : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _tableId;
        [SerializeField] private string _displayName;
        [SerializeField] private int _maxLevel = 50;

        [Header("XP Curve")]
        [SerializeField] private AnimationCurve _xpCurve = AnimationCurve.Linear(0, 100, 50, 5000);
        [SerializeField] private int _baseXP = 100;
        [SerializeField] private int _xpPerLevelIncrement = 50;
        [SerializeField] private int _weeklyXPCap = 10000;

        [Header("Free Track Rewards")]
        [SerializeField] private TrackReward[] _freeTrackRewards;

        [Header("Premium Track Rewards")]
        [SerializeField] private TrackReward[] _premiumTrackRewards;

        [Header("Premium Purchase")]
        [SerializeField] private int _premiumPriceGems = 800;
        [SerializeField] private int _premiumPlusPriceGems = 2000; // 含 10 级直升
        [SerializeField] private TrackReward[] _premiumPlusInstantRewards;

        [Header("Season Link")]
        [SerializeField] private SeasonConfig _linkedSeason;

        // Properties
        public string TableId => _tableId;
        public string DisplayName => _displayName;
        public int MaxLevel => _maxLevel;
        public AnimationCurve XPCurve => _xpCurve;
        public int BaseXP => _baseXP;
        public int XPPerLevelIncrement => _xpPerLevelIncrement;
        public int WeeklyXPCap => _weeklyXPCap;
        public TrackReward[] FreeTrackRewards => _freeTrackRewards;
        public TrackReward[] PremiumTrackRewards => _premiumTrackRewards;
        public int PremiumPriceGems => _premiumPriceGems;
        public int PremiumPlusPriceGems => _premiumPlusPriceGems;
        public TrackReward[] PremiumPlusInstantRewards => _premiumPlusInstantRewards;
        public SeasonConfig LinkedSeason => _linkedSeason;

        /// <summary>
        /// 获取指定等级所需总 XP
        /// </summary>
        public int GetTotalXPForLevel(int level)
        {
            level = Mathf.Clamp(level, 1, _maxLevel);
            int total = 0;
            for (int i = 1; i <= level; i++)
            {
                total += GetXPForLevel(i);
            }
            return total;
        }

        /// <summary>
        /// 获取指定等级单级所需 XP
        /// </summary>
        public int GetXPForLevel(int level)
        {
            level = Mathf.Clamp(level, 1, _maxLevel);
            // 优先使用曲线
            if (_xpCurve.length > 0)
            {
                return Mathf.RoundToInt(_xpCurve.Evaluate(level));
            }
            // 回退到线性公式
            return _baseXP + (level - 1) * _xpPerLevelIncrement;
        }

        /// <summary>
        /// 根据总 XP 计算当前等级
        /// </summary>
        public int GetLevelFromXP(int totalXP)
        {
            int level = 1;
            int accumulated = 0;

            while (level <= _maxLevel)
            {
                int needed = GetXPForLevel(level);
                if (accumulated + needed > totalXP)
                    break;
                accumulated += needed;
                level++;
            }

            return Mathf.Min(level, _maxLevel);
        }

        /// <summary>
        /// 获取当前等级进度 0-1
        /// </summary>
        public float GetLevelProgress(int totalXP)
        {
            int level = GetLevelFromXP(totalXP);
            if (level >= _maxLevel) return 1f;

            int currentLevelStartXP = GetTotalXPForLevel(level - 1);
            int xpIntoLevel = totalXP - currentLevelStartXP;
            int xpNeeded = GetXPForLevel(level);

            return Mathf.Clamp01((float)xpIntoLevel / xpNeeded);
        }

        /// <summary>
        /// 获取指定等级的免费奖励
        /// </summary>
        public TrackReward GetFreeReward(int level)
        {
            level = Mathf.Clamp(level, 1, _maxLevel);
            var reward = Array.Find(_freeTrackRewards, r => r.Level == level);
            return reward ?? new TrackReward { Level = level, Rewards = Array.Empty<RewardData>() };
        }

        /// <summary>
        /// 获取指定等级的付费奖励
        /// </summary>
        public TrackReward GetPremiumReward(int level)
        {
            level = Mathf.Clamp(level, 1, _maxLevel);
            var reward = Array.Find(_premiumTrackRewards, r => r.Level == level);
            return reward ?? new TrackReward { Level = level, Rewards = Array.Empty<RewardData>() };
        }

        /// <summary>
        /// 验证配置完整性
        /// </summary>
        public bool Validate(out string error)
        {
            if (string.IsNullOrEmpty(_tableId))
            {
                error = "TableId is empty";
                return false;
            }

            if (_maxLevel < 1 || _maxLevel > 200)
            {
                error = "MaxLevel must be 1-200";
                return false;
            }

            if (_freeTrackRewards == null || _freeTrackRewards.Length != _maxLevel)
            {
                error = $"FreeTrackRewards length ({_freeTrackRewards?.Length ?? 0}) != MaxLevel ({_maxLevel})";
                return false;
            }

            if (_premiumTrackRewards == null || _premiumTrackRewards.Length != _maxLevel)
            {
                error = $"PremiumTrackRewards length ({_premiumTrackRewards?.Length ?? 0}) != MaxLevel ({_maxLevel})";
                return false;
            }

            // 检查等级连续性
            for (int i = 1; i <= _maxLevel; i++)
            {
                if (Array.FindIndex(_freeTrackRewards, r => r.Level == i) < 0)
                {
                    error = $"Free track missing level {i}";
                    return false;
                }
                if (Array.FindIndex(_premiumTrackRewards, r => r.Level == i) < 0)
                {
                    error = $"Premium track missing level {i}";
                    return false;
                }
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_tableId))
                _tableId = Guid.NewGuid().ToString("N")[..8].ToUpper();

            // 自动填充等级
            if (_freeTrackRewards == null || _freeTrackRewards.Length != _maxLevel)
            {
                Array.Resize(ref _freeTrackRewards, _maxLevel);
                for (int i = 0; i < _maxLevel; i++)
                {
                    if (_freeTrackRewards[i] == null)
                        _freeTrackRewards[i] = new TrackReward();
                    _freeTrackRewards[i].Level = i + 1;
                }
            }

            if (_premiumTrackRewards == null || _premiumTrackRewards.Length != _maxLevel)
            {
                Array.Resize(ref _premiumTrackRewards, _maxLevel);
                for (int i = 0; i < _maxLevel; i++)
                {
                    if (_premiumTrackRewards[i] == null)
                        _premiumTrackRewards[i] = new TrackReward();
                    _premiumTrackRewards[i].Level = i + 1;
                }
            }
        }
#endif
    }

    /// <summary>
    /// 单级奖励数据
    /// </summary>
    [Serializable]
    public class TrackReward
    {
        public int Level;
        public RewardData[] Rewards;
        public string Description; // UI 显示用
        public bool IsMilestone;   // 是否为里程碑等级 (每 10 级等)
    }

    /// <summary>
    /// 奖励数据结构
    /// </summary>
    [Serializable]
    public class RewardData
    {
        public RewardType Type;
        public string ItemId;      // 皮肤ID、道具ID等
        public int Amount;         // 数量 (货币类用)
        public int Weight = 100;   // 权重 (用于随机奖励)
        public bool IsBonus;       // 额外奖励标记
    }

    public enum RewardType
    {
        Currency_Gold,
        Currency_Gem,
        Currency_EventToken,
        Skin_Character,
        Skin_Trail,
        Emote,
        Pet,
        Consumable_Revive,
        Consumable_ScoreBoost,
        Consumable_CoinMagnet,
        Consumable_Shield,
        LootBox_Common,
        LootBox_Rare,
        LootBox_Epic,
        LootBox_Legendary,
        XP_Boost,
        BattlePass_LevelSkip
    }
}