using System;
using System.Collections.Generic;
using UnityEngine;
using TomCatRunner.Core.Level;
using TomCatRunner.Core.Data;
using TomCatRunner.Core.Generation;

namespace TomCatRunner.Gameplay.World
{
    /// <summary>
    /// Chunk 内容构建器 - 订阅 LevelStreamer 事件，把每个流式激活的 Chunk
    /// 填充为完整可玩内容：道路、路缘、草地、树木、障碍、金币、道具
    /// 所有内容程序化生成，按 Chunk 回收时销毁、激活时重建
    /// </summary>
    public class ChunkContentBuilder
    {
        // 固定玩法配色（不随主题，保证可读性）
        private static readonly Color CoinGold = new Color(1f, 0.82f, 0.15f);
        private static readonly Color FenceWood = new Color(0.78f, 0.32f, 0.18f);
        private static readonly Color FenceStripe = new Color(0.95f, 0.9f, 0.82f);
        private static readonly Color BeamGray = new Color(0.42f, 0.46f, 0.55f);
        private static readonly Color SpikeRed = new Color(0.85f, 0.2f, 0.16f);
        private static readonly Color CrateWood = new Color(0.66f, 0.45f, 0.24f);
        private static readonly Color GemPurple = new Color(0.72f, 0.35f, 0.95f);

        private readonly LevelStreamer _streamer;
        private readonly ThemeRuntime _theme;
        private readonly Transform _cat;
        private readonly Action<CoinBehaviour> _onCoin;
        private readonly Action<PowerupBehaviour> _onPowerup;

        private GameTheme _t;   // 当前主题配色缓存

        public ChunkContentBuilder(LevelStreamer streamer, ThemeRuntime theme, Transform cat,
            Action<CoinBehaviour> onCoin, Action<PowerupBehaviour> onPowerup)
        {
            _streamer = streamer;
            _theme = theme;
            _cat = cat;
            _onCoin = onCoin;
            _onPowerup = onPowerup;
            _t = theme.Current;

            theme.ApplyRequested += t => _t = t;

            _streamer.OnChunkActivated += HandleChunkActivated;
            _streamer.OnChunkDeactivated += HandleChunkDeactivated;
        }

        private void HandleChunkActivated(ChunkInstance chunk)
        {
            if (chunk?.GameObject == null || chunk.Data == null) return;

            var go = chunk.GameObject;
            go.transform.position = new Vector3(0f, 0f, chunk.Data.StartZ);

            // 清理旧内容（池复用兜底）
            ClearContent(go);

            var content = new GameObject("Content");
            content.AddComponent<GeneratedContentMarker>();
            content.transform.SetParent(go.transform, false);
            content.transform.localPosition = Vector3.zero;

            var rng = new System.Random(((int)(chunk.Data.StartZ * 7.13f)) ^ ((int)chunk.Data.Length * 31));

            // 1. 基础道路与两侧环境
            BuildRoad(content.transform, chunk.Data);
            BuildEnvironment(content.transform, chunk.Data, rng);

            // 2. 按类型布置玩法内容
            BuildGameplay(content.transform, chunk.Data, rng);
        }

        private void HandleChunkDeactivated(ChunkInstance chunk)
        {
            if (chunk?.GameObject == null) return;
            ClearContent(chunk.GameObject);
        }

        private static void ClearContent(GameObject chunkGo)
        {
            for (int i = chunkGo.transform.childCount - 1; i >= 0; i--)
            {
                var child = chunkGo.transform.GetChild(i);
                if (child.GetComponent<GeneratedContentMarker>() != null)
                    UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        // =====================================================================
        // 道路
        // =====================================================================

        private void BuildRoad(Transform parent, ChunkData data)
        {
            float len = data.Length;

            // 主路面（顶面 y=0）
            WorldPrimitives.Part(parent, PrimitiveType.Cube, new Vector3(0f, -0.2f, len * 0.5f),
                new Vector3(7.4f, 0.4f, len), _t.Road, name: "Road");

            // 路缘
            WorldPrimitives.Part(parent, PrimitiveType.Cube, new Vector3(-3.85f, 0.06f, len * 0.5f),
                new Vector3(0.5f, 0.28f, len), _t.Curb, name: "CurbL");
            WorldPrimitives.Part(parent, PrimitiveType.Cube, new Vector3(3.85f, 0.06f, len * 0.5f),
                new Vector3(0.5f, 0.28f, len), _t.Curb, name: "CurbR");

            // 车道分隔虚线
            for (float z = 2f; z < len - 1f; z += 4f)
            {
                WorldPrimitives.Part(parent, PrimitiveType.Cube, new Vector3(-1.1f, 0.012f, z),
                    new Vector3(0.09f, 0.02f, 1.8f), FenceStripe, castShadow: false, name: "DashL");
                WorldPrimitives.Part(parent, PrimitiveType.Cube, new Vector3(1.1f, 0.012f, z),
                    new Vector3(0.09f, 0.02f, 1.8f), FenceStripe, castShadow: false, name: "DashR");
            }

            // 两侧草地（大平面）
            WorldPrimitives.Part(parent, PrimitiveType.Cube, new Vector3(-19f, -0.24f, len * 0.5f),
                new Vector3(30f, 0.34f, len), _t.Grass, castShadow: false, name: "GrassL");
            WorldPrimitives.Part(parent, PrimitiveType.Cube, new Vector3(19f, -0.24f, len * 0.5f),
                new Vector3(30f, 0.34f, len), _t.Grass, castShadow: false, name: "GrassR");

            // 远景草地（更深色，制造层次）
            WorldPrimitives.Part(parent, PrimitiveType.Cube, new Vector3(-42f, -0.28f, len * 0.5f),
                new Vector3(18f, 0.3f, len), _t.GrassDark, castShadow: false, name: "GrassFarL");
            WorldPrimitives.Part(parent, PrimitiveType.Cube, new Vector3(42f, -0.28f, len * 0.5f),
                new Vector3(18f, 0.3f, len), _t.GrassDark, castShadow: false, name: "GrassFarR");
        }

        // =====================================================================
        // 环境
        // =====================================================================

        private void BuildEnvironment(Transform parent, ChunkData data, System.Random rng)
        {
            // 树木
            for (float z = 3f; z < data.Length - 2f; z += 9f + (float)rng.NextDouble() * 8f)
            {
                BuildTree(parent, rng, -6.2f - (float)rng.NextDouble() * 5.5f, z);
                BuildTree(parent, rng, 6.2f + (float)rng.NextDouble() * 5.5f, z + 4f);
            }

            // 岩石
            int rocks = (int)(data.Length / 22f) + 1;
            for (int i = 0; i < rocks; i++)
            {
                float z = 2f + (float)rng.NextDouble() * (data.Length - 4f);
                float x = (rng.Next(2) == 0 ? -1 : 1) * (5.4f + (float)rng.NextDouble() * 3.5f);
                float s = 0.5f + (float)rng.NextDouble() * 0.9f;
                WorldPrimitives.Part(parent, PrimitiveType.Sphere, new Vector3(x, 0.1f, z),
                    new Vector3(s, s * 0.7f, s), new Color(0.62f, 0.62f, 0.66f), name: "Rock");
            }

            // 过渡段：入口拱门 + 灯柱
            if (data.Type == ChunkType.Transition)
            {
                BuildGate(parent, data.Length * 0.5f);
                BuildLamp(parent, -4.6f, 1.5f);
                BuildLamp(parent, 4.6f, 1.5f);
            }
        }

        private void BuildTree(Transform parent, System.Random rng, float x, float z)
        {
            float scale = 0.85f + (float)rng.NextDouble() * 0.6f;
            var tree = WorldPrimitives.Node(parent, $"Tree_{x:F0}", new Vector3(x, 0f, z));
            tree.localScale = Vector3.one * scale;
            tree.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

            WorldPrimitives.Part(tree, PrimitiveType.Cylinder, new Vector3(0f, 0.8f, 0f),
                new Vector3(0.22f, 0.8f, 0.22f), _t.TreeTrunk, name: "Trunk");
            WorldPrimitives.Part(tree, PrimitiveType.Sphere, new Vector3(0f, 1.9f, 0f),
                new Vector3(1.5f, 1.4f, 1.5f), _t.TreeLeaf1, name: "Leaf1");
            WorldPrimitives.Part(tree, PrimitiveType.Sphere, new Vector3(0.35f, 1.5f, 0.25f),
                new Vector3(1.0f, 0.9f, 1.0f), _t.TreeLeaf2, name: "Leaf2");
            WorldPrimitives.Part(tree, PrimitiveType.Sphere, new Vector3(-0.3f, 2.35f, -0.2f),
                new Vector3(0.9f, 0.8f, 0.9f), _t.TreeLeaf2, name: "Leaf3");
        }

        private void BuildLamp(Transform parent, float x, float z)
        {
            var lamp = WorldPrimitives.Node(parent, "Lamp", new Vector3(x, 0f, z));
            WorldPrimitives.Part(lamp, PrimitiveType.Cylinder, new Vector3(0f, 1.5f, 0f),
                new Vector3(0.09f, 1.5f, 0.09f), BeamGray, name: "Pole");
            WorldPrimitives.Part(lamp, PrimitiveType.Sphere, new Vector3(0f, 3.1f, 0f),
                new Vector3(0.4f, 0.4f, 0.4f), _t.LampGlow, emission: 0.85f, name: "Bulb");
        }

        private void BuildGate(Transform parent, float z)
        {
            var gate = WorldPrimitives.Node(parent, "Gate", new Vector3(0f, 0f, z));
            WorldPrimitives.Part(gate, PrimitiveType.Cylinder, new Vector3(-4.0f, 1.7f, 0f),
                new Vector3(0.28f, 1.7f, 0.28f), _t.Curb, name: "PillarL");
            WorldPrimitives.Part(gate, PrimitiveType.Cylinder, new Vector3(4.0f, 1.7f, 0f),
                new Vector3(0.28f, 1.7f, 0.28f), _t.Curb, name: "PillarR");
            WorldPrimitives.Part(gate, PrimitiveType.Cube, new Vector3(0f, 3.5f, 0f),
                new Vector3(8.6f, 0.5f, 0.45f), _t.Accent, name: "Beam");
            WorldPrimitives.Part(gate, PrimitiveType.Cube, new Vector3(0f, 3.05f, 0f),
                new Vector3(7.2f, 0.42f, 0.36f), _t.Accent, emission: 0.28f, name: "Banner");
        }

        // =====================================================================
        // 玩法内容
        // =====================================================================

        private void BuildGameplay(Transform parent, ChunkData data, System.Random rng)
        {
            float diff = Mathf.Clamp01(data.Difficulty01);
            float len = data.Length;

            switch (data.Type)
            {
                case ChunkType.Jump: BuildJumpChunk(parent, data, rng, diff); break;
                case ChunkType.Slide: BuildSlideChunk(parent, data, rng, diff); break;
                case ChunkType.Attack: BuildAttackChunk(parent, data, rng, diff); break;
                case ChunkType.Coin: BuildCoinChunk(parent, data, rng, diff); break;
                case ChunkType.Mixed: BuildMixedChunk(parent, data, rng, diff); break;
                case ChunkType.Hazard: BuildHazardChunk(parent, data, rng, diff); break;
                case ChunkType.Boss: BuildBossChunk(parent, data, rng, diff); break;
                case ChunkType.Powerup: BuildPowerupChunk(parent, data, rng, diff); break;
                case ChunkType.Secret: BuildSecretChunk(parent, data, rng, diff); break;
                case ChunkType.Transition:
                default:
                    // 过渡段：安全区，一行引导金币
                    if (len > 22f) BuildCoinLine(parent, rng, 1, len * 0.5f, 4);
                    break;
            }
        }

        private float Spacing(float diff, float baseNear, float baseFar)
        {
            return Mathf.Lerp(baseFar, baseNear, diff);
        }

        // ---- 跳跃段：低栏（可跳过或换道） ----
        private void BuildJumpChunk(Transform parent, ChunkData data, System.Random rng, float diff)
        {
            float spacing = Spacing(diff, 9f, 16f);
            for (float z = 8f; z < data.Length - 6f; z += spacing)
            {
                bool allLanes = rng.NextDouble() < 0.55f + diff * 0.25f;
                if (allLanes)
                {
                    BuildFence(parent, rng, -1, z);
                    BuildFence(parent, rng, 0, z);
                    BuildFence(parent, rng, 1, z);
                    // 栏上金币弧线（奖励跳跃）
                    if (rng.NextDouble() < 0.45f) BuildCoinArc(parent, rng, 0, z);
                }
                else
                {
                    int skip = rng.Next(3);
                    for (int lane = -1; lane <= 1; lane++)
                        if (lane != skip) BuildFence(parent, rng, lane, z);
                    BuildCoinLine(parent, rng, skip, z - 3f, 3);
                }
            }
        }

        // ---- 滑铲段：高梁（必须滑铲） ----
        private void BuildSlideChunk(Transform parent, ChunkData data, System.Random rng, float diff)
        {
            float spacing = Spacing(diff, 11f, 18f);
            for (float z = 10f; z < data.Length - 6f; z += spacing)
            {
                BuildBeam(parent, z);
                if (rng.NextDouble() < 0.5f)
                {
                    // 梁下低金币，鼓励滑铲
                    int lane = rng.Next(3) - 1;
                    for (int i = 0; i < 3; i++)
                        BuildCoin(parent, lane, z - 1.2f + i * 1.2f, 0.4f);
                }
            }
        }

        // ---- 攻击段：移动箱（laneA/laneB 均为 -1..1 的车道偏移） ----
        private void BuildAttackChunk(Transform parent, ChunkData data, System.Random rng, float diff)
        {
            float spacing = Spacing(diff, 13f, 20f);
            for (float z = 12f; z < data.Length - 8f; z += spacing)
            {
                bool wide = rng.NextDouble() < 0.5f;
                BuildMovingCrate(parent, rng, wide ? -1 : 0, wide ? 1 : 1, z, diff);
                if (rng.NextDouble() < 0.35f)
                    BuildCoinLine(parent, rng, rng.Next(3) - 1, z + 5f, 3);
            }
        }

        // ---- 金币段：大量金币阵型 ----
        private void BuildCoinChunk(Transform parent, ChunkData data, System.Random rng, float diff)
        {
            int pattern = rng.Next(3);
            switch (pattern)
            {
                case 0: // 三道并行线
                    BuildCoinLine(parent, rng, -1, 6f, (int)(data.Length / 2.4f));
                    BuildCoinLine(parent, rng, 0, 6f, (int)(data.Length / 2.4f));
                    BuildCoinLine(parent, rng, 1, 6f, (int)(data.Length / 2.4f));
                    break;
                case 1: // 蛇形
                    {
                        float period = 10f;
                        for (float z = 6f; z < data.Length - 4f; z += 2.2f)
                        {
                            int lane = (int)Mathf.Round(Mathf.Sin((z / period) * Mathf.PI * 2f) * 1f);
                            BuildCoin(parent, lane, z, 0.75f);
                        }
                    }
                    break;
                case 2: // 菱形阵
                    {
                        float cz = data.Length * 0.5f;
                        for (int i = 0; i < 5; i++)
                        {
                            BuildCoin(parent, 0, cz - 6f + i * 3f, 0.75f);
                            if (i is >= 1 and <= 3)
                            {
                                BuildCoin(parent, -1, cz - 6f + i * 3f, 0.75f);
                                BuildCoin(parent, 1, cz - 6f + i * 3f, 0.75f);
                            }
                        }
                    }
                    break;
            }
        }

        // ---- 混合段：栏 + 梁 + 箱交替 ----
        private void BuildMixedChunk(Transform parent, ChunkData data, System.Random rng, float diff)
        {
            float spacing = Spacing(diff, 10f, 16f);
            int patternIdx = 0;
            for (float z = 8f; z < data.Length - 6f; z += spacing)
            {
                switch (patternIdx % 4)
                {
                    case 0:
                        BuildFence(parent, rng, -1, z); BuildFence(parent, rng, 0, z); BuildFence(parent, rng, 1, z);
                        break;
                    case 1:
                        BuildBeam(parent, z);
                        break;
                    case 2:
                        {
                            int blocked = rng.Next(3) - 1;
                            BuildCrate(parent, blocked, z);
                            if (rng.NextDouble() < 0.6f)
                            {
                                int free = blocked == 0 ? 1 : 0;
                                BuildCoinLine(parent, rng, free, z - 2f, 3);
                            }
                        }
                        break;
                    case 3:
                        {
                            int lane = rng.Next(3) - 1;
                            for (int l = -1; l <= 1; l++)
                                if (l != lane) BuildSpikes(parent, rng, l, z);
                        }
                        break;
                }
                patternIdx++;
            }
        }

        // ---- 危险段：地刺 + 高箱密集 ----
        private void BuildHazardChunk(Transform parent, ChunkData data, System.Random rng, float diff)
        {
            float spacing = Spacing(diff, 8.5f, 14f);
            for (float z = 9f; z < data.Length - 6f; z += spacing)
            {
                double roll = rng.NextDouble();
                if (roll < 0.45f)
                {
                    // 双车道地刺
                    int safe = rng.Next(3) - 1;
                    for (int l = -1; l <= 1; l++)
                        if (l != safe) BuildSpikes(parent, rng, l, z);
                    BuildCoinLine(parent, rng, safe, z - 2f, 3);
                }
                else if (roll < 0.8f)
                {
                    // 单高箱
                    BuildCrate(parent, rng.Next(3) - 1, z);
                }
                else
                {
                    // 三连栏（跳！）
                    BuildFence(parent, rng, -1, z); BuildFence(parent, rng, 0, z); BuildFence(parent, rng, 1, z);
                    BuildCoinArc(parent, rng, 0, z);
                }
            }
        }

        // ---- Boss 段：长距离高强度混合 ----
        private void BuildBossChunk(Transform parent, ChunkData data, System.Random rng, float diff)
        {
            float spacing = Spacing(diff, 9f, 13f);
            int idx = 0;
            for (float z = 12f; z < data.Length - 10f; z += spacing)
            {
                switch (idx % 5)
                {
                    case 0: BuildMovingCrate(parent, rng, -1, 1, z, diff); break;
                    case 1:
                        BuildFence(parent, rng, -1, z); BuildFence(parent, rng, 0, z); BuildFence(parent, rng, 1, z);
                        BuildCoinArc(parent, rng, 0, z);
                        break;
                    case 2: BuildBeam(parent, z); break;
                    case 3:
                        {
                            int safe = rng.Next(3) - 1;
                            for (int l = -1; l <= 1; l++)
                                if (l != safe) BuildSpikes(parent, rng, l, z);
                        }
                        break;
                    case 4: BuildMovingCrate(parent, rng, 0, 1, z, diff); break;
                }
                idx++;
            }
            // 段尾胜利拱门
            BuildGate(parent, data.Length - 4f);
        }

        // ---- 道具段：保证一个道具 + 引导金币 ----
        private void BuildPowerupChunk(Transform parent, ChunkData data, System.Random rng, float diff)
        {
            float z = data.Length * 0.5f;
            int lane = rng.Next(3) - 1;
            BuildPowerup(parent, lane, z, rng);
            BuildCoinLine(parent, rng, lane, z + 4f, 4);
            if (data.Length > 28f)
                BuildFence(parent, rng, lane, z + 14f);
        }

        // ---- 隐藏段：宝石 + 金币环 ----
        private void BuildSecretChunk(Transform parent, ChunkData data, System.Random rng, float diff)
        {
            float cz = data.Length * 0.5f;
            int lane = rng.Next(3) - 1;

            // 金币环
            const int count = 10;
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f;
                float x = lane * CatController.LaneWidth + Mathf.Cos(a) * 1.6f;
                float z = cz + Mathf.Sin(a) * 3.2f;
                var coin = BuildCoinAt(parent, x, 1.1f, z);
                coin.SpinSpeed = 200f;
            }

            // 中央宝石（大额金币）
            var gem = WorldPrimitives.Part(parent, PrimitiveType.Cube,
                new Vector3(lane * CatController.LaneWidth, 1.1f, cz), new Vector3(0.5f, 0.7f, 0.5f),
                GemPurple, emission: 0.8f, name: "Gem");
            gem.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            var gemCol = gem.AddComponent<BoxCollider>();
            gemCol.isTrigger = true;
            var coinBeh = gem.AddComponent<CoinBehaviour>();
            coinBeh.Value = 5;
            coinBeh.Initialize(_cat, rng.Next(100) / 100f, _onCoin);

            // 守卫：环两侧地刺，增加获取难度
            BuildSpikes(parent, rng, lane, cz - 5.5f);
            BuildSpikes(parent, rng, lane, cz + 5.5f);
        }

        // =====================================================================
        // 部件
        // =====================================================================

        private void BuildFence(Transform parent, System.Random rng, int lane, float z)
        {
            var root = WorldPrimitives.Node(parent, "Fence", new Vector3(lane * CatController.LaneWidth, 0f, z));
            root.gameObject.AddComponent<ObstacleMarker>().Kind = ObstacleKind.Fence;

            // 主体（高 0.55，需跳跃）
            WorldPrimitives.Part(root, PrimitiveType.Cube, new Vector3(0f, 0.28f, 0f),
                new Vector3(2.0f, 0.55f, 0.22f), FenceWood, name: "Bar");
            // 警示条纹
            WorldPrimitives.Part(root, PrimitiveType.Cube, new Vector3(0f, 0.58f, 0f),
                new Vector3(2.05f, 0.1f, 0.24f), FenceStripe, name: "StripeTop");
            // 支脚
            WorldPrimitives.Part(root, PrimitiveType.Cube, new Vector3(-0.8f, 0.1f, 0f),
                new Vector3(0.14f, 0.2f, 0.3f), FenceWood, name: "FootL");
            WorldPrimitives.Part(root, PrimitiveType.Cube, new Vector3(0.8f, 0.1f, 0f),
                new Vector3(0.14f, 0.2f, 0.3f), FenceWood, name: "FootR");

            // 碰撞体（非触发器）
            var col = root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.3f, 0f);
            col.size = new Vector3(2.0f, 0.6f, 0.3f);
        }

        private void BuildBeam(Transform parent, float z)
        {
            var root = WorldPrimitives.Node(parent, "Beam", new Vector3(0f, 0f, z));
            var marker = root.gameObject.AddComponent<ObstacleMarker>();
            marker.Kind = ObstacleKind.Beam;

            // 两侧支柱
            WorldPrimitives.Part(root, PrimitiveType.Cube, new Vector3(-3.4f, 1.0f, 0f),
                new Vector3(0.35f, 2.0f, 0.4f), BeamGray, name: "PillarL");
            WorldPrimitives.Part(root, PrimitiveType.Cube, new Vector3(3.4f, 1.0f, 0f),
                new Vector3(0.35f, 2.0f, 0.4f), BeamGray, name: "PillarR");
            // 横梁（底面 1.05m：站立 1.4 撞，滑铲 0.62 过）
            WorldPrimitives.Part(root, PrimitiveType.Cube, new Vector3(0f, 1.45f, 0f),
                new Vector3(7.2f, 0.55f, 0.5f), BeamGray, name: "Bar");
            // 提示灯
            WorldPrimitives.Part(root, PrimitiveType.Sphere, new Vector3(0f, 1.78f, 0.28f),
                new Vector3(0.22f, 0.22f, 0.22f), SpikeRed, emission: 0.9f, name: "Warn");

            var col = root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.45f, 0f);
            col.size = new Vector3(7.4f, 0.6f, 0.55f);
        }

        private void BuildSpikes(Transform parent, System.Random rng, int lane, float z)
        {
            var root = WorldPrimitives.Node(parent, "Spikes", new Vector3(lane * CatController.LaneWidth, 0f, z));
            root.gameObject.AddComponent<ObstacleMarker>().Kind = ObstacleKind.Spikes;

            // 底座
            WorldPrimitives.Part(root, PrimitiveType.Cube, new Vector3(0f, 0.06f, 0f),
                new Vector3(2.0f, 0.12f, 1.6f), new Color(0.35f, 0.32f, 0.32f), name: "Base");
            // 尖刺两排
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 4; i++)
                {
                    float x = -0.75f + i * 0.5f;
                    float zz = -0.4f + row * 0.8f;
                    WorldPrimitives.Part(root, PrimitiveType.Cube, new Vector3(x, 0.45f, zz),
                        new Vector3(0.16f, 0.7f, 0.16f), SpikeRed, name: $"Spike{row}{i}")
                        .transform.localRotation = Quaternion.Euler((float)(rng.NextDouble() * 10 - 5), 0f, 0f);
                }

            var col = root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.35f, 0f);
            col.size = new Vector3(2.0f, 0.7f, 1.7f);
        }

        private void BuildCrate(Transform parent, int lane, float z)
        {
            var root = WorldPrimitives.Node(parent, "Crate", new Vector3(lane * CatController.LaneWidth, 0f, z));
            root.gameObject.AddComponent<ObstacleMarker>().Kind = ObstacleKind.Crate;

            WorldPrimitives.Part(root, PrimitiveType.Cube, new Vector3(0f, 1.05f, 0f),
                new Vector3(1.9f, 2.1f, 1.4f), CrateWood, name: "Box");
            // 木板纹
            WorldPrimitives.Part(root, PrimitiveType.Cube, new Vector3(0f, 1.05f, 0.71f),
                new Vector3(1.7f, 0.16f, 0.03f), new Color(0.5f, 0.33f, 0.17f), castShadow: false, name: "Band1");
            WorldPrimitives.Part(root, PrimitiveType.Cube, new Vector3(0f, 0.45f, 0.71f),
                new Vector3(1.7f, 0.16f, 0.03f), new Color(0.5f, 0.33f, 0.17f), castShadow: false, name: "Band2");

            var col = root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.05f, 0f);
            col.size = new Vector3(1.9f, 2.1f, 1.4f);
        }

        private void BuildMovingCrate(Transform parent, System.Random rng, int laneA, int laneB, float z, float diff)
        {
            var root = WorldPrimitives.Node(parent, "MovingCrate", new Vector3(0f, 0f, z));
            var marker = root.gameObject.AddComponent<ObstacleMarker>();
            marker.Kind = ObstacleKind.Moving;

            var box = WorldPrimitives.Node(root, "Box", new Vector3(laneA * CatController.LaneWidth, 0f, 0f));
            WorldPrimitives.Part(box, PrimitiveType.Cube, new Vector3(0f, 1.0f, 0f),
                new Vector3(1.85f, 2.0f, 1.3f), new Color(0.72f, 0.3f, 0.3f), name: "Box");
            WorldPrimitives.Part(box, PrimitiveType.Cube, new Vector3(0f, 1.0f, 0.66f),
                new Vector3(1.6f, 0.2f, 0.04f), FenceStripe, castShadow: false, name: "Band");
            var col = box.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.0f, 0f);
            col.size = new Vector3(1.85f, 2.0f, 1.3f);

            var mover = box.gameObject.AddComponent<MovingObstacle>();
            mover.XMin = Mathf.Min(laneA, laneB) * CatController.LaneWidth;
            mover.XMax = Mathf.Max(laneA, laneB) * CatController.LaneWidth;
            mover.Speed = Mathf.Lerp(1.6f, 0.9f, diff);   // 越难越快
            mover.StartRight = rng.Next(2) == 0;
        }

        // ---- 金币 ----

        private void BuildCoinLine(Transform parent, System.Random rng, int lane, float startZ, int count)
        {
            for (int i = 0; i < count; i++)
                BuildCoin(parent, lane, startZ + i * 2.2f, 0.85f, rng);
        }

        /// <summary>跳跃弧线金币（越过障碍的奖励线）</summary>
        private void BuildCoinArc(Transform parent, System.Random rng, int lane, float obstacleZ)
        {
            const int count = 5;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                float y = 0.8f + Mathf.Sin(t * Mathf.PI) * 1.5f;
                float z = obstacleZ - 2.6f + t * 5.2f;
                var coin = BuildCoinAt(parent, lane * CatController.LaneWidth, y, z);
                coin.BobAmplitude = 0.05f;
            }
        }

        private void BuildCoin(Transform parent, int lane, float z, float y, System.Random rng = null)
        {
            BuildCoinAt(parent, lane * CatController.LaneWidth, y, z, rng);
        }

        private CoinBehaviour BuildCoinAt(Transform parent, float x, float y, float z, System.Random rng = null)
        {
            var go = new GameObject("Coin");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, y, z);

            var disc = WorldPrimitives.Part(go.transform, PrimitiveType.Cylinder, Vector3.zero,
                new Vector3(0.36f, 0.05f, 0.36f), CoinGold, emission: 0.55f, name: "Disc");
            // 内圈花纹
            WorldPrimitives.Part(go.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.06f, 0f),
                new Vector3(0.22f, 0.045f, 0.22f), new Color(1f, 0.65f, 0.1f), emission: 0.4f, castShadow: false, name: "Inner");

            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.55f;
            col.center = Vector3.zero;

            var coin = go.AddComponent<CoinBehaviour>();
            coin.Initialize(_cat, (rng?.Next(100) ?? 0) / 100f, _onCoin);
            disc.transform.localRotation = Quaternion.identity; // 旋转由 CoinBehaviour 接管
            return coin;
        }

        // ---- 道具 ----

        private static readonly Color MagnetBlue = new Color(0.25f, 0.55f, 1f);
        private static readonly Color ShieldYellow = new Color(1f, 0.85f, 0.25f);
        private static readonly Color BoostRed = new Color(1f, 0.35f, 0.25f);

        private void BuildPowerup(Transform parent, int lane, float z, System.Random rng)
        {
            var go = new GameObject("Powerup");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(lane * CatController.LaneWidth, 1.0f, z);

            float roll = (float)rng.NextDouble();
            var kind = roll < 0.4f ? PowerupKind.Magnet : roll < 0.75f ? PowerupKind.Shield : PowerupKind.Boost;
            var color = kind == PowerupKind.Magnet ? MagnetBlue
                : kind == PowerupKind.Shield ? ShieldYellow : BoostRed;

            WorldPrimitives.Part(go.transform, PrimitiveType.Capsule, Vector3.zero,
                new Vector3(0.55f, 0.42f, 0.55f), color, emission: 0.7f, name: "Core");
            // 环绕小星
            for (int i = 0; i < 3; i++)
            {
                float a = i / 3f * Mathf.PI * 2f;
                WorldPrimitives.Part(go.transform, PrimitiveType.Sphere,
                    new Vector3(Mathf.Cos(a) * 0.55f, 0.3f, Mathf.Sin(a) * 0.55f),
                    new Vector3(0.12f, 0.12f, 0.12f), Color.white, emission: 1f, castShadow: false, name: $"Star{i}");
            }

            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.75f;

            var powerup = go.AddComponent<PowerupBehaviour>();
            powerup.Kind = kind;
            powerup.Initialize(_cat, rng.Next(100) / 100f, _onPowerup);
        }
    }
}
