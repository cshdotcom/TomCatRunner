using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;
using VContainer;
using TomCatRunner.Core.Data;

namespace TomCatRunner.Core.Level
{
    /// <summary>
    /// Chunk 对象池 - 管理 Chunk 实例的获取、回收、预加载
    /// 支持 Addressables 异步加载、预热、内存压力清理
    /// </summary>
    public class ChunkPool : IInitializable, IDisposable
    {
        public event Action<ChunkData> OnChunkPreloaded;

        private readonly GrammarConfig _grammarConfig;
        private readonly Dictionary<ChunkType, Queue<ChunkInstance>> _pools = new();
        private readonly Dictionary<ChunkType, GameObject> _prefabs = new();
        private readonly Dictionary<ChunkType, AsyncOperationHandle<GameObject>> _addressableHandles = new();
        private readonly Dictionary<ChunkData, ChunkInstance> _activeInstances = new();
        private readonly HashSet<ChunkData> _preloading = new();

        private Transform _poolRoot;
        private int _defaultPoolSize = 5;
        private int _maxPoolSize = 20;
        private bool _useAddressables = true;

        [Inject]
        public ChunkPool(GrammarConfig grammarConfig)
        {
            _grammarConfig = grammarConfig;
        }

        public void Initialize()
        {
            _poolRoot = new GameObject("ChunkPool").transform;
            _poolRoot.hideFlags = HideFlags.HideInHierarchy;

            // 预加载所有 Chunk Prefab
            LoadAllPrefabs().Forget();
        }

        public void Dispose()
        {
            // 释放所有池对象
            foreach (var kvp in _pools)
            {
                while (kvp.Value.Count > 0)
                {
                    var instance = kvp.Value.Dequeue();
                    instance?.Dispose();
                }
            }
            _pools.Clear();

            // 释放 Addressables 句柄
            foreach (var handle in _addressableHandles.Values)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
            }
            _addressableHandles.Clear();

            // 销毁池根节点
            if (_poolRoot != null)
                UnityEngine.Object.Destroy(_poolRoot.gameObject);

            _prefabs.Clear();
            _activeInstances.Clear();
            _preloading.Clear();
        }

        /// <summary>
        /// 预热对象池
        /// </summary>
        public void Warmup(int poolSize)
        {
            _defaultPoolSize = Mathf.Clamp(poolSize, 1, _maxPoolSize);

            foreach (var chunkDef in _grammarConfig.ChunkDefinitions)
            {
                EnsurePool(chunkDef.Type, _defaultPoolSize);
            }
        }

        /// <summary>
        /// 获取 Chunk 实例
        /// </summary>
        public ChunkInstance Get(ChunkData data)
        {
            if (!_pools.TryGetValue(data.Type, out var pool) || pool.Count == 0)
            {
                // 池为空，尝试实例化新对象
                return CreateNewInstance(data);
            }

            var instance = pool.Dequeue();
            instance.Initialize(data, instance.GameObject);
            _activeInstances[data] = instance;
            return instance;
        }

        /// <summary>
        /// 归还 Chunk 实例到池
        /// </summary>
        public void Return(ChunkInstance instance)
        {
            if (instance == null || instance.Data == null) return;

            _activeInstances.Remove(instance.Data);
            instance.Deactivate();

            if (!_pools.TryGetValue(instance.Data.Type, out var pool))
            {
                pool = new Queue<ChunkInstance>();
                _pools[instance.Data.Type] = pool;
            }

            // 池满则销毁
            if (pool.Count >= _maxPoolSize)
            {
                instance.Dispose();
            }
            else
            {
                pool.Enqueue(instance);
            }
        }

        /// <summary>
        /// 预加载 Chunk 资源
        /// </summary>
        public async UniTask Preload(ChunkData data)
        {
            if (_preloading.Contains(data)) return;
            _preloading.Add(data);

            try
            {
                // 确保 Prefab 已加载
                await EnsurePrefabLoaded(data.Type);

                OnChunkPreloaded?.Invoke(data);
            }
            finally
            {
                _preloading.Remove(data);
            }
        }

        /// <summary>
        /// 获取当前池统计信息
        /// </summary>
        public PoolStats GetStats()
        {
            int totalPooled = 0, totalActive = _activeInstances.Count;
            foreach (var kvp in _pools) totalPooled += kvp.Value.Count;

            return new PoolStats
            {
                TotalPooled = totalPooled,
                TotalActive = totalActive,
                PoolCount = _pools.Count,
                PrefabCount = _prefabs.Count
            };
        }

        #region Private Methods

        private async UniTask LoadAllPrefabs()
        {
            var tasks = new List<UniTask>();
            foreach (var chunkDef in _grammarConfig.ChunkDefinitions)
            {
                tasks.Add(EnsurePrefabLoaded(chunkDef.Type));
            }
            await UniTask.WhenAll(tasks);
            Debug.Log($"[ChunkPool] All prefabs loaded: {_prefabs.Count}");
        }

        private async UniTask<GameObject> EnsurePrefabLoaded(ChunkType type)
        {
            if (_prefabs.TryGetValue(type, out var prefab) && prefab != null)
                return prefab;

            var chunkDef = _grammarConfig.GetChunkDefinition(type);
            if (chunkDef == null)
            {
                Debug.LogError($"[ChunkPool] No definition for chunk type: {type}");
                return null;
            }

            GameObject loaded = null;

            // 优先 Addressables
            if (_useAddressables && !string.IsNullOrEmpty(chunkDef.AddressableKey))
            {
                try
                {
                    var handle = Addressables.LoadAssetAsync<GameObject>(chunkDef.AddressableKey);
                    _addressableHandles[type] = handle;
                    loaded = await handle.Task;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ChunkPool] Addressables load failed for {type}: {e.Message}");
                }
            }

            // 回退到直接引用
            if (loaded == null && chunkDef.PrefabRef != null)
            {
                loaded = chunkDef.PrefabRef;
            }

            // 最后尝试 Resources
            if (loaded == null)
            {
                loaded = Resources.Load<GameObject>($"Chunks/{type}");
            }

            if (loaded == null)
            {
                // 创建默认白模
                loaded = CreateDefaultChunkPrefab(type);
                Debug.LogWarning($"[ChunkPool] Using default primitive for {type}");
            }

            _prefabs[type] = loaded;
            return loaded;
        }

        private GameObject CreateDefaultChunkPrefab(ChunkType type)
        {
            var go = new GameObject($"Chunk_{type}_Default");
            var renderer = go.AddComponent<MeshRenderer>();
            var filter = go.AddComponent<MeshFilter>();

            // 根据类型创建不同形状的白模
            PrimitiveType primitive = type switch
            {
                ChunkType.Jump => PrimitiveType.Cube,
                ChunkType.Slide => PrimitiveType.Cube,
                ChunkType.Attack => PrimitiveType.Cylinder,
                ChunkType.Coin => PrimitiveType.Sphere,
                ChunkType.Mixed => PrimitiveType.Cube,
                ChunkType.Hazard => PrimitiveType.Cube,
                ChunkType.Boss => PrimitiveType.Cube,
                ChunkType.Transition => PrimitiveType.Cube,
                ChunkType.Secret => PrimitiveType.Sphere,
                ChunkType.Powerup => PrimitiveType.Capsule,
                _ => PrimitiveType.Cube
            };

            filter.mesh = Resources.GetBuiltinResource<Mesh>($"{primitive}.mesh");

            // 设置颜色标识
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = GetTypeColor(type);
            renderer.material = mat;

            // 添加 ChunkMetadata
            var metadata = go.AddComponent<ChunkMetadata>();
            metadata.Type = type;

            return go;
        }

        private Color GetTypeColor(ChunkType type)
        {
            return type switch
            {
                ChunkType.Jump => new Color(0.2f, 0.8f, 1f),      // 青色
                ChunkType.Slide => new Color(1f, 0.6f, 0.2f),     // 橙色
                ChunkType.Attack => new Color(1f, 0.2f, 0.4f),    // 红色
                ChunkType.Coin => new Color(1f, 0.9f, 0.1f),      // 金色
                ChunkType.Mixed => new Color(0.6f, 0.3f, 0.9f),   // 紫色
                ChunkType.Hazard => new Color(0.8f, 0.1f, 0.1f),  // 深红
                ChunkType.Boss => new Color(0.5f, 0f, 0.8f),      // 深紫
                ChunkType.Transition => new Color(0.5f, 0.5f, 0.5f), // 灰色
                ChunkType.Secret => new Color(0f, 1f, 0.5f),      // 绿色
                ChunkType.Powerup => new Color(1f, 0.4f, 0.8f),   // 粉色
                _ => Color.white
            };
        }

        private ChunkInstance CreateNewInstance(ChunkData data)
        {
            var prefab = _prefabs.GetValueOrDefault(data.Type);
            if (prefab == null)
            {
                // 同步加载 (不推荐但在兜底时使用)
                EnsurePrefabLoaded(data.Type).Forget();
                prefab = _prefabs.GetValueOrDefault(data.Type);
            }

            if (prefab == null) return null;

            var go = UnityEngine.Object.Instantiate(prefab, _poolRoot);
            go.name = $"Chunk_{data.Type}_{data.StartZ:F0}";

            var instance = new ChunkInstance();
            instance.Initialize(data, go);
            _activeInstances[data] = instance;

            return instance;
        }

        private void EnsurePool(ChunkType type, int count)
        {
            if (!_pools.ContainsKey(type))
                _pools[type] = new Queue<ChunkInstance>();

            var pool = _pools[type];
            var prefab = _prefabs.GetValueOrDefault(type);

            if (prefab == null) return;

            for (int i = 0; i < count; i++)
            {
                if (pool.Count >= _maxPoolSize) break;

                var go = UnityEngine.Object.Instantiate(prefab, _poolRoot);
                go.SetActive(false);
                go.name = $"Chunk_{type}_Pooled_{i}";

                var instance = new ChunkInstance();
                // 临时数据用于初始化
                var tempData = new ChunkData { Type = type, StartZ = 0, EndZ = 100 };
                instance.Initialize(tempData, go);
                pool.Enqueue(instance);
            }
        }

        #endregion

        [Serializable]
        public struct PoolStats
        {
            public int TotalPooled;
            public int TotalActive;
            public int PoolCount;
            public int PrefabCount;
        }
    }

    /// <summary>
    /// Chunk 元数据组件 - 挂在 Chunk 预制体上
    /// </summary>
    public class ChunkMetadata : MonoBehaviour
    {
        public ChunkType Type;
        public int SegmentIndex;
        public string GrammarProduction; // 生成该 Chunk 的产生式
        public DifficultyParams DifficultyParams;

        private void OnDrawGizmos()
        {
            // 编辑器下显示 Chunk 类型
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 3, $"{Type}\nSeg:{SegmentIndex}");
#endif
        }
    }
}