using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TomCatRunner.Gameplay.World
{
    /// <summary>
    /// 世界图元工具 - 统一创建程序化网格部件与材质
    /// 材质缓存：同色共享，减少 DrawCall 与内存
    /// </summary>
    public static class WorldPrimitives
    {
        private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();
        private static Shader _standardShader;

        public static Shader StandardShader
        {
            get
            {
                if (_standardShader == null)
                {
                    // WebGL IL2CPP stripping 会裁掉所有未被显式引用的 built-in shader。
                    // link.xml 只保 C# 类型不保 .shader 文件。
                    // 必须有非空兜底，否则 new Material(null) 抛 ArgumentNullException(shader)，
                    // 进而 WorldBuilder.Build() 全部 mesh 用 magenta fallback，画面一片粉红。
                    //
                    // 兜底链顺序：Standard → URP-Lit → Mobile/Diffuse → Unlit/Color → Sprites/Default → UI/Default
                    // 至少 Sprites/Default / UI/Default 是 Unity 始终保留的最基础 shader（2D / UI 系统依赖）。
                    _standardShader = Shader.Find("Standard")
                                      ?? Shader.Find("Universal Render Pipeline/Lit")
                                      ?? Shader.Find("Mobile/Diffuse")
                                      ?? Shader.Find("Unlit/Color")
                                      ?? Shader.Find("Sprites/Default")
                                      ?? Shader.Find("UI/Default")
                                      ?? Shader.Find("Hidden/InternalErrorShader");
                    if (_standardShader == null)
                        Debug.LogError("[WorldPrimitives] No usable shader found! All fallbacks missing.");
                    else
                        Debug.Log($"[WorldPrimitives] Using shader: {_standardShader.name}");
                }
                return _standardShader;
            }
        }

        /// <summary>获取/创建共享材质（按颜色+自发光缓存）</summary>
        public static Material GetMaterial(Color color, float emission = 0f, float smoothness = 0.08f, float metallic = 0f)
        {
            string key = $"{ColorUtility.ToHtmlStringRGBA(color)}|{emission:F2}|{smoothness:F2}|{metallic:F2}";
            if (MaterialCache.TryGetValue(key, out var mat) && mat != null)
                return mat;

            mat = new Material(StandardShader);
            mat.color = color;
            if (StandardShader.name.StartsWith("Standard"))
            {
                mat.SetFloat("_Glossiness", smoothness);
                mat.SetFloat("_Metallic", metallic);
                if (emission > 0.001f)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", color * emission);
                }
            }
            MaterialCache[key] = mat;
            return mat;
        }

        /// <summary>创建图元部件</summary>
        public static GameObject Part(Transform parent, PrimitiveType type, Vector3 localPos,
            Vector3 localScale, Color color, bool collider = false, float emission = 0f,
            string name = null, bool castShadow = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name ?? type.ToString();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;

            if (!collider)
                Object.Destroy(go.GetComponent<Collider>());

            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = GetMaterial(color, emission);
            mr.shadowCastingMode = castShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = true;
            return go;
        }

        /// <summary>创建空节点</summary>
        public static Transform Node(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }
    }
}
