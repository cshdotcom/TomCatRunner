using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using TomCatRunner.Common.Reactive;
using TomCatRunner.Core.Data;
using TomCatRunner.Core.Level;

namespace TomCatRunner.Core.Generation
{
    /// <summary>
    /// 程序化关卡生成器 - 基于 GrammarConfig 语法树 + 马尔可夫链生成 ChunkData 流
    /// 供 LevelStreamer 按距离增量消费
    /// </summary>
    public class ProceduralLevelGenerator : IInitializable, IDisposable
    {
        private readonly GrammarConfig _grammarConfig;
        private readonly MessageBroker _messageBroker;

        private readonly List<ChunkData> _generatedChunks = new List<ChunkData>();
        private readonly Queue<ChunkData> _pendingGeneration = new Queue<ChunkData>();

        private System.Random _random;
        private ChunkType? _lastChunkType;
        private float _nextChunkStartZ;
        private float _totalLength;
        private int _seed;

        public float TotalLevelLength => _totalLength;

        [Inject]
        public ProceduralLevelGenerator(GrammarConfig grammarConfig, MessageBroker messageBroker)
        {
            _grammarConfig = grammarConfig;
            _messageBroker = messageBroker;
        }

        public void Initialize()
        {
            _seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            _random = new System.Random(_seed);
            _generatedChunks.Clear();
            _pendingGeneration.Clear();
            _nextChunkStartZ = 0f;
            _totalLength = 0f;
            _lastChunkType = null;

            Debug.Log($"[ProceduralLevelGenerator] Initialized with grammar '{_grammarConfig?.GrammarId ?? "null"}'");
        }

        public void Dispose()
        {
            _generatedChunks.Clear();
            _pendingGeneration.Clear();
        }

        /// <summary>
        /// 依据赛季配置重置生成器并预生成初始段落
        /// </summary>
        public async UniTask GenerateLevelData(SeasonConfig season)
        {
            Initialize();

            if (season?.DifficultyCurve != null)
            {
                // 难度曲线影响后续 chunk 的难度权重，这里先预生成一段初始数据
                for (int i = 0; i < 8; i++)
                {
                    await GenerateNextChunk();
                }
            }
            else
            {
                for (int i = 0; i < 8; i++)
                {
                    await GenerateNextChunk();
                }
            }

            _messageBroker.Publish(new LevelGenerationStartedMessage(_seed));
        }

        /// <summary>
        /// 生成下一个 Chunk（语法推导 + 马尔可夫权重）
        /// </summary>
        public async UniTask GenerateNextChunk()
        {
            if (_grammarConfig == null)
            {
                // 无配置时输出默认过渡段，保证流式加载不中断
                AppendChunk(ChunkType.Transition, 50f);
                await UniTask.Yield();
                return;
            }

            ChunkType nextType = PickNextChunkType();
            float length = GetChunkLength(nextType);

            var chunk = AppendChunk(nextType, length);
            chunk.Difficulty01 = Mathf.Lerp(0.15f, 0.9f, Mathf.Clamp01(_nextChunkStartZ / 5000f));

            _messageBroker.Publish(new ChunkGeneratedMessage(chunk));

            // 模拟异步资源准备（保持 API 为异步，方便接入 Addressables）
            await UniTask.Yield();
        }

        /// <summary>
        /// 已生成 Chunk 的末尾 Z（用于判断是否需要继续生成）
        /// </summary>
        public float GetNextChunkEndZ()
        {
            return _nextChunkStartZ;
        }

        /// <summary>
        /// 获取区间内的 Chunk 数据（按 StartZ 升序）
        /// </summary>
        public IReadOnlyList<ChunkData> GetChunksInRange(float minZ, float maxZ)
        {
            var result = new List<ChunkData>();
            foreach (var chunk in _generatedChunks)
            {
                if (chunk.EndZ < minZ) continue;
                if (chunk.StartZ > maxZ) break;
                result.Add(chunk);
            }
            return result;
        }

        /// <summary>
        /// 获取尚未被流式加载消费的下一个 Chunk 数据
        /// </summary>
        public ChunkData GetNextChunkData()
        {
            return _pendingGeneration.Peek();
        }

        /// <summary>
        /// 标记一个 Chunk 已被流式加载消费（从待处理队列移除）
        /// </summary>
        public void ConsumeChunk(ChunkData data)
        {
            if (_pendingGeneration.Count > 0 && _pendingGeneration.Peek() == data)
            {
                _pendingGeneration.Dequeue();
            }
        }

        public IReadOnlyList<ChunkData> GeneratedChunks => _generatedChunks;

        #region Private Methods

        private ChunkData AppendChunk(ChunkType type, float length)
        {
            var chunk = new ChunkData(type, _nextChunkStartZ, length);
            _generatedChunks.Add(chunk);
            _pendingGeneration.Enqueue(chunk);
            _nextChunkStartZ += length;
            _totalLength += length;
            _lastChunkType = type;
            return chunk;
        }

        private ChunkType PickNextChunkType()
        {
            // 开局强制过渡段
            if (_lastChunkType == null) return ChunkType.Transition;

            var weights = _grammarConfig.GetNextChunkWeights(_lastChunkType.Value);
            if (weights == null || weights.Length == 0)
            {
                return ChunkType.Transition;
            }

            // 必选 Chunk 检查（语法约束）
            foreach (var required in _grammarConfig.RequiredChunks)
            {
                bool alreadyGenerated = _generatedChunks.Any(c => c.Type == required);
                if (!alreadyGenerated && _generatedChunks.Count > 4)
                {
                    return required;
                }
            }

            float total = weights.Sum(w => w.weight);
            if (total <= 0f) return ChunkType.Transition;

            float pick = (float)_random.NextDouble() * total;
            foreach (var (type, weight) in weights)
            {
                pick -= weight;
                if (pick <= 0f) return type;
            }

            return weights[weights.Length - 1].type;
        }

        private float GetChunkLength(ChunkType type)
        {
            var def = _grammarConfig.GetChunkDefinition(type);
            if (def != null && def.AverageLength > 0f)
            {
                // MinLength/MaxLength 以 10 米为一个段位换算成米
                return def.AverageLength * 10f;
            }

            return type switch
            {
                ChunkType.Boss => 120f,
                ChunkType.Jump => 45f,
                ChunkType.Slide => 40f,
                ChunkType.Attack => 50f,
                ChunkType.Coin => 30f,
                ChunkType.Transition => 25f,
                ChunkType.Secret => 35f,
                _ => 50f
            };
        }

        #endregion

        #region Messages

        public readonly struct LevelGenerationStartedMessage
        {
            public readonly int Seed;
            public LevelGenerationStartedMessage(int seed) => Seed = seed;
        }

        public readonly struct ChunkGeneratedMessage
        {
            public readonly ChunkData Chunk;
            public ChunkGeneratedMessage(ChunkData chunk) => Chunk = chunk;
        }

        #endregion
    }
}
