using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using TomCatRunner.Core.Data;
using TomCatRunner.Core.Level;
using TomCatRunner.Core.Season;

namespace TomCatRunner.Gameplay.World
{
    /// <summary>
    /// 世界构建器 - 创建相机、太阳光、起始平台、事件系统等静态基础设施
    /// </summary>
    public class WorldBuilder
    {
        public CameraRig CameraRig { get; private set; }
        public Light Sun { get; private set; }
        public Transform WorldRoot { get; private set; }

        private readonly ThemeRuntime _theme;

        public WorldBuilder(ThemeRuntime theme)
        {
            _theme = theme;
        }

        public void Build()
        {
            WorldRoot = new GameObject("[World]").transform;

            // 相机
            CameraRig = CameraRig.Create(null);
            _theme.SetSun(null); // 占位，下面设置

            // 太阳光
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(WorldRoot, false);
            sunGo.transform.rotation = Quaternion.Euler(52f, -38f, 0f);
            Sun = sunGo.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowResolution = LightShadowResolution.Medium;
            Sun.shadowStrength = 0.75f;
            _theme.SetSun(Sun);

            // 事件系统（uGUI 必需）
            EnsureEventSystem();

            // 起始平台（z ∈ [-35, 2]，避免开局脚下悬空）
            BuildStartPad();
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            Object.DontDestroyOnLoad(es);
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        private void BuildStartPad()
        {
            var pad = new GameObject("StartPad");
            pad.transform.SetParent(WorldRoot, false);

            var t = _theme.Current;

            // 地面（与 chunk 路面同规格）
            const float padLen = 40f;
            const float padCenterZ = -18f; // 覆盖 z ∈ [-38, 2]
            WorldPrimitives.Part(pad.transform, PrimitiveType.Cube, new Vector3(0f, -0.2f, padCenterZ),
                new Vector3(7.4f, 0.4f, padLen), t.Road, name: "Road");
            WorldPrimitives.Part(pad.transform, PrimitiveType.Cube, new Vector3(-3.85f, 0.06f, padCenterZ),
                new Vector3(0.5f, 0.28f, padLen), t.Curb, name: "CurbL");
            WorldPrimitives.Part(pad.transform, PrimitiveType.Cube, new Vector3(3.85f, 0.06f, padCenterZ),
                new Vector3(0.5f, 0.28f, padLen), t.Curb, name: "CurbR");
            WorldPrimitives.Part(pad.transform, PrimitiveType.Cube, new Vector3(-19f, -0.24f, padCenterZ),
                new Vector3(30f, 0.34f, padLen), t.Grass, castShadow: false, name: "GrassL");
            WorldPrimitives.Part(pad.transform, PrimitiveType.Cube, new Vector3(19f, -0.24f, padCenterZ),
                new Vector3(30f, 0.34f, padLen), t.Grass, castShadow: false, name: "GrassR");

            // 起点拱门
            var gate = new GameObject("StartGate");
            gate.transform.SetParent(pad.transform, false);
            WorldPrimitives.Part(gate.transform, PrimitiveType.Cylinder, new Vector3(-4.0f, 1.9f, -2f),
                new Vector3(0.3f, 1.9f, 0.3f), t.Curb, name: "PillarL");
            WorldPrimitives.Part(gate.transform, PrimitiveType.Cylinder, new Vector3(4.0f, 1.9f, -2f),
                new Vector3(0.3f, 1.9f, 0.3f), t.Curb, name: "PillarR");
            WorldPrimitives.Part(gate.transform, PrimitiveType.Cube, new Vector3(0f, 3.9f, -2f),
                new Vector3(9.0f, 0.55f, 0.5f), t.Accent, name: "Beam");
            WorldPrimitives.Part(gate.transform, PrimitiveType.Cube, new Vector3(0f, 3.4f, -2f),
                new Vector3(7.4f, 0.45f, 0.4f), t.Accent, emission: 0.3f, name: "Banner");

            // 起点两侧灯柱
            BuildLampPair(pad.transform, -4.6f, -8f);
            BuildLampPair(pad.transform, -4.6f, -26f);

            // 后方挡墙装饰（树篱，挡住回望的空旷）
            WorldPrimitives.Part(pad.transform, PrimitiveType.Cube, new Vector3(0f, 0.9f, -37.5f),
                new Vector3(46f, 1.8f, 1.2f), t.TreeLeaf2, name: "Hedge");
        }

        private void BuildLampPair(Transform parent, float x, float z)
        {
            var t = _theme.Current;
            WorldPrimitives.Part(parent, PrimitiveType.Cylinder, new Vector3(x, 1.5f, z),
                new Vector3(0.09f, 1.5f, 0.09f), new Color(0.42f, 0.46f, 0.55f), name: $"Pole{x:F0}{z:F0}");
            WorldPrimitives.Part(parent, PrimitiveType.Sphere, new Vector3(x, 3.1f, z),
                new Vector3(0.4f, 0.4f, 0.4f), t.LampGlow, emission: 0.85f, name: $"Bulb{x:F0}{z:F0}");
            WorldPrimitives.Part(parent, PrimitiveType.Cylinder, new Vector3(-x, 1.5f, z),
                new Vector3(0.09f, 1.5f, 0.09f), new Color(0.42f, 0.46f, 0.55f), name: $"PoleR{z:F0}");
            WorldPrimitives.Part(parent, PrimitiveType.Sphere, new Vector3(-x, 3.1f, z),
                new Vector3(0.4f, 0.4f, 0.4f), t.LampGlow, emission: 0.85f, name: $"BulbR{z:F0}");
        }
    }
}
