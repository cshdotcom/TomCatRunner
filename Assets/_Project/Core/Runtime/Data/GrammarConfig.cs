using System;
using System.Collections.Generic;
using UnityEngine;

namespace TomCatRunner.Core.Data
{
    /// <summary>
    /// 语法配置 - 定义程序化关卡生成的文法规则
    /// 基于上下文无关文法 (CFG) + 马尔可夫链权重
    /// </summary>
    [CreateAssetMenu(fileName = "GrammarConfig_", menuName = "TomCatRunner/Grammar Config", order = 5)]
    public class GrammarConfig : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _grammarId;
        [SerializeField] private string _displayName;

        [Header("Grammar Rules")]
        [SerializeField] private string _startRule = "Level";
        [SerializeField] private GrammarRule[] _rules;

        [Header("Markov Chain Weights")]
        [SerializeField] private MarkovTransition[] _transitions;

        [Header("Chunk Definitions")]
        [SerializeField] private ChunkDefinition[] _chunkDefinitions;

        [Header("Constraints")]
        [SerializeField] private int _minChunkLength = 3;
        [SerializeField] private int _maxChunkLength = 15;
        [SerializeField] private float _requiredChunkSpacing = 50f;
        [SerializeField] private ChunkType[] _requiredChunks;

        [Header("Validation")]
        [SerializeField] private bool _validateReachability = true;
        [SerializeField] private int _maxGenerationAttempts = 10;

        // Properties
        public string GrammarId => _grammarId;
        public string DisplayName => _displayName;
        public string StartRule => _startRule;
        public GrammarRule[] Rules => _rules;
        public MarkovTransition[] Transitions => _transitions;
        public ChunkDefinition[] ChunkDefinitions => _chunkDefinitions;
        public int MinChunkLength => _minChunkLength;
        public int MaxChunkLength => _maxChunkLength;
        public float RequiredChunkSpacing => _requiredChunkSpacing;
        public ChunkType[] RequiredChunks => _requiredChunks;
        public bool ValidateReachability => _validateReachability;
        public int MaxGenerationAttempts => _maxGenerationAttempts;

        /// <summary>
        /// 获取 Chunk 定义
        /// </summary>
        public ChunkDefinition GetChunkDefinition(ChunkType type)
        {
            return Array.Find(_chunkDefinitions, c => c.Type == type);
        }

        /// <summary>
        /// 获取规则产生式
        /// </summary>
        public string[] GetProductions(string nonTerminal)
        {
            var rule = Array.Find(_rules, r => r.NonTerminal == nonTerminal);
            return rule?.Productions ?? Array.Empty<string>();
        }

        /// <summary>
        /// 获取马尔可夫转移权重
        /// </summary>
        public float GetTransitionWeight(ChunkType from, ChunkType to)
        {
            var transition = Array.Find(_transitions, t => t.From == from && t.To == to);
            return transition?.Weight ?? 0f;
        }

        /// <summary>
        /// 获取所有可能的下一个 Chunk 类型及权重
        /// </summary>
        public (ChunkType type, float weight)[] GetNextChunkWeights(ChunkType current)
        {
            var results = new List<(ChunkType, float)>();
            foreach (var t in _transitions)
            {
                if (t.From == current && t.Weight > 0)
                {
                    results.Add((t.To, t.Weight));
                }
            }
            return results.ToArray();
        }

        public bool Validate(out string error)
        {
            if (string.IsNullOrEmpty(_grammarId))
            {
                error = "GrammarId is empty";
                return false;
            }

            if (string.IsNullOrEmpty(_startRule))
            {
                error = "StartRule is empty";
                return false;
            }

            if (_rules == null || _rules.Length == 0)
            {
                error = "No grammar rules defined";
                return false;
            }

            // 检查开始符号是否有定义
            if (Array.FindIndex(_rules, r => r.NonTerminal == _startRule) < 0)
            {
                error = $"Start rule '{_startRule}' not defined in rules";
                return false;
            }

            // 检查所有非终结符都有定义
            var definedNonTerminals = new HashSet<string>();
            foreach (var rule in _rules)
            {
                definedNonTerminals.Add(rule.NonTerminal);
            }

            foreach (var rule in _rules)
            {
                foreach (var prod in rule.Productions)
                {
                    var symbols = prod.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var sym in symbols)
                    {
                        // 大写开头视为非终结符
                        if (char.IsUpper(sym[0]) && !definedNonTerminals.Contains(sym))
                        {
                            error = $"Undefined non-terminal: {sym} in rule {rule.NonTerminal}";
                            return false;
                        }
                    }
                }
            }

            // 检查 Chunk 定义完整性
            if (_chunkDefinitions == null || _chunkDefinitions.Length == 0)
            {
                error = "No chunk definitions";
                return false;
            }

            var definedChunks = new HashSet<ChunkType>();
            foreach (var cd in _chunkDefinitions)
            {
                if (definedChunks.Contains(cd.Type))
                {
                    error = $"Duplicate chunk definition: {cd.Type}";
                    return false;
                }
                definedChunks.Add(cd.Type);

                if (cd.PrefabRef == null && string.IsNullOrEmpty(cd.AddressableKey))
                {
                    error = $"Chunk {cd.Type} missing both PrefabRef and AddressableKey";
                    return false;
                }
            }

            // 检查必需 Chunk 都有定义
            if (_requiredChunks != null)
            {
                foreach (var req in _requiredChunks)
                {
                    if (!definedChunks.Contains(req))
                    {
                        error = $"Required chunk {req} not defined";
                        return false;
                    }
                }
            }

            // 检查马尔可夫链权重归一化
            var fromGroups = new Dictionary<ChunkType, float>();
            foreach (var t in _transitions ?? Array.Empty<MarkovTransition>())
            {
                if (!fromGroups.ContainsKey(t.From))
                    fromGroups[t.From] = 0f;
                fromGroups[t.From] += t.Weight;
            }

            foreach (var kvp in fromGroups)
            {
                if (Mathf.Abs(kvp.Value - 1f) > 0.01f)
                {
                    error = $"Markov weights for {kvp.Key} sum to {kvp.Value:F2}, expected 1.0";
                    return false;
                }
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_grammarId))
                _grammarId = Guid.NewGuid().ToString("N")[..8].ToUpper();

            // 默认规则
            if (_rules == null || _rules.Length == 0)
            {
                _rules = new[]
                {
                    new GrammarRule { NonTerminal = "Level", Productions = new[] { "Segment Level", "Segment" } },
                    new GrammarRule { NonTerminal = "Segment", Productions = new[] { "JumpChunk", "SlideChunk", "AttackChunk", "CoinChunk", "MixedChunk", "HazardChunk" } },
                    new GrammarRule { NonTerminal = "JumpChunk", Productions = new[] { "LowJump", "HighJump", "DoubleJump", "TripleJump" } },
                    new GrammarRule { NonTerminal = "SlideChunk", Productions = new[] { "ShortSlide", "LongSlide", "SlideJump" } },
                    new GrammarRule { NonTerminal = "AttackChunk", Productions = new[] { "SingleTarget", "MultiTarget", "BossAttack" } },
                    new GrammarRule { NonTerminal = "CoinChunk", Productions = new[] { "CoinLine", "CoinArc", "CoinCluster", "CoinSpiral" } },
                    new GrammarRule { NonTerminal = "MixedChunk", Productions = new[] { "JumpSlide", "JumpAttack", "SlideAttack", "JumpSlideAttack" } },
                    new GrammarRule { NonTerminal = "HazardChunk", Productions = new[] { "Spikes", "Pit", "MovingObstacle", "TimedHazard" } },
                };
            }

            // 默认马尔可夫转移
            if (_transitions == null || _transitions.Length == 0)
            {
                var transitions = new List<MarkovTransition>();
                var types = Enum.GetValues(typeof(ChunkType));
                foreach (ChunkType from in types)
                {
                    foreach (ChunkType to in types)
                    {
                        float weight = from == to ? 0.1f : 1f; // 避免连续相同
                        // 某些自然转移提高权重
                        if ((from == ChunkType.Jump && to == ChunkType.Slide) ||
                            (from == ChunkType.Slide && to == ChunkType.Coin) ||
                            (from == ChunkType.Coin && to == ChunkType.Mixed) ||
                            (from == ChunkType.Mixed && to == ChunkType.Hazard))
                            weight = 2f;

                        transitions.Add(new MarkovTransition { From = from, To = to, Weight = weight });
                    }
                }
                // 归一化
                var groups = new Dictionary<ChunkType, float>();
                foreach (var t in transitions) groups[t.From] = groups.GetValueOrDefault(t.From) + t.Weight;
                foreach (var t in transitions) t.Weight /= groups[t.From];

                _transitions = transitions.ToArray();
            }

            // 默认 Chunk 定义
            if (_chunkDefinitions == null || _chunkDefinitions.Length == 0)
            {
                _chunkDefinitions = new[]
                {
                    new ChunkDefinition { Type = ChunkType.Jump, PrefabRef = null, AddressableKey = "Chunk_Jump", MinLength = 2, MaxLength = 5 },
                    new ChunkDefinition { Type = ChunkType.Slide, PrefabRef = null, AddressableKey = "Chunk_Slide", MinLength = 2, MaxLength = 5 },
                    new ChunkDefinition { Type = ChunkType.Attack, PrefabRef = null, AddressableKey = "Chunk_Attack", MinLength = 3, MaxLength = 6 },
                    new ChunkDefinition { Type = ChunkType.Coin, PrefabRef = null, AddressableKey = "Chunk_Coin", MinLength = 2, MaxLength = 4 },
                    new ChunkDefinition { Type = ChunkType.Mixed, PrefabRef = null, AddressableKey = "Chunk_Mixed", MinLength = 4, MaxLength = 8 },
                    new ChunkDefinition { Type = ChunkType.Hazard, PrefabRef = null, AddressableKey = "Chunk_Hazard", MinLength = 2, MaxLength = 5 },
                    new ChunkDefinition { Type = ChunkType.Boss, PrefabRef = null, AddressableKey = "Chunk_Boss", MinLength = 8, MaxLength = 12 },
                    new ChunkDefinition { Type = ChunkType.Transition, PrefabRef = null, AddressableKey = "Chunk_Transition", MinLength = 1, MaxLength = 3 },
                    new ChunkDefinition { Type = ChunkType.Secret, PrefabRef = null, AddressableKey = "Chunk_Secret", MinLength = 3, MaxLength = 6 },
                    new ChunkDefinition { Type = ChunkType.Powerup, PrefabRef = null, AddressableKey = "Chunk_Powerup", MinLength = 1, MaxLength = 2 },
                };
            }
        }
#endif
    }

    /// <summary>
    /// 文法规则
    /// </summary>
    [Serializable]
    public class GrammarRule
    {
        public string NonTerminal;           // 非终结符，如 "Level", "Segment"
        public string[] Productions;         // 产生式，如 "Segment Level", "JumpChunk"
        public float Weight = 1f;            // 规则权重
        public string Condition;             // 可选条件表达式
    }

    /// <summary>
    /// 马尔可夫链转移
    /// </summary>
    [Serializable]
    public class MarkovTransition
    {
        public ChunkType From;
        public ChunkType To;
        public float Weight;
    }

    /// <summary>
    /// Chunk 类型枚举
    /// </summary>
    public enum ChunkType
    {
        Jump,       // 跳跃为主
        Slide,      // 滑铲为主
        Attack,     // 攻击为主
        Coin,       // 金币收集为主
        Mixed,      // 混合类型
        Hazard,     // 高危险
        Boss,       // Boss 关卡
        Transition, // 过渡/连接
        Secret,     // 隐藏/彩蛋
        Powerup     // 道具奖励
    }

    /// <summary>
    /// Chunk 定义 - 运行时实例化所需信息
    /// </summary>
    [Serializable]
    public class ChunkDefinition
    {
        public ChunkType Type;
        public GameObject PrefabRef;          // 直接引用 (Resources/场景中)
        public string AddressableKey;         // Addressables 加载键
        public int MinLength = 2;             // 最小段数
        public int MaxLength = 5;             // 最大段数
        public float Weight = 1f;             // 生成权重
        public ChunkConstraint[] Constraints; // 约束条件

        public float AverageLength => (MinLength + MaxLength) * 0.5f;
    }

    /// <summary>
    /// Chunk 生成约束
    /// </summary>
    [Serializable]
    public class ChunkConstraint
    {
        public ConstraintType Type;
        public string Parameter; // JSON 或简单键值
        public float Value;
    }

    public enum ConstraintType
    {
        MinDistanceFromSame,    // 同类型最小间距
        MaxConsecutive,         // 最大连续出现次数
        RequireBefore,          // 必须在指定类型前出现
        RequireAfter,           // 必须在指定类型后出现
        ForbidAdjacent,         // 禁止相邻
        ForceAtDistance,        // 强制在特定距离出现
        MinPlayerLevel,         // 最低玩家等级
        MaxDifficulty           // 最大难度阈值
    }
}