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
    /// 关卡流式加载器 - 基于玩家距离动态实例化/回收关卡段落
    /// </summary>
    public class LevelStreamer : MonoSingleton<LevelStreamer>
    {
        [Header("流式加载设置")]
        [SerializeField] private float _preloadDistance = 500f;      // 预加载距离(米)
        [SerializeField] private float _unloadDistance = 700f;       // 卸载距离(米)
        [SerializeField] private int _maxActiveSegments = 5;         // 最大同时激活段落数
        [SerializeField] private float _updateInterval = 0.5f;       // 检查间隔(秒)

        [Header("对象池设置")]
        [SerializeField] private int _segmentPoolSize = 10;
        [SerializeField] private int _chunkPoolSize = 30;

        [Header("调试")]
        [SerializeField] private bool _debugLog = false;
        [SerializeField] private bool _showGizmos = true;

        // 状态
        private LevelData _currentLevelData;
        private Transform _playerTransform;
        private float _playerDistance = 0f;
        private readonly Dictionary<int, SegmentInstance> _activeSegments = new Dictionary<int, SegmentInstance>();
        private readonly Queue<SegmentInstance> _segmentPool = new Queue<SegmentInstance>();
        private readonly Dictionary<string, GameObjectPool> _chunkPools = new Dictionary<string, GameObjectPool>();
        private Coroutine _streamCoroutine;
        private bool _isStreaming = false;

        // 事件
        public event Action<SegmentInstance> OnSegmentActivated;
        public event Action<SegmentInstance> OnSegmentDeactivated;
        public event Action<int> OnCheckpointReached;
        public event Action OnLevelCompleted;

        // 属性
        public LevelData CurrentLevelData => _currentLevelData;
        public float PlayerDistance => _playerDistance;
        public IReadOnlyDictionary<int, SegmentInstance> ActiveSegments => _activeSegments;
        public bool IsStreaming => _isStreaming;

        protected override void OnInit()
        {
            base.OnInit();
        }

        /// <summary>
        /// 开始流式加载关卡
        /// </summary>
        public async UniTask StartStreamingAsync(LevelData levelData, Transform playerTransform)
        {
            if (_isStreaming)
            {
                Debug.LogWarning("[LevelStreamer] Already streaming, stopping previous first");
                StopStreaming();
            }

            _currentLevelData = levelData;
            _playerTransform = playerTransform;
            _playerDistance = 0f;
            _isStreaming = true;

            // 预热对象池
            await WarmupPoolsAsync();

            // 启动流式协程
            _streamCoroutine = StartCoroutine(StreamingRoutine());

            if (_debugLog) Debug.Log($"[LevelStreamer] Started streaming level: {levelData.levelId}");
        }

        /// <summary>
        /// 停止流式加载
        /// </summary>
        public void StopStreaming()
        {
            _isStreaming = false;

            if (_streamCoroutine != null)
            {
                StopCoroutine(_streamCoroutine);
                _streamCoroutine = null;
            }

            // 回收所有激活段落
            foreach (var segment in _activeSegments.Values.ToList())
            {
                ReturnSegmentToPool(segment);
            }
            _activeSegments.Clear();

            _currentLevelData = null;
            _playerTransform = null;

            if (_debugLog) Debug.Log("[LevelStreamer] Stopped streaming");
        }

        /// <summary>
        /// 更新玩家距离 (每帧调用或由外部驱动)
        /// </summary>
        public void UpdatePlayerDistance(float distance)
        {
            _playerDistance = distance;
        }

        private async UniTask WarmupPoolsAsync()
        {
            if (_currentLevelData?.segments == null) return;

            // 收集所有需要的 Chunk 预制体
            var chunkPrefabs = new Dictionary<string, GameObject>();

            foreach (var segment in _currentLevelData.segments)
            {
                foreach (var chunkInstance in segment.chunks)
                {
                    // 这里需要从 ThemeProfile 获取 ChunkDefinition
                    // 简化: 假设 ChunkDefinition 已在 ThemeProfile 中可通过 ID 查找
                    // 实际项目中应通过 Addressables 异步加载
                }
            }

            // 预热段落池
            for (int i = 0; i < _segmentPoolSize; i++)
            {
                var segmentObj = new GameObject($"PooledSegment_{i}");
                segmentObj.SetActive(false);
                segmentObj.transform.SetParent(transform);
                var instance = segmentObj.AddComponent<SegmentInstance>();
                _segmentPool.Enqueue(instance);
            }

            await UniTask.Yield();
        }

        private IEnumerator StreamingRoutine()
        {
            var wait = new WaitForSeconds(_updateInterval);

            while (_isStreaming)
            {
                yield return wait;

                if (_currentLevelData == null || _playerTransform == null) continue;

                UpdateStreaming();
            }
        }

        private void UpdateStreaming()
        {
            if (_currentLevelData?.segments == null) return;

            // 1. 激活需要的段落
            ActivateNeededSegments();

            // 2. 回收过远的段落
            DeactivateDistantSegments();

            // 3. 检查关卡完成
            CheckLevelCompletion();
        }

        private void ActivateNeededSegments()
        {
            float activateThreshold = _playerDistance + _preloadDistance;

            for (int i = 0; i < _currentLevelData.segments.Length; i++)
            {
                if (_activeSegments.ContainsKey(i)) continue;
                if (_activeSegments.Count >= _maxActiveSegments) break;

                var segmentDef = _currentLevelData.segments[i];
                float segmentStart = segmentDef.startPosition.z; // 假设 Z 轴为前进方向
                float segmentEnd = segmentDef.endPosition.z;

                // 检查是否在预加载范围内
                if (segmentStart <= activateThreshold && segmentEnd >= _playerDistance - _unloadDistance)
                {
                    ActivateSegment(i, segmentDef);
                }
            }
        }

        private void ActivateSegment(int index, SegmentDefinition segmentDef)
        {
            SegmentInstance instance;

            if (_segmentPool.Count > 0)
            {
                instance = _segmentPool.Dequeue();
                instance.gameObject.SetActive(true);
            }
            else
            {
                var obj = new GameObject($"Segment_{index}");
                obj.transform.SetParent(transform);
                instance = obj.AddComponent<SegmentInstance>();
            }

            instance.Initialize(index, segmentDef, this);
            instance.transform.position = segmentDef.startPosition;

            _activeSegments[index] = instance;
            OnSegmentActivated?.Invoke(instance);

            if (_debugLog) Debug.Log($"[LevelStreamer] Activated segment {index} at {segmentDef.startPosition}");
        }

        private void DeactivateDistantSegments()
        {
            float deactivateThreshold = _playerDistance - _unloadDistance;

            var toRemove = new List<int>();

            foreach (var kvp in _activeSegments)
            {
                var segment = kvp.Value;
                float segmentEnd = segment.Definition.endPosition.z;

                if (segmentEnd < deactivateThreshold)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (int index in toRemove)
            {
                if (_activeSegments.TryGetValue(index, out var segment))
                {
                    ReturnSegmentToPool(segment);
                    _activeSegments.Remove(index);
                    OnSegmentDeactivated?.Invoke(segment);

                    if (_debugLog) Debug.Log($"[LevelStreamer] Deactivated segment {index}");
                }
            }
        }

        private void ReturnSegmentToPool(SegmentInstance segment)
        {
            segment.Cleanup();
            segment.gameObject.SetActive(false);
            segment.transform.SetParent(transform);
            _segmentPool.Enqueue(segment);
        }

        private void CheckLevelCompletion()
        {
            if (_currentLevelData == null) return;

            float levelEnd = _currentLevelData.totalLength;

            if (_playerDistance >= levelEnd)
            {
                OnLevelCompleted?.Invoke();
                StopStreaming();

                if (_debugLog) Debug.Log("[LevelStreamer] Level completed!");
            }
        }

        /// <summary>
        /// 获取或创建 Chunk 对象池
        /// </summary>
        public GameObjectPool GetChunkPool(string chunkId, GameObject prefab)
        {
            if (!_chunkPools.TryGetValue(chunkId, out var pool))
            {
                pool = new GameObjectPool(prefab, transform, _chunkPoolSize);
                _chunkPools[chunkId] = pool;
            }
            return pool;
        }

        /// <summary>
        /// 玩家通过检查点
        /// </summary>
        public void NotifyCheckpointReached(int checkpointIndex)
        {
            OnCheckpointReached?.Invoke(checkpointIndex);
        }

        protected override void OnDispose()
        {
            StopStreaming();

            foreach (var pool in _chunkPools.Values)
            {
                pool.Clear();
            }
            _chunkPools.Clear();

            while (_segmentPool.Count > 0)
            {
                var seg = _segmentPool.Dequeue();
                if (seg != null) Destroy(seg.gameObject);
            }

            base.OnDispose();
        }

        private void OnDrawGizmos()
        {
            if (!_showGizmos || _currentLevelData == null) return;

            Gizmos.color = Color.green;
            foreach (var segment in _currentLevelData.segments)
            {
                Gizmos.DrawWireCube(segment.startPosition + Vector3.up * 5f, new Vector3(20f, 10f, segment.length));
            }

            // 玩家位置
            if (_playerTransform != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(_playerTransform.position, 3f);

                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_playerTransform.position, _preloadDistance);
                Gizmos.DrawWireSphere(_playerTransform.position, _unloadDistance);
            }
        }
    }

    /// <summary>
    /// 段落实例 - 运行时段落对象
    /// </summary>
    public class SegmentInstance : MonoBehaviour
    {
        public int Index { get; private set; }
        public SegmentDefinition Definition { get; private set; }
        public LevelStreamer Streamer { get; private set; }
        private readonly List<GameObject> _activeChunks = new List<GameObject>();

        public void Initialize(int index, SegmentDefinition definition, LevelStreamer streamer)
        {
            Index = index;
            Definition = definition;
            Streamer = streamer;
            name = $"Segment_{index}";

            InstantiateChunks();
        }

        private void InstantiateChunks()
        {
            if (Definition?.chunks == null) return;

            foreach (var chunkInstance in Definition.chunks)
            {
                // 这里需要从 ThemeProfile 获取 ChunkDefinition 并实例化
                // 简化实现：创建占位物体
                var chunkObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chunkObj.name = $"Chunk_{chunkInstance.chunkId}";
                chunkObj.transform.SetParent(transform);
                chunkObj.transform.localPosition = chunkInstance.localPosition;
                chunkObj.transform.localRotation = chunkInstance.localRotation;
                chunkObj.transform.localScale = Vector3.one * 10f; // 临时大小

                // 移除碰撞体 (仅视觉)
                DestroyImmediate(chunkObj.GetComponent<Collider>());

                _activeChunks.Add(chunkObj);
            }
        }

        public void Cleanup()
        {
            foreach (var chunk in _activeChunks)
            {
                if (chunk != null)
                {
                    // 如果是从对象池获取的，应该返回池中
                    // 这里简化直接销毁
                    Destroy(chunk);
                }
            }
            _activeChunks.Clear();
        }

        /// <summary>
        /// 获取段落内的检查点位置
        /// </summary>
        public Vector3 GetCheckpointPosition()
        {
            return Definition?.checkpoint?.position ?? transform.position;
        }
    }
}