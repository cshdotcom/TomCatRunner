using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using VContainer;
using TomCatRunner.Common.Reactive;

namespace TomCatRunner.Core.Level
{
    /// <summary>
    /// 关卡流式加载器 - 基于距离的增量实例化/回收
    /// 管理 Chunk 池、视野范围内的激活 Chunk、预加载
    /// </summary>
    public class LevelStreamer : IInitializable, ITickable, IDisposable, IPausable
    {
        public event Action<ChunkInstance> OnChunkActivated;
        public event Action<ChunkInstance> OnChunkDeactivated;
        public event Action<float> OnStreamingProgress; // 用于加载界面

        private readonly ChunkPool _chunkPool;
        private readonly ProceduralLevelGenerator _generator;
        private readonly SeasonManager _seasonManager;
        private readonly MessageBroker _messageBroker;

        [Header("Streaming Settings")]
        [SerializeField] private float _activationDistance = 150f;   // 激活距离 (玩家前方)
        [SerializeField] private float _deactivationDistance = 50f;  // 回收距离 (玩家后方)
        [SerializeField] private float _preloadDistance = 300f;      // 预加载距离
        [SerializeField] private int _maxActiveChunks = 10;          // 最大同时激活 Chunk
        [SerializeField] private int _poolSize = 50;                 // 对象池大小
        [SerializeField] private bool _enableFrustumCulling = true;  // 视锥剔除

        private Transform _playerTransform;
        private readonly List<ChunkInstance> _activeChunks = new();
        private readonly Queue<ChunkInstance> _pendingActivation = new();
        private float _lastPlayerZ;
        private bool _isStreaming;
        private bool _isPaused;
        private CancellationTokenSource _cts;

        [Inject]
        public LevelStreamer(ChunkPool chunkPool, ProceduralLevelGenerator generator,
                           SeasonManager seasonManager, MessageBroker messageBroker)
        {
            _chunkPool = chunkPool;
            _generator = generator;
            _seasonManager = seasonManager;
            _messageBroker = messageBroker;
        }

        public void Initialize()
        {
            _cts = new CancellationTokenSource();

            // 找到玩家
            var player = GameObject.FindWithTag("Player");
            if (player != null)
                _playerTransform = player.transform;

            // 预热对象池
            _chunkPool.Warmup(_poolSize);

            // 生成初始关卡数据
            _generator.GenerateLevelData(_seasonManager.CurrentSeason).Forget();

            _lastPlayerZ = _playerTransform?.position.z ?? 0f;
            _isStreaming = true;

            Debug.Log($"[LevelStreamer] Initialized. Pool: {_poolSize}, MaxActive: {_maxActiveChunks}");
        }

        public void Tick()
        {
            if (_isPaused || !_isStreaming || _playerTransform == null) return;

            float playerZ = _playerTransform.position.z;

            // 只在玩家前进时处理流式加载
            if (playerZ > _lastPlayerZ + 1f)
            {
                UpdateStreaming(playerZ);
                _lastPlayerZ = playerZ;
            }
        }

        public void Pause(bool pause)
        {
            _isPaused = pause;
        }

        public void Dispose()
        {
            _isStreaming = false;
            _cts?.Cancel();
            _cts?.Dispose();

            // 回收所有激活 Chunk
            foreach (var chunk in _activeChunks)
            {
                _chunkPool.Return(chunk);
            }
            _activeChunks.Clear();
            _pendingActivation.Clear();
        }

        /// <summary>
        /// 设置玩家引用 (场景切换后调用)
        /// </summary>
        public void SetPlayer(Transform player)
        {
            _playerTransform = player;
            _lastPlayerZ = player?.position.z ?? 0f;
        }

        /// <summary>
        /// 强制刷新流式状态
        /// </summary>
        public void Refresh()
        {
            if (_playerTransform != null)
                UpdateStreaming(_playerTransform.position.z);
        }

        #region Private Methods

        private void UpdateStreaming(float playerZ)
        {
            // 1. 回收超出范围的 Chunk
            DeactivateBehindChunks(playerZ);

            // 2. 激活前方新 Chunk
            ActivateAheadChunks(playerZ);

            // 3. 处理预加载队列
            ProcessPreloadQueue(playerZ);

            // 4. 更新进度 (用于 LoadingScreen)
            float progress = Mathf.Clamp01(playerZ / _generator.TotalLevelLength);
            OnStreamingProgress?.Invoke(progress);
        }

        private void DeactivateBehindChunks(float playerZ)
        {
            float deactivateZ = playerZ - _deactivationDistance;

            for (int i = _activeChunks.Count - 1; i >= 0; i--)
            {
                var chunk = _activeChunks[i];
                if (chunk.EndZ < deactivateZ)
                {
                    _activeChunks.RemoveAt(i);
                    _chunkPool.Return(chunk);
                    OnChunkDeactivated?.Invoke(chunk);

                    _messageBroker.Publish(new ChunkDeactivatedMessage(chunk.Data.Type, chunk.EndZ));
                }
            }
        }

        private void ActivateAheadChunks(float playerZ)
        {
            float activateZ = playerZ + _activationDistance;

            // 确保有足够的 Chunk 数据
            while (_generator.GetNextChunkEndZ() < activateZ + _preloadDistance)
            {
                _generator.GenerateNextChunk().Forget();
            }

            // 激活范围内的 Chunk
            var nextChunks = _generator.GetChunksInRange(playerZ - _deactivationDistance, activateZ + _preloadDistance);

            foreach (var chunkData in nextChunks)
            {
                if (_activeChunks.Count >= _maxActiveChunks)
                    break;

                // 检查是否已经激活
                if (_activeChunks.Exists(c => c.Data == chunkData))
                    continue;

                // 从池获取或实例化
                var instance = _chunkPool.Get(chunkData);
                if (instance != null)
                {
                    instance.Activate();
                    _activeChunks.Add(instance);
                    OnChunkActivated?.Invoke(instance);

                    _messageBroker.Publish(new ChunkActivatedMessage(chunkData.Type, chunkData.StartZ));
                }
            }

            // 按 Z 排序
            _activeChunks.Sort((a, b) => a.Data.StartZ.CompareTo(b.Data.StartZ));
        }

        private void ProcessPreloadQueue(float playerZ)
        {
            float preloadZ = playerZ + _preloadDistance;

            // 预加载下一个 Chunk 的资源
            var nextChunkData = _generator.GetNextChunkData();
            if (nextChunkData != null && nextChunkData.StartZ < preloadZ)
            {
                _chunkPool.Preload(nextChunkData);
            }
        }

        #endregion

        #region Messages

        public readonly struct ChunkActivatedMessage
        {
            public readonly ChunkType Type;
            public readonly float PositionZ;
            public ChunkActivatedMessage(ChunkType type, float z) { Type = type; PositionZ = z; }
        }

        public readonly struct ChunkDeactivatedMessage
        {
            public readonly ChunkType Type;
            public readonly float PositionZ;
            public ChunkDeactivatedMessage(ChunkType type, float z) { Type = type; PositionZ = z; }
        }

        #endregion
    }

    /// <summary>
    /// Chunk 实例包装器
    /// </summary>
    public class ChunkInstance : IDisposable
    {
        public ChunkData Data { get; private set; }
        public GameObject GameObject { get; private set; }
        public bool IsActive { get; private set; }
        public float StartZ => Data?.StartZ ?? 0f;
        public float EndZ => Data?.EndZ ?? 0f;

        private readonly List<PoolableComponent> _poolables = new();

        public void Initialize(ChunkData data, GameObject go)
        {
            Data = data;
            GameObject = go;
            IsActive = false;

            // 缓存所有可池化组件
            _poolables.Clear();
            _poolables.AddRange(go.GetComponentsInChildren<PoolableComponent>());
        }

        public void Activate()
        {
            if (IsActive) return;
            IsActive = true;
            GameObject.SetActive(true);

            foreach (var p in _poolables)
                p.OnActivate();
        }

        public void Deactivate()
        {
            if (!IsActive) return;
            IsActive = false;

            foreach (var p in _poolables)
                p.OnDeactivate();

            GameObject.SetActive(false);
        }

        public void Dispose()
        {
            if (GameObject != null)
            {
                UnityEngine.Object.Destroy(GameObject);
                GameObject = null;
            }
            _poolables.Clear();
            Data = null;
        }
    }

    /// <summary>
    /// 可池化组件接口
    /// </summary>
    public interface IPoolable
    {
        void OnActivate();
        void OnDeactivate();
        void OnSpawn();
        void OnDespawn();
    }

    /// <summary>
    /// 可池化组件基类
    /// </summary>
    public abstract class PoolableComponent : MonoBehaviour, IPoolable
    {
        public virtual void OnActivate() { }
        public virtual void OnDeactivate() { }
        public virtual void OnSpawn() { }
        public virtual void OnDespawn() { }
    }
}