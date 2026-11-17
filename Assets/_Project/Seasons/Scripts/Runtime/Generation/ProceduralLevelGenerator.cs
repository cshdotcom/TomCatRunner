using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Cysharp.Threading.Tasks;
using TomCatRunner.Common;

namespace TomCatRunner.Seasons
{
    /// <summary>
    /// 程序化关卡生成器 - 基于语法树和马尔可夫链生成关卡数据
    /// 支持离线烘焙和运行时生成
    /// </summary>
    public class ProceduralLevelGenerator : MonoSingleton<ProceduralLevelGenerator>
    {
        [Header("生成设置")]
        [SerializeField] private int _defaultSeed = 12345;
        [SerializeField] private bool _useFixedSeed = false;
        [SerializeField] private bool _debugLog = true;

        // 状态
        private ThemeProfile _currentTheme;
        private DifficultyCurve _difficultyCurve;
        private System.Random _rng;
        private int _currentSeed;

        // 马尔可夫链转移矩阵: ChunkType -> (NextChunkType -> Probability)
        private readonly Dictionary<ChunkType, Dictionary<ChunkType, float>> _transitionMatrix = new Dictionary<ChunkType, Dictionary<ChunkType, float>>();

        // 语法规则
        private readonly List<GrammarRule> _grammarRules = new List<GrammarRule>();

        // 事件
        public event Action<LevelData> OnLevelGenerated;
        public event Action<float> OnGenerationProgress;

        protected override void OnInit()
        {
            base.OnInit();
            InitializeDefaultGrammar();
            InitializeDefaultTransitions();
        }

        /// <summary>
        /// 生成完整关卡数据 (离线/编辑器使用)
        /// </summary>
        public async UniTask<LevelData> GenerateLevelAsync(ThemeProfile theme, DifficultyCurve difficultyCurve, int seed = -1, int segmentCount = -1)
        {
            _currentTheme = theme;
            _difficultyCurve = difficultyCurve;
            _currentSeed = seed > 0 ? seed : (_useFixedSeed ? _defaultSeed : UnityEngine.Random.Range(1, int.MaxValue));
            _rng = new System.Random(_currentSeed);

            if (_debugLog) Debug.Log($"[ProceduralLevelGenerator] Generating level with seed: {_currentSeed}");

            int count = segmentCount > 0 ? segmentCount : theme.segmentsPerLevel;
            var segments = new List<SegmentDefinition>();

            float currentZ = 0f;
            ChunkType previousChunkType = ChunkType.Straight;

            for (int i = 0; i < count; i++)
            {
                float progress = (float)i / count;
                OnGenerationProgress?.Invoke(progress);

                var segment = GenerateSegment(i, count, currentZ, previousChunkType, progress);
                segments.Add(segment);

                currentZ += segment.length;
                previousChunkType = segment.chunks.LastOrDefault()?.chunkId.GetChunkType() ?? ChunkType.Straight;

                await UniTask.Yield(); // 避免阻塞主线程
            }

            var levelData = new LevelData
            {
                levelId = $"{theme.themeId}_Level_{_currentSeed}",
                seasonId = 0, // 由调用者设置
                themeId = theme.themeId,
                segmentCount = segments.Count,
                segments = segments.ToArray(),
                totalLength = currentZ,
                overallDifficulty = CalculateOverallDifficulty(segments),
                seed = _currentSeed,
                generatedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                generatorVersion = "1.0.0"
            };

            OnLevelGenerated?.Invoke(levelData);

            if (_debugLog) Debug.Log($"[ProceduralLevelGenerator] Generated level: {levelData.levelId}, Length: {levelData.totalLength}m, Segments: {levelData.segmentCount}");

            return levelData;
        }

        /// <summary>
        /// 生成单个段落
        /// </summary>
        private SegmentDefinition GenerateSegment(int segmentIndex, int totalSegments, float startZ, ChunkType previousType, float difficultyProgress)
        {
            var difficulty = _difficultyCurve?.GetDifficultyForSegment(segmentIndex, totalSegments) ?? DifficultyTier.Normal;
            float segmentLength = _currentTheme.baseSegmentLength;

            // 根据难度调整段落长度
            segmentLength *= 0.8f + 0.4f * difficultyProgress; // 后期段落更长

            var chunks = new List<ChunkInstance>();
            float currentChunkZ = 0f;
            ChunkType lastChunkType = previousType;

            // 确定该段落包含多少个 Chunk
            int chunkCount = Mathf.Max(1, Mathf.RoundToInt(segmentLength / 30f)); // 平均每个 Chunk 30m

            for (int c = 0; c < chunkCount; c++)
            {
                float chunkProgress = (float)c / chunkCount;
                float combinedProgress = (segmentIndex + chunkProgress) / totalSegments;

                // 选择 Chunk 类型
                ChunkType chunkType = SelectChunkType(lastChunkType, difficulty, combinedProgress);

                // 获取该类型的 ChunkDefinition
                var chunkDef = GetChunkDefinition(chunkType, difficulty, segmentIndex);
                if (chunkDef == null) continue;

                // 选择变体
                int variationIndex = SelectVariation(chunkDef);

                // 计算长度
                float chunkLength = chunkDef.length * (0.8f + 0.4f * chunkProgress);

                var chunkInstance = new ChunkInstance
                {
                    chunkId = chunkDef.id,
                    variationIndex = variationIndex,
                    localPosition = new Vector3(0, 0, currentChunkZ),
                    localRotation = Quaternion.identity,
                    runtimeParameters = GenerateRuntimeParameters(chunkDef, difficulty, combinedProgress)
                };

                chunks.Add(chunkInstance);
                currentChunkZ += chunkLength;
                lastChunkType = chunkType;
            }

            // 确保段落长度匹配
            float actualLength = chunks.Sum(c => GetChunkLength(c));
            float scale = segmentLength / Mathf.Max(0.1f, actualLength);

            foreach (var chunk in chunks)
            {
                chunk.localPosition = new Vector3(chunk.localPosition.x, chunk.localPosition.y, chunk.localPosition.z * scale);
            }

            // 创建检查点 (每 3-5 个段落一个)
            CheckpointData checkpoint = null;
            if (segmentIndex % UnityEngine.Random.Range(3, 6) == 0 && segmentIndex > 0)
            {
                checkpoint = new CheckpointData
                {
                    distanceFromStart = startZ + segmentLength * 0.5f,
                    position = new Vector3(0, 1f, startZ + segmentLength * 0.5f),
                    rotation = Quaternion.identity,
                    coinsCollected = 0,
                    timeElapsed = 0f
                };
            }

            return new SegmentDefinition
            {
                id = $"{_currentTheme.themeId}_Seg_{segmentIndex}",
                index = segmentIndex,
                length = segmentLength,
                chunks = chunks,
                startPosition = new Vector3(0, 0, startZ),
                endPosition = new Vector3(0, 0, startZ + segmentLength),
                checkpoint = checkpoint
            };
        }

        /// <summary>
        /// 基于马尔可夫链和语法规则选择 Chunk 类型
        /// </summary>
        private ChunkType SelectChunkType(ChunkType previousType, DifficultyTier difficulty, float progress)
        {
            // 1. 获取基础转移概率
            var transitions = _transitionMatrix.GetValueOrDefault(previousType, new Dictionary<ChunkType, float>());

            // 2. 应用难度调整
            var adjustedWeights = new Dictionary<ChunkType, float>();
            foreach (var kvp in transitions)
            {
                float weight = kvp.Value;

                // 难度调整
                switch (difficulty)
                {
                    case DifficultyTier.Easy:
                        if (kvp.Key == ChunkType.Hazard) weight *= 0.3f;
                        if (kvp.Key == ChunkType.Boss) weight *= 0.1f;
                        if (kvp.Key == ChunkType.Coin) weight *= 1.5f;
                        if (kvp.Key == ChunkType.Powerup) weight *= 1.3f;
                        break;
                    case DifficultyTier.Hard:
                        if (kvp.Key == ChunkType.Hazard) weight *= 1.5f;
                        if (kvp.Key == ChunkType.Attack) weight *= 1.3f;
                        if (kvp.Key == ChunkType.Coin) weight *= 0.7f;
                        break;
                    case DifficultyTier.Expert:
                        if (kvp.Key == ChunkType.Hazard) weight *= 2f;
                        if (kvp.Key == ChunkType.Boss) weight *= 1.5f;
                        if (kvp.Key == ChunkType.Mixed) weight *= 1.4f;
                        break;
                }

                // 进度调整 (后期增加难度)
                if (kvp.Key == ChunkType.Hazard || kvp.Key == ChunkType.Attack || kvp.Key == ChunkType.Mixed)
                    weight *= 1f + progress;
                if (kvp.Key == ChunkType.Coin || kvp.Key == ChunkType.Powerup)
                    weight *= 1f - progress * 0.5f;

                adjustedWeights[kvp.Key] = Mathf.Max(0.01f, weight);
            }

            // 3. 应用语法规则约束
            foreach (var rule in _grammarRules)
            {
                if (rule.Condition(previousType, difficulty, progress))
                {
                    rule.Apply(adjustedWeights);
                }
            }

            // 4. 从主题配置获取可用 Chunk 并进一步筛选
            var availableChunks = _currentTheme.chunkDefinitions
                .Where(cd => cd.minDifficulty <= difficulty && cd.maxDifficulty >= difficulty)
                .ToList();

            // 合并权重
            var finalWeights = new Dictionary<ChunkType, float>();
            foreach (var chunk in availableChunks)
            {
                float baseWeight = chunk.weight;
                float transitionWeight = adjustedWeights.GetValueOrDefault(chunk.type, 0.1f);
                finalWeights[chunk.type] = baseWeight * transitionWeight;
            }

            // 5. 加权随机选择
            return WeightedRandom(finalWeights);
        }

        /// <summary>
        /// 获取 ChunkDefinition
        /// </summary>
        private ChunkDefinition GetChunkDefinition(ChunkType type, DifficultyTier difficulty, int segmentIndex)
        {
            var candidates = _currentTheme.chunkDefinitions
                .Where(cd => cd.type == type &&
                           cd.minDifficulty <= difficulty && cd.maxDifficulty >= difficulty &&
                           cd.minSegmentIndex <= segmentIndex && cd.maxSegmentIndex >= segmentIndex)
                .ToList();

            if (candidates.Count == 0) return null;

            // 加权随机选择
            float totalWeight = candidates.Sum(c => c.weight);
            float rand = (float)_rng.NextDouble() * totalWeight;
            float accum = 0f;

            foreach (var c in candidates)
            {
                accum += c.weight;
                if (rand <= accum) return c;
            }

            return candidates.Last();
        }

        /// <summary>
        /// 选择变体
        /// </summary>
        private int SelectVariation(ChunkDefinition chunkDef)
        {
            if (chunkDef.variations == null || chunkDef.variations.Length == 0) return 0;

            float totalWeight = chunkDef.variations.Sum(v => v.weight);
            float rand = (float)_rng.NextDouble() * totalWeight;
            float accum = 0f;

            for (int i = 0; i < chunkDef.variations.Length; i++)
            {
                accum += chunkDef.variations[i].weight;
                if (rand <= accum) return i;
            }

            return chunkDef.variations.Length - 1;
        }

        /// <summary>
        /// 生成运行时参数
        /// </summary>
        private SerializableDictionary<string, float> GenerateRuntimeParameters(ChunkDefinition chunkDef, DifficultyTier difficulty, float progress)
        {
            var parameters = new SerializableDictionary<string, float>();

            // 基础参数
            parameters["difficulty"] = (int)difficulty;
            parameters["progress"] = progress;

            // 根据类型添加特定参数
            switch (chunkDef.type)
            {
                case ChunkType.Jump:
                    parameters["jumpHeight"] = 3f + (int)difficulty * 0.5f;
                    parameters["gapWidth"] = 4f + progress * 2f;
                    break;
                case ChunkType.Slide:
                    parameters["slideLength"] = 10f + progress * 5f;
                    parameters["obstacleHeight"] = 1.5f;
                    break;
                case ChunkType.Hazard:
                    parameters["hazardFrequency"] = 0.3f + (int)difficulty * 0.1f + progress * 0.2f;
                    parameters["hazardSpeed"] = 5f + (int)difficulty * 1f;
                    break;
                case ChunkType.Coin:
                    parameters["coinDensity"] = 0.8f - progress * 0.3f;
                    parameters["coinPattern"] = _rng.Next(0, 3); // 0=直线, 1=弧线, 2=螺旋
                    break;
                case ChunkType.Powerup:
                    parameters["powerupType"] = _rng.Next(0, 4); // 0=磁铁, 1=无敌, 2=冲刺, 3=倍率
                    break;
            }

            // 合并变体参数
            if (chunkDef.variations != null && chunkDef.variations.Length > 0)
            {
                var variation = chunkDef.variations[0]; // 简化：使用第一个变体参数
                if (variation.parameterOverrides != null)
                {
                    foreach (var kvp in variation.parameterOverrides)
                    {
                        parameters[kvp.Key] = kvp.Value;
                    }
                }
            }

            return parameters;
        }

        /// <summary>
        /// 获取 Chunk 长度
        /// </summary>
        private float GetChunkLength(ChunkInstance chunkInstance)
        {
            var def = _currentTheme.chunkDefinitions.FirstOrDefault(c => c.id == chunkInstance.chunkId);
            return def?.length ?? 30f;
        }

        /// <summary>
        /// 计算整体难度
        /// </summary>
        private DifficultyTier CalculateOverallDifficulty(List<SegmentDefinition> segments)
        {
            if (segments.Count == 0) return DifficultyTier.Normal;

            int hazardCount = 0;
            int totalChunks = 0;

            foreach (var seg in segments)
            {
                foreach (var chunk in seg.chunks)
                {
                    totalChunks++;
                    var def = _currentTheme.chunkDefinitions.FirstOrDefault(c => c.id == chunk.chunkId);
                    if (def?.type == ChunkType.Hazard || def?.type == ChunkType.Boss)
                        hazardCount++;
                }
            }

            float hazardRatio = (float)hazardCount / Mathf.Max(1, totalChunks);

            if (hazardRatio < 0.15f) return DifficultyTier.Easy;
            if (hazardRatio < 0.3f) return DifficultyTier.Normal;
            if (hazardRatio < 0.5f) return DifficultyTier.Hard;
            return DifficultyTier.Expert;
        }

        /// <summary>
        /// 加权随机选择
        /// </summary>
        private T WeightedRandom<T>(Dictionary<T, float> weights) where T : Enum
        {
            float total = weights.Values.Sum();
            float rand = (float)_rng.NextDouble() * total;
            float accum = 0f;

            foreach (var kvp in weights)
            {
                accum += kvp.Value;
                if (rand <= accum) return kvp.Key;
            }

            return weights.Keys.Last();
        }

        /// <summary>
        /// 初始化默认语法规则
        /// </summary>
        private void InitializeDefaultGrammar()
        {
            // 规则1: 直道后不应立即接直道 (避免单调)
            _grammarRules.Add(new GrammarRule
            {
                name = "NoConsecutiveStraight",
                Condition = (prev, diff, prog) => prev == ChunkType.Straight,
                Apply = (weights) =>
                {
                    if (weights.ContainsKey(ChunkType.Straight))
                        weights[ChunkType.Straight] *= 0.1f;
                }
            });

            // 规则2: 危险段后应给玩家喘息机会 (Coin/Powerup)
            _grammarRules.Add(new GrammarRule
            {
                name = "ReliefAfterHazard",
                Condition = (prev, diff, prog) => prev == ChunkType.Hazard,
                Apply = (weights) =>
                {
                    if (weights.ContainsKey(ChunkType.Coin)) weights[ChunkType.Coin] *= 2f;
                    if (weights.ContainsKey(ChunkType.Powerup)) weights[ChunkType.Powerup] *= 2f;
                    if (weights.ContainsKey(ChunkType.Hazard)) weights[ChunkType.Hazard] *= 0.2f;
                }
            });

            // 规则3: Boss 段前必须有铺垫
            _grammarRules.Add(new GrammarRule
            {
                name = "BossBuildup",
                Condition = (prev, diff, prog) => prog > 0.7f && prev != ChunkType.Boss,
                Apply = (weights) =>
                {
                    if (weights.ContainsKey(ChunkType.Boss)) weights[ChunkType.Boss] *= 5f;
                    if (weights.ContainsKey(ChunkType.Mixed)) weights[ChunkType.Mixed] *= 2f;
                }
            });

            // 规则4: 早期多给奖励
            _grammarRules.Add(new GrammarRule
            {
                name = "EarlyRewards",
                Condition = (prev, diff, prog) => prog < 0.3f,
                Apply = (weights) =>
                {
                    if (weights.ContainsKey(ChunkType.Coin)) weights[ChunkType.Coin] *= 1.5f;
                    if (weights.ContainsKey(ChunkType.Powerup)) weights[ChunkType.Powerup] *= 1.5f;
                }
            });
        }

        /// <summary>
        /// 初始化默认马尔可夫转移矩阵
        /// </summary>
        private void InitializeDefaultTransitions()
        {
            // 直道 -> 任何
            _transitionMatrix[ChunkType.Straight] = new Dictionary<ChunkType, float>
            {
                [ChunkType.Straight] = 0.2f,
                [ChunkType.Jump] = 0.2f,
                [ChunkType.Slide] = 0.15f,
                [ChunkType.Attack] = 0.1f,
                [ChunkType.Coin] = 0.15f,
                [ChunkType.Powerup] = 0.1f,
                [ChunkType.Hazard] = 0.05f,
                [ChunkType.Mixed] = 0.05f
            };

            // 跳跃 -> 滑铲/攻击/直道
            _transitionMatrix[ChunkType.Jump] = new Dictionary<ChunkType, float>
            {
                [ChunkType.Straight] = 0.3f,
                [ChunkType.Slide] = 0.25f,
                [ChunkType.Attack] = 0.15f,
                [ChunkType.Coin] = 0.15f,
                [ChunkType.Powerup] = 0.1f,
                [ChunkType.Hazard] = 0.05f
            };

            // 滑铲 -> 跳跃/攻击/直道
            _transitionMatrix[ChunkType.Slide] = new Dictionary<ChunkType, float>
            {
                [ChunkType.Straight] = 0.3f,
                [ChunkType.Jump] = 0.25f,
                [ChunkType.Attack] = 0.15f,
                [ChunkType.Coin] = 0.15f,
                [ChunkType.Powerup] = 0.1f,
                [ChunkType.Hazard] = 0.05f
            };

            // 攻击 -> 直道/金币/道具
            _transitionMatrix[ChunkType.Attack] = new Dictionary<ChunkType, float>
            {
                [ChunkType.Straight] = 0.35f,
                [ChunkType.Coin] = 0.2f,
                [ChunkType.Powerup] = 0.15f,
                [ChunkType.Jump] = 0.1f,
                [ChunkType.Slide] = 0.1f,
                [ChunkType.Hazard] = 0.1f
            };

            // 金币 -> 直道/道具/跳跃
            _transitionMatrix[ChunkType.Coin] = new Dictionary<ChunkType, float>
            {
                [ChunkType.Straight] = 0.3f,
                [ChunkType.Powerup] = 0.2f,
                [ChunkType.Jump] = 0.15f,
                [ChunkType.Slide] = 0.15f,
                [ChunkType.Attack] = 0.1f,
                [ChunkType.Hazard] = 0.1f
            };

            // 道具 -> 直道/跳跃/滑铲
            _transitionMatrix[ChunkType.Powerup] = new Dictionary<ChunkType, float>
            {
                [ChunkType.Straight] = 0.35f,
                [ChunkType.Jump] = 0.2f,
                [ChunkType.Slide] = 0.15f,
                [ChunkType.Coin] = 0.15f,
                [ChunkType.Attack] = 0.1f,
                [ChunkType.Hazard] = 0.05f
            };

            // 危险 -> 直道/金币/道具 (喘息)
            _transitionMatrix[ChunkType.Hazard] = new Dictionary<ChunkType, float>
            {
                [ChunkType.Straight] = 0.4f,
                [ChunkType.Coin] = 0.25f,
                [ChunkType.Powerup] = 0.15f,
                [ChunkType.Jump] = 0.1f,
                [ChunkType.Slide] = 0.1f
            };

            // 混合 -> 任何
            _transitionMatrix[ChunkType.Mixed] = new Dictionary<ChunkType, float>
            {
                [ChunkType.Straight] = 0.25f,
                [ChunkType.Jump] = 0.15f,
                [ChunkType.Slide] = 0.15f,
                [ChunkType.Attack] = 0.15f,
                [ChunkType.Coin] = 0.1f,
                [ChunkType.Powerup] = 0.1f,
                [ChunkType.Hazard] = 0.1f
            };

            // Boss -> 直道 (结束)
            _transitionMatrix[ChunkType.Boss] = new Dictionary<ChunkType, float>
            {
                [ChunkType.Straight] = 1f
            };
        }

        // ===================== 编辑器工具 =====================

#if UNITY_EDITOR
        [UnityEditor.MenuItem("TomCatRunner/Generate Test Level")]
        public static async void GenerateTestLevel()
        {
            var generator = Instance;
            var theme = CreateTestTheme();
            var curve = ScriptableObject.CreateInstance<DifficultyCurve>();

            var level = await generator.GenerateLevelAsync(theme, curve, 12345, 10);

            string path = $"Assets/_Project/Seasons/Data/GeneratedLevels/{level.levelId}.json";
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            System.IO.File.WriteAllText(path, JsonUtility.ToJson(level, true));
            UnityEditor.AssetDatabase.Refresh();

            Debug.Log($"[ProceduralLevelGenerator] Test level saved to: {path}");
        }

        private static ThemeProfile CreateTestTheme()
        {
            var theme = ScriptableObject.CreateInstance<ThemeProfile>();
            theme.themeId = "Test_NeonCyber";
            theme.displayName = "Test Neon Cyber";
            theme.baseSegmentLength = 300f;
            theme.segmentsPerLevel = 10;

            theme.chunkDefinitions = new ChunkDefinition[]
            {
                new ChunkDefinition { id = "straight_01", type = ChunkType.Straight, length = 30f, weight = 100, minDifficulty = DifficultyTier.Easy, maxDifficulty = DifficultyTier.Expert },
                new ChunkDefinition { id = "jump_01", type = ChunkType.Jump, length = 25f, weight = 80, minDifficulty = DifficultyTier.Easy, maxDifficulty = DifficultyTier.Expert },
                new ChunkDefinition { id = "slide_01", type = ChunkType.Slide, length = 25f, weight = 80, minDifficulty = DifficultyTier.Easy, maxDifficulty = DifficultyTier.Expert },
                new ChunkDefinition { id = "attack_01", type = ChunkType.Attack, length = 30f, weight = 60, minDifficulty = DifficultyTier.Normal, maxDifficulty = DifficultyTier.Expert },
                new ChunkDefinition { id = "coin_01", type = ChunkType.Coin, length = 20f, weight = 90, minDifficulty = DifficultyTier.Easy, maxDifficulty = DifficultyTier.Expert },
                new ChunkDefinition { id = "powerup_01", type = ChunkType.Powerup, length = 15f, weight = 50, minDifficulty = DifficultyTier.Easy, maxDifficulty = DifficultyTier.Expert },
                new ChunkDefinition { id = "hazard_01", type = ChunkType.Hazard, length = 30f, weight = 40, minDifficulty = DifficultyTier.Normal, maxDifficulty = DifficultyTier.Expert },
                new ChunkDefinition { id = "mixed_01", type = ChunkType.Mixed, length = 40f, weight = 30, minDifficulty = DifficultyTier.Hard, maxDifficulty = DifficultyTier.Expert },
                new ChunkDefinition { id = "boss_01", type = ChunkType.Boss, length = 100f, weight = 10, minDifficulty = DifficultyTier.Expert, maxDifficulty = DifficultyTier.Expert, minSegmentIndex = 8 }
            };

            theme.chunkWeightCurve = AnimationCurve.Linear(0, 1, 1, 1);
            theme.obstacleDensityCurve = AnimationCurve.Linear(0, 0.3f, 1, 0.8f);
            theme.rewardDensityCurve = AnimationCurve.Linear(0, 0.8f, 1, 0.3f);
            theme.speedMultiplierCurve = AnimationCurve.Linear(0, 1f, 1, 2f);

            return theme;
        }
#endif
    }

    /// <summary>
    /// 语法规则
    /// </summary>
    public class GrammarRule
    {
        public string name;
        public Func<ChunkType, DifficultyTier, float, bool> Condition;
        public Action<Dictionary<ChunkType, float>> Apply;
    }

    // ===================== 扩展方法 =====================

    public static class ChunkTypeExtensions
    {
        public static ChunkType GetChunkType(this string chunkId)
        {
            if (chunkId.Contains("straight")) return ChunkType.Straight;
            if (chunkId.Contains("jump")) return ChunkType.Jump;
            if (chunkId.Contains("slide")) return ChunkType.Slide;
            if (chunkId.Contains("attack")) return ChunkType.Attack;
            if (chunkId.Contains("coin")) return ChunkType.Coin;
            if (chunkId.Contains("powerup")) return ChunkType.Powerup;
            if (chunkId.Contains("hazard")) return ChunkType.Hazard;
            if (chunkId.Contains("mixed")) return ChunkType.Mixed;
            if (chunkId.Contains("boss")) return ChunkType.Boss;
            return ChunkType.Straight;
        }
    }
}