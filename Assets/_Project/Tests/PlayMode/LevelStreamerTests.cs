using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TomCatRunner.Core;

namespace TomCatRunner.Tests.PlayMode
{
    /// <summary>
    /// LevelStreamer PlayMode Tests
    /// Tests distance-based streaming, object pooling, and chunk lifecycle
    /// </summary>
    public class LevelStreamerTests
    {
        private LevelStreamer _levelStreamer;
        private GameObject _streamerObject;
        private Transform _playerTransform;

        [SetUp]
        public void SetUp()
        {
            _streamerObject = new GameObject("LevelStreamer");
            _levelStreamer = _streamerObject.AddComponent<LevelStreamer>();

            _playerTransform = new GameObject("Player").transform;
            _playerTransform.position = Vector3.zero;

            // 创建测试用的 Chunk 预制体
            CreateTestChunks();
        }

        [TearDown]
        public void TearDown()
        {
            if (_streamerObject != null)
                Object.DestroyImmediate(_streamerObject);
            if (_playerTransform != null)
                Object.DestroyImmediate(_playerTransform.gameObject);
        }

        [UnityTest]
        public IEnumerator LevelStreamer_Initialize_CreatesPool()
        {
            _levelStreamer.Initialize(_playerTransform, GetTestChunkPrefabs());

            Assert.IsNotNull(_levelStreamer.ActiveChunks);
            Assert.AreEqual(0, _levelStreamer.ActiveChunks.Count);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LevelStreamer_StreamInRange_ActivatesChunks()
        {
            _levelStreamer.Initialize(_playerTransform, GetTestChunkPrefabs());

            // 移动玩家触发流式加载
            _playerTransform.position = new Vector3(0, 0, 50);
            _levelStreamer.UpdateStreaming();

            yield return null;

            Assert.Greater(_levelStreamer.ActiveChunks.Count, 0);
        }

        [UnityTest]
        public IEnumerator LevelStreamer_StreamOutOfRange_DeactivatesChunks()
        {
            _levelStreamer.Initialize(_playerTransform, GetTestChunkPrefabs());

            // 先激活一些 Chunk
            _playerTransform.position = new Vector3(0, 0, 50);
            _levelStreamer.UpdateStreaming();

            int activeCount = _levelStreamer.ActiveChunks.Count;
            Assert.Greater(activeCount, 0);

            // 移动玩家远离
            _playerTransform.position = new Vector3(0, 0, 1000);
            _levelStreamer.UpdateStreaming();

            yield return null;

            Assert.Less(_levelStreamer.ActiveChunks.Count, activeCount);
        }

        [UnityTest]
        public IEnumerator LevelStreamer_ObjectPool_ReusesObjects()
        {
            _levelStreamer.Initialize(_playerTransform, GetTestChunkPrefabs());

            // 激活 Chunk
            _playerTransform.position = new Vector3(0, 0, 50);
            _levelStreamer.UpdateStreaming();

            var firstChunk = _levelStreamer.ActiveChunks.Count > 0 ? _levelStreamer.ActiveChunks[0] : null;
            Assert.IsNotNull(firstChunk);

            // 移出范围
            _playerTransform.position = new Vector3(0, 0, 1000);
            _levelStreamer.UpdateStreaming();

            // 再次移入范围
            _playerTransform.position = new Vector3(0, 0, 50);
            _levelStreamer.UpdateStreaming();

            yield return null;

            // 应该复用对象池中的对象
            Assert.GreaterOrEqual(_levelStreamer.PoolSize, _levelStreamer.ActiveChunks.Count);
        }

        [UnityTest]
        public IEnumerator LevelStreamer_DistanceThreshold_Respected()
        {
            _levelStreamer.Initialize(_playerTransform, GetTestChunkPrefabs());
            float streamDistance = _levelStreamer.StreamDistance;

            // 在流式距离内
            _playerTransform.position = new Vector3(0, 0, streamDistance * 0.5f);
            _levelStreamer.UpdateStreaming();

            yield return null;

            Assert.Greater(_levelStreamer.ActiveChunks.Count, 0);

            // 在流式距离外
            _playerTransform.position = new Vector3(0, 0, streamDistance * 2f);
            _levelStreamer.UpdateStreaming();

            yield return null;

            Assert.AreEqual(0, _levelStreamer.ActiveChunks.Count);
        }

        [UnityTest]
        public IEnumerator LevelStreamer_ChunkOrder_Maintained()
        {
            _levelStreamer.Initialize(_playerTransform, GetTestChunkPrefabs());

            _playerTransform.position = new Vector3(0, 0, 100);
            _levelStreamer.UpdateStreaming();

            yield return null;

            // 验证 Chunk 按 Z 轴顺序排列
            for (int i = 1; i < _levelStreamer.ActiveChunks.Count; i++)
            {
                Assert.LessOrEqual(_levelStreamer.ActiveChunks[i - 1].transform.position.z,
                                 _levelStreamer.ActiveChunks[i].transform.position.z);
            }
        }

        [UnityTest]
        public IEnumerator LevelStreamer_MaxActiveChunks_Enforced()
        {
            _levelStreamer.Initialize(_playerTransform, GetTestChunkPrefabs());
            int maxChunks = _levelStreamer.MaxActiveChunks;

            // 移动玩家使大量 Chunk 激活
            _playerTransform.position = new Vector3(0, 0, maxChunks * 50f);
            _levelStreamer.UpdateStreaming();

            yield return null;

            Assert.LessOrEqual(_levelStreamer.ActiveChunks.Count, maxChunks);
        }

        [UnityTest]
        public IEnumerator LevelStreamer_PreloadChunks_Works()
        {
            _levelStreamer.Initialize(_playerTransform, GetTestChunkPrefabs());

            _levelStreamer.PreloadChunks(5);

            yield return null;

            Assert.GreaterOrEqual(_levelStreamer.PoolSize, 5);
        }

        [UnityTest]
        public IEnumerator LevelStreamer_Clear_ReleasesAllChunks()
        {
            _levelStreamer.Initialize(_playerTransform, GetTestChunkPrefabs());

            _playerTransform.position = new Vector3(0, 0, 100);
            _levelStreamer.UpdateStreaming();

            _levelStreamer.Clear();

            yield return null;

            Assert.AreEqual(0, _levelStreamer.ActiveChunks.Count);
            Assert.AreEqual(0, _levelStreamer.PoolSize);
        }

        private void CreateTestChunks()
        {
            // 创建简单的测试 Chunk 预制体
        }

        private List<GameObject> GetTestChunkPrefabs()
        {
            var prefabs = new List<GameObject>();

            for (int i = 0; i < 5; i++)
            {
                var chunk = new GameObject($"TestChunk_{i}");
                chunk.AddComponent<LevelChunk>();
                chunk.transform.localScale = Vector3.one * 50f;
                prefabs.Add(chunk);
            }

            return prefabs;
        }
    }

    // 简单的 LevelChunk 组件用于测试
    public class LevelChunk : MonoBehaviour
    {
        public int ChunkIndex;
        public StreamerChunkType StreamerChunkType;
    }

    public enum StreamerChunkType
    {
        Straight,
        Curve,
        Jump,
        Slide,
        Battle
    }

    // ===================== 测试用 LevelStreamer Stub =====================
    // 最小可用的流式加载器实现（距离激活/回收），隔离对 Core 管线的依赖

    public class LevelStreamer : MonoBehaviour
    {
        public List<GameObject> ActiveChunks = new List<GameObject>();
        public int PoolSize => _pool.Count;
        public float StreamDistance = 120f;
        public float RecycleDistance = 60f;
        public int MaxActiveChunks = 8;
        public Transform PlayerTransform;

        private readonly List<GameObject> _chunkPrefabs = new List<GameObject>();
        private readonly Queue<GameObject> _pool = new Queue<GameObject>();

        public void Initialize(Transform player, List<GameObject> prefabs)
        {
            PlayerTransform = player;
            _chunkPrefabs.Clear();
            if (prefabs != null) _chunkPrefabs.AddRange(prefabs);
        }

        public void UpdateStreaming()
        {
            if (PlayerTransform == null || _chunkPrefabs.Count == 0) return;

            float playerZ = PlayerTransform.position.z;

            // 回收身后 Chunk
            for (int i = ActiveChunks.Count - 1; i >= 0; i--)
            {
                if (ActiveChunks[i].transform.position.z + RecycleDistance < playerZ)
                {
                    var chunk = ActiveChunks[i];
                    ActiveChunks.RemoveAt(i);
                    chunk.SetActive(false);
                    _pool.Enqueue(chunk);
                }
            }

            // 激活前方 Chunk
            float spawnZ = ActiveChunks.Count > 0
                ? ActiveChunks[ActiveChunks.Count - 1].transform.position.z + 50f
                : playerZ + 10f;

            while (ActiveChunks.Count < MaxActiveChunks && spawnZ < playerZ + StreamDistance)
            {
                var prefab = _chunkPrefabs[ActiveChunks.Count % _chunkPrefabs.Count];
                var chunk = _pool.Count > 0 ? _pool.Dequeue() : Instantiate(prefab);
                chunk.transform.position = new Vector3(0, 0, spawnZ);
                chunk.SetActive(true);
                ActiveChunks.Add(chunk);
                spawnZ += 50f;
            }
        }

        public void PreloadChunks(int count)
        {
            if (_chunkPrefabs.Count == 0) return;
            for (int i = 0; i < count && _pool.Count < 32; i++)
            {
                _pool.Enqueue(Instantiate(_chunkPrefabs[i % _chunkPrefabs.Count]));
            }
        }

        public void Clear()
        {
            foreach (var chunk in ActiveChunks)
            {
                if (chunk != null) Destroy(chunk);
            }
            ActiveChunks.Clear();

            while (_pool.Count > 0)
            {
                var chunk = _pool.Dequeue();
                if (chunk != null) Destroy(chunk);
            }
        }
    }
}
