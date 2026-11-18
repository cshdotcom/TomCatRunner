using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace TomCatRunner.Core.Data
{
    /// <summary>
    /// 赛季配置 - ScriptableObject
    /// 每个赛季一个实例，包含时间、主题、通行证、活动配置
    /// </summary>
    [CreateAssetMenu(fileName = "SeasonConfig_", menuName = "TomCatRunner/Season Config", order = 0)]
    public class SeasonConfig : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _seasonId;
        [SerializeField] private string _displayName;
        [SerializeField] private int _sortOrder;

        [Header("Time Range (UTC)")]
        [SerializeField] private DateTime _startTime;
        [SerializeField] private DateTime _endTime;
        [SerializeField] private bool _useServerTime = true;

        [Header("Theme & Content")]
        [SerializeField] private ThemeProfile _themeProfile;
        [SerializeField] private AssetReference _seasonContentBundle; // Addressables label

        [Header("Battle Pass")]
        [SerializeField] private BattlePassTable _battlePassTable;

        [Header("Events")]
        [SerializeField] private MiniEventConfig[] _eventConfigs;

        [Header("Difficulty")]
        [SerializeField] private DifficultyCurve _difficultyCurve;

        // Properties
        public string SeasonId => _seasonId;
        public string DisplayName => _displayName;
        public int SortOrder => _sortOrder;
        public DateTime StartTime => _startTime;
        public DateTime EndTime => _endTime;
        public bool UseServerTime => _useServerTime;
        public ThemeProfile ThemeProfile => _themeProfile;
        public AssetReference SeasonContentBundle => _seasonContentBundle;
        public BattlePassTable BattlePassTable => _battlePassTable;
        public MiniEventConfig[] EventConfigs => _eventConfigs;
        public DifficultyCurve DifficultyCurve => _difficultyCurve;

        public TimeSpan Duration => _endTime - _startTime;
        public bool IsActive => DateTime.UtcNow >= _startTime && DateTime.UtcNow < _endTime;
        public bool IsUpcoming => DateTime.UtcNow < _startTime;
        public bool IsExpired => DateTime.UtcNow >= _endTime;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_seasonId))
                _seasonId = Guid.NewGuid().ToString("N")[..8].ToUpper();

            if (_endTime <= _startTime)
                _endTime = _startTime.AddDays(30); // 默认 30 天
        }
#endif
    }

    /// <summary>
    /// 活动配置 (内嵌在 SeasonConfig 中)
    /// </summary>
    [Serializable]
    public class MiniEventConfig
    {
        public string EventId;
        public string DisplayName;
        public EventType Type;
        public DateTime StartTime;
        public DateTime EndTime;
        public EventReward[] Rewards;
        public string RequiredProgressKey; // 进度键，如 "distance_total"
        public float RequiredProgressValue;
    }

    public enum EventType
    {
        Daily,
        Weekly,
        Seasonal,
        OneTime,
        Community
    }

    [Serializable]
    public class EventReward
    {
        public RewardType Type;
        public string ItemId;
        public int Amount;
        public int MilestoneIndex; // 第几个里程碑解锁
    }

    // RewardType 统一定义在 BattlePassTable.cs（更完整的成员集）
}