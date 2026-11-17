using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace TomCatRunner.Seasons.Editor
{
    /// <summary>
    /// 白模 Chunk 预制体创建工具
    /// 生成 10 种基础 Chunk 类型的占位预制体，用于程序化关卡生成测试
    /// </summary>
    public static class WhiteboxChunkCreator
    {
        private const string PREFAB_PATH = "Assets/_Project/Seasons/Prefabs/Chunks/Whitebox/";
        private const float CHUNK_LENGTH = 50f;
        private const float LANE_WIDTH = 4f;
        private const float LANE_COUNT = 3;

        [MenuItem("TomCatRunner/Seasons/Create Whitebox Chunks", false, 100)]
        public static void CreateAllWhiteboxChunks()
        {
            CreateDirectoryIfNeeded(PREFAB_PATH);

            var chunks = new List<(string id, System.Action<GameObject> setup)>
            {
                ("straight", SetupStraightChunk),
                ("jump", SetupJumpChunk),
                ("slide", SetupSlideChunk),
                ("attack", SetupAttackChunk),
                ("coin_line", SetupCoinLineChunk),
                ("powerup", SetupPowerupChunk),
                ("hazard_spikes", SetupHazardSpikesChunk),
                ("hazard_laser", SetupHazardLaserChunk),
                ("mixed", SetupMixedChunk),
                ("boss_arena", SetupBossArenaChunk)
            };

            foreach (var (id, setup) in chunks)
            {
                CreateChunkPrefab(id, setup);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[WhiteboxChunkCreator] Created {chunks.Count} whitebox chunk prefabs at {PREFAB_PATH}");
        }

        private static void CreateDirectoryIfNeeded(string path)
        {
            var folders = path.Split('/');
            string currentPath = "";
            foreach (var folder in folders)
            {
                if (string.IsNullOrEmpty(folder)) continue;
                currentPath += (currentPath == "" ? "" : "/") + folder;
                if (!AssetDatabase.IsValidFolder(currentPath))
                {
                    var parent = System.IO.Path.GetDirectoryName(currentPath);
                    var name = System.IO.Path.GetFileName(currentPath);
                    AssetDatabase.CreateFolder(parent, name);
                }
            }
        }

        private static void CreateChunkPrefab(string chunkId, System.Action<GameObject> setup)
        {
            var prefabPath = PREFAB_PATH + $"Chunk_{chunkId}.prefab";

            // 检查是否已存在
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null)
            {
                // 更新现有预制体
                var instance = PrefabUtility.InstantiatePrefab(existing) as GameObject;
                SetupChunkBase(instance, chunkId);
                setup(instance);
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                Object.DestroyImmediate(instance);
                Debug.Log($"[WhiteboxChunkCreator] Updated: {chunkId}");
            }
            else
            {
                // 创建新预制体
                var instance = new GameObject($"Chunk_{chunkId}");
                SetupChunkBase(instance, chunkId);
                setup(instance);

                var prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                Object.DestroyImmediate(instance);
                Debug.Log($"[WhiteboxChunkCreator] Created: {chunkId}");
            }
        }

        private static void SetupChunkBase(GameObject chunk, string chunkId)
        {
            // 添加 Chunk 标识组件
            var identifier = chunk.AddComponent<ChunkIdentifier>();
            identifier.chunkId = chunkId;
            identifier.chunkLength = CHUNK_LENGTH;
            identifier.laneCount = (int)LANE_COUNT;
            identifier.laneWidth = LANE_WIDTH;

            // 添加网格渲染器用于可视化边界
            var boundsRenderer = chunk.AddComponent<ChunkBoundsVisualizer>();
            boundsRenderer.chunkLength = CHUNK_LENGTH;
            boundsRenderer.laneWidth = LANE_WIDTH;
            boundsRenderer.laneCount = (int)LANE_COUNT;

            // 设置层级
            chunk.layer = LayerMask.NameToLayer("Default");
        }

        // ===================== 各类型 Chunk 设置 =====================

        private static void SetupStraightChunk(GameObject chunk)
        {
            // 直道：仅地面和路边标记
            CreateGroundPlane(chunk, "Ground", new Color(0.2f, 0.2f, 0.25f));
            CreateLaneMarkers(chunk, new Color(0.5f, 0.8f, 1f, 0.5f));
            CreateSideWalls(chunk, new Color(0.1f, 0.1f, 0.15f));
        }

        private static void SetupJumpChunk(GameObject chunk)
        {
            SetupStraightChunk(chunk);

            // 跳跃障碍：中间车道放置障碍物
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = "Obstacle_Jump";
            obstacle.transform.SetParent(chunk.transform);
            obstacle.transform.localPosition = new Vector3(0, 1f, CHUNK_LENGTH * 0.5f);
            obstacle.transform.localScale = new Vector3(LANE_WIDTH * 0.8f, 2f, 3f);
            obstacle.GetComponent<Renderer>().material.color = new Color(1f, 0.3f, 0.3f);
            obstacle.tag = "Obstacle";
            obstacle.layer = LayerMask.NameToLayer("Obstacle");

            // 添加触发器提示跳跃
            var trigger = obstacle.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(LANE_WIDTH, 3f, 5f);
            trigger.center = new Vector3(0, 1.5f, 0);

            var jumpMarker = obstacle.AddComponent<JumpTrigger>();
            jumpMarker.requiredLane = 1; // 中间车道
        }

        private static void SetupSlideChunk(GameObject chunk)
        {
            SetupStraightChunk(chunk);

            // 滑铲障碍：低空悬浮障碍
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = "Obstacle_Slide";
            obstacle.transform.SetParent(chunk.transform);
            obstacle.transform.localPosition = new Vector3(0, 2.5f, CHUNK_LENGTH * 0.5f);
            obstacle.transform.localScale = new Vector3(LANE_WIDTH * 1.2f, 1f, 4f);
            obstacle.GetComponent<Renderer>().material.color = new Color(1f, 0.5f, 0.2f);
            obstacle.tag = "Obstacle";
            obstacle.layer = LayerMask.NameToLayer("Obstacle");

            var trigger = obstacle.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(LANE_WIDTH * 1.5f, 3f, 6f);
            trigger.center = new Vector3(0, 2f, 0);

            var slideMarker = obstacle.AddComponent<SlideTrigger>();
            slideMarker.requiredLane = 1;
        }

        private static void SetupAttackChunk(GameObject chunk)
        {
            SetupStraightChunk(chunk);

            // 攻击目标：可破坏的敌人/障碍
            for (int i = 0; i < 3; i++)
            {
                float laneOffset = (i - 1) * LANE_WIDTH;
                var target = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                target.name = $"Target_Attack_{i}";
                target.transform.SetParent(chunk.transform);
                target.transform.localPosition = new Vector3(laneOffset, 1.5f, CHUNK_LENGTH * (0.3f + i * 0.2f));
                target.transform.localScale = new Vector3(2f, 3f, 2f);
                target.GetComponent<Renderer>().material.color = new Color(0.8f, 0.2f, 0.8f);
                target.tag = "AttackTarget";
                target.layer = LayerMask.NameToLayer("Enemy");

                var collider = target.GetComponent<CapsuleCollider>();
                collider.isTrigger = true;

                var attackTarget = target.AddComponent<AttackTarget>();
                attackTarget.health = 1;
                attackTarget.scoreValue = 100;
                attackTarget.laneIndex = i;
            }
        }

        private static void SetupCoinLineChunk(GameObject chunk)
        {
            SetupStraightChunk(chunk);

            // 金币线：三条车道各一排金币
            for (int lane = 0; lane < LANE_COUNT; lane++)
            {
                float laneOffset = (lane - 1) * LANE_WIDTH;
                for (int i = 0; i < 10; i++)
                {
                    var coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    coin.name = $"Coin_{lane}_{i}";
                    coin.transform.SetParent(chunk.transform);
                    coin.transform.localPosition = new Vector3(
                        laneOffset,
                        1.5f,
                        CHUNK_LENGTH * 0.1f + i * (CHUNK_LENGTH * 0.08f)
                    );
                    coin.transform.localScale = new Vector3(0.8f, 0.2f, 0.8f);
                    coin.transform.localRotation = Quaternion.Euler(90, 0, 0);
                    coin.GetComponent<Renderer>().material.color = new Color(1f, 0.85f, 0f);
                    coin.tag = "Coin";
                    coin.layer = LayerMask.NameToLayer("Collectible");

                    var collider = coin.GetComponent<CapsuleCollider>();
                    collider.isTrigger = true;

                    var coinComp = coin.AddComponent<CoinPickup>();
                    coinComp.value = 1;
                    coinComp.laneIndex = lane;
                }
            }
        }

        private static void SetupPowerupChunk(GameObject chunk)
        {
            SetupStraightChunk(chunk);

            // 道具：随机车道放置一个道具
            int lane = UnityEngine.Random.Range(0, (int)LANE_COUNT);
            float laneOffset = (lane - 1) * LANE_WIDTH;

            var powerup = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            powerup.name = "Powerup_Shield";
            powerup.transform.SetParent(chunk.transform);
            powerup.transform.localPosition = new Vector3(laneOffset, 1.5f, CHUNK_LENGTH * 0.5f);
            powerup.transform.localScale = Vector3.one * 2f;
            powerup.GetComponent<Renderer>().material.color = new Color(0f, 0.8f, 1f);
            powerup.tag = "Powerup";
            powerup.layer = LayerMask.NameToLayer("Collectible");

            var collider = powerup.GetComponent<SphereCollider>();
            collider.isTrigger = true;

            var powerupComp = powerup.AddComponent<PowerupPickup>();
            powerupComp.type = PowerupType.Shield;
            powerupComp.duration = 10f;
            powerupComp.laneIndex = lane;
        }

        private static void SetupHazardSpikesChunk(GameObject chunk)
        {
            SetupStraightChunk(chunk);

            // 尖刺陷阱：地面陷阱
            var spikes = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spikes.name = "Hazard_Spikes";
            spikes.transform.SetParent(chunk.transform);
            spikes.transform.localPosition = new Vector3(0, 0.25f, CHUNK_LENGTH * 0.5f);
            spikes.transform.localScale = new Vector3(LANE_WIDTH * 2.5f, 0.5f, 5f);
            spikes.GetComponent<Renderer>().material.color = new Color(0.6f, 0.1f, 0.1f);
            spikes.tag = "Hazard";
            spikes.layer = LayerMask.NameToLayer("Hazard");

            var collider = spikes.GetComponent<BoxCollider>();
            collider.isTrigger = true;

            var hazard = spikes.AddComponent<SpikeHazard>();
            hazard.damage = 1;
            hazard.knockbackForce = 10f;
        }

        private static void SetupHazardLaserChunk(GameObject chunk)
        {
            SetupStraightChunk(chunk);

            // 激光陷阱：水平激光束
            for (int i = 0; i < 2; i++)
            {
                float height = (i == 0) ? 1f : 2.5f; // 跳跃高度和滑铲高度
                var laser = GameObject.CreatePrimitive(PrimitiveType.Cube);
                laser.name = $"Hazard_Laser_{i}";
                laser.transform.SetParent(chunk.transform);
                laser.transform.localPosition = new Vector3(0, height, CHUNK_LENGTH * 0.5f);
                laser.transform.localScale = new Vector3(LANE_WIDTH * 3f, 0.2f, 0.2f);
                laser.GetComponent<Renderer>().material.color = new Color(1f, 0f, 0f, 0.8f);
                laser.tag = "Hazard";
                laser.layer = LayerMask.NameToLayer("Hazard");

                var collider = laser.GetComponent<BoxCollider>();
                collider.isTrigger = true;

                var laserHazard = laser.AddComponent<LaserHazard>();
                laserHazard.damage = 1;
                laserHazard.isVertical = false;
                laserHazard.warningTime = 1f;
            }
        }

        private static void SetupMixedChunk(GameObject chunk)
        {
            // 混合段：组合多种元素
            SetupStraightChunk(chunk);

            // 金币
            for (int lane = 0; lane < LANE_COUNT; lane++)
            {
                float laneOffset = (lane - 1) * LANE_WIDTH;
                for (int i = 0; i < 5; i++)
                {
                    var coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    coin.name = $"Coin_Mixed_{lane}_{i}";
                    coin.transform.SetParent(chunk.transform);
                    coin.transform.localPosition = new Vector3(
                        laneOffset, 1.5f,
                        CHUNK_LENGTH * 0.1f + i * (CHUNK_LENGTH * 0.15f)
                    );
                    coin.transform.localScale = new Vector3(0.6f, 0.15f, 0.6f);
                    coin.transform.localRotation = Quaternion.Euler(90, 0, 0);
                    coin.GetComponent<Renderer>().material.color = new Color(1f, 0.85f, 0f);
                    coin.tag = "Coin";
                    var collider = coin.GetComponent<CapsuleCollider>();
                    collider.isTrigger = true;
                    coin.AddComponent<CoinPickup>().value = 1;
                }
            }

            // 一个跳跃障碍
            var jumpObs = GameObject.CreatePrimitive(PrimitiveType.Cube);
            jumpObs.name = "Obstacle_Jump_Mixed";
            jumpObs.transform.SetParent(chunk.transform);
            jumpObs.transform.localPosition = new Vector3(-LANE_WIDTH, 1f, CHUNK_LENGTH * 0.6f);
            jumpObs.transform.localScale = new Vector3(LANE_WIDTH * 0.8f, 2f, 3f);
            jumpObs.GetComponent<Renderer>().material.color = new Color(1f, 0.3f, 0.3f);
            jumpObs.tag = "Obstacle";
            var jumpTrigger = jumpObs.AddComponent<BoxCollider>();
            jumpTrigger.isTrigger = true;
            jumpTrigger.size = new Vector3(LANE_WIDTH, 3f, 5f);
            jumpObs.AddComponent<JumpTrigger>().requiredLane = 0;

            // 一个滑铲障碍
            var slideObs = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slideObs.name = "Obstacle_Slide_Mixed";
            slideObs.transform.SetParent(chunk.transform);
            slideObs.transform.localPosition = new Vector3(LANE_WIDTH, 2.5f, CHUNK_LENGTH * 0.7f);
            slideObs.transform.localScale = new Vector3(LANE_WIDTH * 0.8f, 1f, 4f);
            slideObs.GetComponent<Renderer>().material.color = new Color(1f, 0.5f, 0.2f);
            slideObs.tag = "Obstacle";
            var slideTrigger = slideObs.AddComponent<BoxCollider>();
            slideTrigger.isTrigger = true;
            slideTrigger.size = new Vector3(LANE_WIDTH, 3f, 6f);
            slideObs.AddComponent<SlideTrigger>().requiredLane = 2;
        }

        private static void SetupBossArenaChunk(GameObject chunk)
        {
            // Boss 竞技场：更宽的平台，特殊标记
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Boss_Ground";
            ground.transform.SetParent(chunk.transform);
            ground.transform.localPosition = new Vector3(0, -0.5f, CHUNK_LENGTH * 0.5f);
            ground.transform.localScale = new Vector3(LANE_WIDTH * 5f, 1f, CHUNK_LENGTH);
            ground.GetComponent<Renderer>().material.color = new Color(0.1f, 0.05f, 0.15f);
            ground.tag = "Ground";

            // Boss 生成点标记
            var spawnPoint = new GameObject("Boss_SpawnPoint");
            spawnPoint.transform.SetParent(chunk.transform);
            spawnPoint.transform.localPosition = new Vector3(0, 2f, CHUNK_LENGTH * 0.8f);

            var spawnMarker = spawnPoint.AddComponent<BossSpawnPoint>();
            spawnMarker.bossType = "CyberBoss";
            spawnMarker.phaseCount = 3;

            // 围栏
            CreateBossArenaWalls(chunk);

            // 装饰性粒子发射点
            for (int i = 0; i < 4; i++)
            {
                var emitter = new GameObject($"ParticleEmitter_{i}");
                emitter.transform.SetParent(chunk.transform);
                float angle = i * 90f;
                float radius = LANE_WIDTH * 2.5f;
                emitter.transform.localPosition = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                    3f,
                    CHUNK_LENGTH * 0.5f + Mathf.Sin(angle * Mathf.Deg2Rad) * radius
                );
                emitter.AddComponent<ParticleEmitterMarker>().emitterType = "BossArena";
            }
        }

        // ===================== 辅助方法 =====================

        private static void CreateGroundPlane(GameObject parent, string name, Color color)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = name;
            ground.transform.SetParent(parent.transform);
            ground.transform.localPosition = new Vector3(0, -0.5f, CHUNK_LENGTH * 0.5f);
            ground.transform.localScale = new Vector3(LANE_WIDTH * LANE_COUNT, 1f, CHUNK_LENGTH);
            ground.GetComponent<Renderer>().material.color = color;
            ground.tag = "Ground";
        }

        private static void CreateLaneMarkers(GameObject parent, Color color)
        {
            for (int i = 1; i < LANE_COUNT; i++)
            {
                float x = (i - 1) * LANE_WIDTH - LANE_WIDTH * (LANE_COUNT - 1) / 2f + LANE_WIDTH / 2f;
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = $"LaneMarker_{i}";
                marker.transform.SetParent(parent.transform);
                marker.transform.localPosition = new Vector3(x, 0.05f, CHUNK_LENGTH * 0.5f);
                marker.transform.localScale = new Vector3(0.1f, 0.1f, CHUNK_LENGTH);
                marker.GetComponent<Renderer>().material.color = color;
                marker.tag = "LaneMarker";
                Object.DestroyImmediate(marker.GetComponent<Collider>());
            }
        }

        private static void CreateSideWalls(GameObject parent, Color color)
        {
            float halfWidth = LANE_WIDTH * LANE_COUNT / 2f;
            for (int side = -1; side <= 1; side += 2)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"SideWall_{side}";
                wall.transform.SetParent(parent.transform);
                wall.transform.localPosition = new Vector3(side * halfWidth, 2f, CHUNK_LENGTH * 0.5f);
                wall.transform.localScale = new Vector3(0.5f, 4f, CHUNK_LENGTH);
                wall.GetComponent<Renderer>().material.color = color;
                wall.tag = "Wall";
            }
        }

        private static void CreateBossArenaWalls(GameObject parent)
        {
            float halfWidth = LANE_WIDTH * 2.5f;
            Color wallColor = new Color(0.2f, 0.1f, 0.3f);
            for (int side = -1; side <= 1; side += 2)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"BossWall_{side}";
                wall.transform.SetParent(parent.transform);
                wall.transform.localPosition = new Vector3(side * halfWidth, 3f, CHUNK_LENGTH * 0.5f);
                wall.transform.localScale = new Vector3(0.5f, 6f, CHUNK_LENGTH);
                wall.GetComponent<Renderer>().material.color = wallColor;
                wall.tag = "Wall";
            }
        }
    }

    // ===================== Chunk 组件定义 =====================

    /// <summary>
    /// Chunk 标识组件
    /// </summary>
    public class ChunkIdentifier : MonoBehaviour
    {
        public string chunkId;
        public float chunkLength = 50f;
        public int laneCount = 3;
        public float laneWidth = 4f;
    }

    /// <summary>
    /// Chunk 边界可视化
    /// </summary>
    public class ChunkBoundsVisualizer : MonoBehaviour
    {
        public float chunkLength = 50f;
        public float laneWidth = 4f;
        public int laneCount = 3;
        public Color gizmoColor = new Color(0f, 1f, 1f, 0.3f);

        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Vector3 center = transform.position + Vector3.forward * chunkLength * 0.5f;
            Vector3 size = new Vector3(laneWidth * laneCount, 5f, chunkLength);
            Gizmos.DrawWireCube(center, size);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 center = transform.position + Vector3.forward * chunkLength * 0.5f;
            Vector3 size = new Vector3(laneWidth * laneCount, 5f, chunkLength);
            Gizmos.DrawWireCube(center, size);
        }
    }

    // ===================== 触发器组件 =====================

    public class JumpTrigger : MonoBehaviour
    {
        public int requiredLane = 1;
        public float jumpHeight = 5f;
        public float coyoteTime = 0.1f;
    }

    public class SlideTrigger : MonoBehaviour
    {
        public int requiredLane = 1;
        public float slideDuration = 1f;
    }

    public class AttackTarget : MonoBehaviour
    {
        public int health = 1;
        public int scoreValue = 100;
        public int laneIndex = 1;
        public GameObject destroyEffect;
    }

    public class CoinPickup : MonoBehaviour
    {
        public int value = 1;
        public int laneIndex = 1;
        public bool isMagnetized = false;
    }

    public enum PowerupType
    {
        Shield,
        Magnet,
        Multiplier,
        SpeedBoost,
        Invincibility,
        DoubleCoins
    }

    public class PowerupPickup : MonoBehaviour
    {
        public PowerupType type = PowerupType.Shield;
        public float duration = 10f;
        public int laneIndex = 1;
        public float multiplierValue = 2f;
    }

    public class SpikeHazard : MonoBehaviour
    {
        public int damage = 1;
        public float knockbackForce = 10f;
        public bool isActive = true;
    }

    public class LaserHazard : MonoBehaviour
    {
        public int damage = 1;
        public bool isVertical = false;
        public float warningTime = 1f;
        public float activeDuration = 2f;
        public float cycleTime = 4f;
    }

    public class BossSpawnPoint : MonoBehaviour
    {
        public string bossType = "CyberBoss";
        public int phaseCount = 3;
        public float[] phaseHealthThresholds = { 0.7f, 0.4f, 0.1f };
    }

    public class ParticleEmitterMarker : MonoBehaviour
    {
        public string emitterType = "Default";
        public float emissionRate = 10f;
        public Color particleColor = Color.white;
    }
}