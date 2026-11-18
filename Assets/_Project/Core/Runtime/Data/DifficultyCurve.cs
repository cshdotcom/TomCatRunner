using UnityEngine;
using System;

namespace TomCatRunner.Core.Data
{
    /// <summary>
    /// 难度曲线配置 - 控制关卡生成的各项参数随距离/时间的变化
    /// 支持多曲线混合、阶段性突变、Boss 关卡触发
    /// </summary>
    [CreateAssetMenu(fileName = "DifficultyCurve_", menuName = "TomCatRunner/Difficulty Curve", order = 4)]
    public class DifficultyCurve : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _curveId;
        [SerializeField] private string _displayName;

        [Header("Distance Normalization")]
        [SerializeField] private float _maxDistance = 50000f; // 50km 归一化基准
        [SerializeField] private int _phaseCount = 5; // 赛季分几个阶段

        [Header("Core Curves (0-1 normalized distance)")]
        [SerializeField] private AnimationCurve _obstacleDensity = AnimationCurve.Linear(0, 0.2f, 1, 0.8f);
        [SerializeField] private AnimationCurve _collectibleDensity = AnimationCurve.Linear(0, 0.5f, 1, 0.3f);
        [SerializeField] private AnimationCurve _speedMultiplier = AnimationCurve.Linear(0, 1f, 1, 2.5f);
        [SerializeField] private AnimationCurve _laneChangeFrequency = AnimationCurve.Linear(0, 0.1f, 1, 0.6f);
        [SerializeField] private AnimationCurve _reactionTimeWindow = AnimationCurve.Linear(0, 1.5f, 1, 0.5f);
        [SerializeField] private AnimationCurve _comboWindow = AnimationCurve.Linear(0, 2f, 1, 0.8f);

        [Header("Chunk Type Weights (per phase)")]
        [SerializeField] private PhaseChunkWeights[] _phaseWeights;

        [Header("Special Events")]
        [SerializeField] private float _bossChunkInterval = 5000f; // 每 5km 一个 Boss chunk
        [SerializeField] private float _bonusChunkChance = 0.05f;
        [SerializeField] private float _secretChunkChance = 0.02f;

        [Header("Difficulty Modifiers")]
        [SerializeField] private DifficultyModifier[] _modifiers;

        // Properties
        public string CurveId => _curveId;
        public string DisplayName => _displayName;
        public float MaxDistance => _maxDistance;
        public int PhaseCount => _phaseCount;
        public AnimationCurve ObstacleDensity => _obstacleDensity;
        public AnimationCurve CollectibleDensity => _collectibleDensity;
        public AnimationCurve SpeedMultiplier => _speedMultiplier;
        public AnimationCurve LaneChangeFrequency => _laneChangeFrequency;
        public AnimationCurve ReactionTimeWindow => _reactionTimeWindow;
        public AnimationCurve ComboWindow => _comboWindow;
        public PhaseChunkWeights[] PhaseWeights => _phaseWeights;
        public float BossChunkInterval => _bossChunkInterval;
        public float BonusChunkChance => _bonusChunkChance;
        public float SecretChunkChance => _secretChunkChance;
        public DifficultyModifier[] Modifiers => _modifiers;

        /// <summary>
        /// 获取归一化距离处的难度参数
        /// </summary>
        public DifficultyParams Evaluate(float normalizedDistance)
        {
            normalizedDistance = Mathf.Clamp01(normalizedDistance);

            var phaseIndex = GetPhaseIndex(normalizedDistance);
            var phaseWeight = _phaseWeights.Length > phaseIndex ? _phaseWeights[phaseIndex] : _phaseWeights[^1];

            return new DifficultyParams
            {
                NormalizedDistance = normalizedDistance,
                PhaseIndex = phaseIndex,
                ObstacleDensity = _obstacleDensity.Evaluate(normalizedDistance),
                CollectibleDensity = _collectibleDensity.Evaluate(normalizedDistance),
                SpeedMultiplier = _speedMultiplier.Evaluate(normalizedDistance),
                LaneChangeFrequency = _laneChangeFrequency.Evaluate(normalizedDistance),
                ReactionTimeWindow = _reactionTimeWindow.Evaluate(normalizedDistance),
                ComboWindow = _comboWindow.Evaluate(normalizedDistance),
                ChunkWeights = phaseWeight,
                IsBossChunk = IsBossChunk(normalizedDistance),
                IsBonusChunk = UnityEngine.Random.value < _bonusChunkChance,
                IsSecretChunk = UnityEngine.Random.value < _secretChunkChance
            };
        }

        /// <summary>
        /// 获取指定距离的原始参数 (不含随机判定)
        /// </summary>
        public DifficultyParams EvaluateDeterministic(float normalizedDistance, uint seed)
        {
            var state = UnityEngine.Random.state;
            UnityEngine.Random.InitState((int)(seed + normalizedDistance * 10000));
            var result = Evaluate(normalizedDistance);
            UnityEngine.Random.state = state;
            return result;
        }

        private int GetPhaseIndex(float normalizedDistance)
        {
            return Mathf.Min(Mathf.FloorToInt(normalizedDistance * _phaseCount), _phaseCount - 1);
        }

        private bool IsBossChunk(float normalizedDistance)
        {
            float distance = normalizedDistance * _maxDistance;
            return distance > 0 && Mathf.Abs(distance % _bossChunkInterval) < 100f; // 100m 容差
        }

        public bool Validate(out string error)
        {
            if (string.IsNullOrEmpty(_curveId))
            {
                error = "CurveId is empty";
                return false;
            }

            if (_maxDistance <= 0)
            {
                error = "MaxDistance must be > 0";
                return false;
            }

            // 检查曲线单调性 (可选)
            if (!IsMonotonic(_obstacleDensity, true))
            {
                error = "ObstacleDensity curve should be monotonically increasing";
                return false;
            }

            if (!IsMonotonic(_speedMultiplier, true))
            {
                error = "SpeedMultiplier curve should be monotonically increasing";
                return false;
            }

            if (_phaseWeights == null || _phaseWeights.Length != _phaseCount)
            {
                error = $"PhaseWeights length ({_phaseWeights?.Length ?? 0}) != PhaseCount ({_phaseCount})";
                return false;
            }

            foreach (var pw in _phaseWeights)
            {
                float sum = pw.JumpWeight + pw.SlideWeight + pw.AttackWeight + pw.CoinWeight +
                           pw.MixedWeight + pw.HazardWeight + pw.BossWeight + pw.TransitionWeight + pw.SecretWeight;
                if (Mathf.Abs(sum - 1f) > 0.01f)
                {
                    error = $"Phase {pw.PhaseName} weights sum to {sum:F2}, expected 1.0";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private bool IsMonotonic(AnimationCurve curve, bool increasing)
        {
            if (curve.length < 2) return true;
            for (int i = 1; i < curve.length; i++)
            {
                if (increasing && curve.keys[i].value < curve.keys[i - 1].value) return false;
                if (!increasing && curve.keys[i].value > curve.keys[i - 1].value) return false;
            }
            return true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_curveId))
                _curveId = Guid.NewGuid().ToString("N")[..8].ToUpper();

            if (_phaseWeights == null || _phaseWeights.Length != _phaseCount)
            {
                Array.Resize(ref _phaseWeights, _phaseCount);
                for (int i = 0; i < _phaseCount; i++)
                {
                    if (_phaseWeights[i] == null)
                        _phaseWeights[i] = new PhaseChunkWeights();
                    _phaseWeights[i].PhaseName = $"Phase {i + 1}";
                    // 默认均匀分布
                    float w = 1f / 9f;
                    _phaseWeights[i].JumpWeight = w;
                    _phaseWeights[i].SlideWeight = w;
                    _phaseWeights[i].AttackWeight = w;
                    _phaseWeights[i].CoinWeight = w;
                    _phaseWeights[i].MixedWeight = w;
                    _phaseWeights[i].HazardWeight = w;
                    _phaseWeights[i].BossWeight = w;
                    _phaseWeights[i].TransitionWeight = w;
                    _phaseWeights[i].SecretWeight = w;
                }
            }
        }
#endif
    }

    /// <summary>
    /// 单个阶段的 Chunk 类型权重
    /// </summary>
    [Serializable]
    public class PhaseChunkWeights
    {
        public string PhaseName = "Phase 1";
        [Range(0f, 1f)] public float JumpWeight = 0.15f;
        [Range(0f, 1f)] public float SlideWeight = 0.15f;
        [Range(0f, 1f)] public float AttackWeight = 0.1f;
        [Range(0f, 1f)] public float CoinWeight = 0.15f;
        [Range(0f, 1f)] public float MixedWeight = 0.2f;
        [Range(0f, 1f)] public float HazardWeight = 0.1f;
        [Range(0f, 1f)] public float BossWeight = 0.05f;
        [Range(0f, 1f)] public float TransitionWeight = 0.05f;
        [Range(0f, 1f)] public float SecretWeight = 0.05f;
    }

    /// <summary>
    /// 难度修正器 - 特殊条件下的参数调整
    /// </summary>
    [Serializable]
    public class DifficultyModifier
    {
        public string Name;
        public ModifierCondition Condition;
        public float ObstacleDensityMult = 1f;
        public float SpeedMult = 1f;
        public float ReactionTimeMult = 1f;
        public int MaxActiveChunks = 0; // 0 = 无限制
    }

    public enum ModifierCondition
    {
        None,
        PlayerLowHealth,
        PlayerHighCombo,
        TimeOfDay_Night,
        Weather_Rain,
        EventActive,
        PremiumPassActive
    }

    /// <summary>
    /// 评估结果数据结构
    /// </summary>
    [Serializable]
    public struct DifficultyParams
    {
        public float NormalizedDistance;
        public int PhaseIndex;
        public float ObstacleDensity;
        public float CollectibleDensity;
        public float SpeedMultiplier;
        public float LaneChangeFrequency;
        public float ReactionTimeWindow;
        public float ComboWindow;
        public PhaseChunkWeights ChunkWeights;
        public bool IsBossChunk;
        public bool IsBonusChunk;
        public bool IsSecretChunk;

        public override string ToString()
        {
            return $"Dist:{NormalizedDistance:P0} Phase:{PhaseIndex} Obs:{ObstacleDensity:F2} Col:{CollectibleDensity:F2} Spd:{SpeedMultiplier:F2}x";
        }
    }
}