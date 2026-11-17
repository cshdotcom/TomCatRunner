using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TomCatRunner.Common;

namespace TomCatRunner.UI
{
    /// <summary>
    /// UI Shader 库 - 统一管理所有 UI 特效材质
    /// </summary>
    public static class UIShaderLib
    {
        private static readonly Dictionary<string, Material> _materialCache = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Shader> _shaderCache = new Dictionary<string, Shader>();

        // Shader 路径常量
        public const string SDF_ROUNDED_RECT = "TomCatRunner/UI/SDF Rounded Rect";
        public const string FLUID_NOISE = "TomCatRunner/UI/Fluid Noise";
        public const string CHROMATIC_ABERRATION = "TomCatRunner/UI/Chromatic Aberration";
        public const string HOLOGRAM_SCAN = "TomCatRunner/UI/Hologram Scan";
        public const string EDGE_GLOW = "TomCatRunner/UI/Edge Glow";
        public const string TRANSITION_DISSOLVE = "UI/Transition/Dissolve";

        /// <summary>
        /// 获取或创建材质实例
        /// </summary>
        public static Material GetMaterial(string shaderPath, bool createInstance = true)
        {
            if (_materialCache.TryGetValue(shaderPath, out var cached) && cached != null)
            {
                return createInstance ? new Material(cached) : cached;
            }

            var shader = GetShader(shaderPath);
            if (shader == null)
            {
                Debug.LogError($"[UIShaderLib] Shader not found: {shaderPath}");
                return null;
            }

            var material = new Material(shader);
            _materialCache[shaderPath] = material;
            return createInstance ? new Material(material) : material;
        }

        /// <summary>
        /// 获取 Shader
        /// </summary>
        public static Shader GetShader(string shaderPath)
        {
            if (_shaderCache.TryGetValue(shaderPath, out var cached))
                return cached;

            var shader = Shader.Find(shaderPath);
            if (shader != null)
            {
                _shaderCache[shaderPath] = shader;
            }
            return shader;
        }

        /// <summary>
        /// 为 Image 应用 SDF 圆角矩形
        /// </summary>
        public static Material ApplySDFRoundedRect(Image image, float cornerRadius = 0.1f, float borderWidth = 0f, Color? borderColor = null, Color? shadowColor = null)
        {
            var material = GetMaterial(SDF_ROUNDED_RECT);
            if (material == null) return null;

            material.SetFloat("_CornerRadius", cornerRadius);
            material.SetFloat("_BorderWidth", borderWidth);
            if (borderColor.HasValue) material.SetColor("_BorderColor", borderColor.Value);
            if (shadowColor.HasValue) material.SetColor("_ShadowColor", shadowColor.Value);

            image.material = material;
            return material;
        }

        /// <summary>
        /// 为 Image 应用流体噪波背景
        /// </summary>
        public static Material ApplyFluidNoise(Image image, Color colorA, Color colorB, Color colorC, float scale = 50f, float speed = 0.5f, float strength = 0.3f, float distortion = 0.1f)
        {
            var material = GetMaterial(FLUID_NOISE);
            if (material == null) return null;

            material.SetColor("_ColorA", colorA);
            material.SetColor("_ColorB", colorB);
            material.SetColor("_ColorC", colorC);
            material.SetFloat("_NoiseScale", scale);
            material.SetFloat("_NoiseSpeed", speed);
            material.SetFloat("_NoiseStrength", strength);
            material.SetFloat("_DistortionStrength", distortion);
            material.SetFloat("_BlendingMode", 0); // Additive

            image.material = material;
            return material;
        }

        /// <summary>
        /// 为 Image 应用色差效果
        /// </summary>
        public static Material ApplyChromaticAberration(Image image, float strength = 0.01f, float vignette = 0.3f, float scanlineIntensity = 0.1f)
        {
            var material = GetMaterial(CHROMATIC_ABERRATION);
            if (material == null) return null;

            material.SetFloat("_AberrationStrength", strength);
            material.SetFloat("_Vignette", vignette);
            material.SetFloat("_ScanlineIntensity", scanlineIntensity);

            image.material = material;
            return material;
        }

        /// <summary>
        /// 为 Image 应用全息扫描效果
        /// </summary>
        public static Material ApplyHologramScan(Image image, Color baseColor, Color scanColor, float scanWidth = 0.02f, float speed = 1f, float intensity = 2f, float flicker = 0.1f, float noise = 0.2f, float glitch = 0.05f)
        {
            var material = GetMaterial(HOLOGRAM_SCAN);
            if (material == null) return null;

            material.SetColor("_Color", baseColor);
            material.SetColor("_ScanColor", scanColor);
            material.SetFloat("_ScanWidth", scanWidth);
            material.SetFloat("_ScanSpeed", speed);
            material.SetFloat("_Intensity", intensity);
            material.SetFloat("_FlickerIntensity", flicker);
            material.SetFloat("_NoiseIntensity", noise);
            material.SetFloat("_GlitchIntensity", glitch);

            image.material = material;
            return material;
        }

        /// <summary>
        /// 为 Image 应用边缘发光
        /// </summary>
        public static Material ApplyEdgeGlow(Image image, Color glowColor, float intensity = 2f, float width = 0.1f, float falloff = 2f, bool animated = true, float pulseSpeed = 2f, float pulseIntensity = 0.5f)
        {
            var material = GetMaterial(EDGE_GLOW);
            if (material == null) return null;

            material.SetColor("_GlowColor", glowColor);
            material.SetFloat("_GlowIntensity", intensity);
            material.SetFloat("_GlowWidth", width);
            material.SetFloat("_GlowFalloff", falloff);
            material.SetFloat("_Animated", animated ? 1f : 0f);
            material.SetFloat("_PulseSpeed", pulseSpeed);
            material.SetFloat("_PulseIntensity", pulseIntensity);

            image.material = material;
            return material;
        }

        /// <summary>
        /// 创建溶解过渡材质
        /// </summary>
        public static Material CreateDissolveTransition(Texture2D dissolveTexture, float edgeWidth = 0.01f, Color? edgeColor = null)
        {
            var material = GetMaterial(TRANSITION_DISSOLVE);
            if (material == null) return null;

            if (dissolveTexture != null) material.SetTexture("_DissolveTex", dissolveTexture);
            material.SetFloat("_DissolveEdgeWidth", edgeWidth);
            if (edgeColor.HasValue) material.SetColor("_DissolveEdgeColor", edgeColor.Value);
            material.SetFloat("_DissolveAmount", 0f);

            return material;
        }

        /// <summary>
        /// 更新溶解进度
        /// </summary>
        public static void SetDissolveProgress(Material material, float progress)
        {
            if (material != null)
            {
                material.SetFloat("_DissolveAmount", Mathf.Clamp01(progress));
            }
        }

        /// <summary>
        /// 清理缓存
        /// </summary>
        public static void ClearCache()
        {
            foreach (var kvp in _materialCache)
            {
                if (kvp.Value != null)
                {
                    if (Application.isPlaying)
                        UnityEngine.Object.Destroy(kvp.Value);
                    else
                        UnityEngine.Object.DestroyImmediate(kvp.Value);
                }
            }
            _materialCache.Clear();
            _shaderCache.Clear();
        }

        /// <summary>
        /// 预热所有 Shader
        /// </summary>
        public static void WarmupShaders()
        {
            GetShader(SDF_ROUNDED_RECT);
            GetShader(FLUID_NOISE);
            GetShader(CHROMATIC_ABERRATION);
            GetShader(HOLOGRAM_SCAN);
            GetShader(EDGE_GLOW);
            GetShader(TRANSITION_DISSOLVE);
        }
    }

    /// <summary>
    /// UI 特效组件基类
    /// </summary>
    public abstract class UIEffectBase : MonoBehaviour
    {
        protected Image _targetImage;
        protected Material _materialInstance;
        protected bool _isPlaying = false;

        protected virtual void Awake()
        {
            _targetImage = GetComponent<Image>();
            if (_targetImage == null)
                _targetImage = gameObject.AddComponent<Image>();
        }

        public virtual void Play()
        {
            _isPlaying = true;
            enabled = true;
        }

        public virtual void Stop()
        {
            _isPlaying = false;
            enabled = false;
        }

        protected virtual void OnDestroy()
        {
            if (_materialInstance != null)
            {
                if (Application.isPlaying)
                    Destroy(_materialInstance);
                else
                    DestroyImmediate(_materialInstance);
            }
        }
    }

    /// <summary>
    /// SDF 圆角矩形组件
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class UISDFRoundedRect : UIEffectBase
    {
        [Header("圆角设置")]
        [SerializeField] private float _cornerRadius = 0.1f;
        [SerializeField] private float _cornerRadiusTL = 0f;
        [SerializeField] private float _cornerRadiusTR = 0f;
        [SerializeField] private float _cornerRadiusBL = 0f;
        [SerializeField] private float _cornerRadiusBR = 0f;

        [Header("边框")]
        [SerializeField] private float _borderWidth = 0f;
        [SerializeField] private Color _borderColor = Color.white;

        [Header("阴影")]
        [SerializeField] private float _shadowDistance = 0f;
        [SerializeField] private Color _shadowColor = new Color(0, 0, 0, 0.5f);
        [SerializeField] private float _shadowSoftness = 0.5f;

        [Header("发光")]
        [SerializeField] private float _glowIntensity = 0f;
        [SerializeField] private Color _glowColor = Color.white;

        protected override void Awake()
        {
            base.Awake();
            _materialInstance = UIShaderLib.GetMaterial(UIShaderLib.SDF_ROUNDED_RECT);
            _targetImage.material = _materialInstance;
            UpdateMaterial();
        }

        private void Update()
        {
            if (_isPlaying)
            {
                UpdateMaterial();
            }
        }

        public void UpdateMaterial()
        {
            if (_materialInstance == null) return;

            _materialInstance.SetFloat("_CornerRadius", _cornerRadius);
            _materialInstance.SetFloat("_CornerRadiusTopLeft", _cornerRadiusTL);
            _materialInstance.SetFloat("_CornerRadiusTopRight", _cornerRadiusTR);
            _materialInstance.SetFloat("_CornerRadiusBottomLeft", _cornerRadiusBL);
            _materialInstance.SetFloat("_CornerRadiusBottomRight", _cornerRadiusBR);
            _materialInstance.SetFloat("_BorderWidth", _borderWidth);
            _materialInstance.SetColor("_BorderColor", _borderColor);
            _materialInstance.SetFloat("_ShadowDistance", _shadowDistance);
            _materialInstance.SetColor("_ShadowColor", _shadowColor);
            _materialInstance.SetFloat("_ShadowSoftness", _shadowSoftness);
            _materialInstance.SetFloat("_GlowIntensity", _glowIntensity);
            _materialInstance.SetColor("_GlowColor", _glowColor);
        }

        public void SetCornerRadius(float radius) => _cornerRadius = radius;
        public void SetBorder(float width, Color color) { _borderWidth = width; _borderColor = color; }
        public void SetShadow(float distance, Color color, float softness) { _shadowDistance = distance; _shadowColor = color; _shadowSoftness = softness; }
        public void SetGlow(float intensity, Color color) { _glowIntensity = intensity; _glowColor = color; }
    }

    /// <summary>
    /// 流体噪波背景组件
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class UIFluidNoise : UIEffectBase
    {
        [Header("颜色")]
        [SerializeField] private Color _colorA = new Color(0.2f, 0.6f, 1f, 1f);
        [SerializeField] private Color _colorB = new Color(0.8f, 0.2f, 1f, 1f);
        [SerializeField] private Color _colorC = new Color(0.2f, 1f, 0.6f, 1f);

        [Header("噪波参数")]
        [SerializeField] private float _scale = 50f;
        [SerializeField] private float _speed = 0.5f;
        [SerializeField] private float _strength = 0.3f;
        [SerializeField] private float _distortion = 0.1f;
        [SerializeField] private Vector2 _flowDirection = Vector2.right;
        [SerializeField] private float _flowSpeed = 0.2f;

        [Header("混合模式")]
        [SerializeField] private int _blendingMode = 0; // 0=Add, 1=Multiply, 2=Screen

        protected override void Awake()
        {
            base.Awake();
            _materialInstance = UIShaderLib.GetMaterial(UIShaderLib.FLUID_NOISE);
            _targetImage.material = _materialInstance;
            UpdateMaterial();
        }

        private void Update()
        {
            // 流体噪波需要每帧更新时间
            if (_materialInstance != null)
            {
                _materialInstance.SetFloat("_Time", Time.time);
            }
        }

        public void UpdateMaterial()
        {
            if (_materialInstance == null) return;

            _materialInstance.SetColor("_ColorA", _colorA);
            _materialInstance.SetColor("_ColorB", _colorB);
            _materialInstance.SetColor("_ColorC", _colorC);
            _materialInstance.SetFloat("_NoiseScale", _scale);
            _materialInstance.SetFloat("_NoiseSpeed", _speed);
            _materialInstance.SetFloat("_NoiseStrength", _strength);
            _materialInstance.SetFloat("_DistortionStrength", _distortion);
            _materialInstance.SetVector("_FlowDirection", _flowDirection);
            _materialInstance.SetFloat("_FlowSpeed", _flowSpeed);
            _materialInstance.SetFloat("_BlendingMode", _blendingMode);
        }

        public void SetColors(Color a, Color b, Color c) { _colorA = a; _colorB = b; _colorC = c; }
        public void SetFlow(Vector2 direction, float speed) { _flowDirection = direction; _flowSpeed = speed; }
    }

    /// <summary>
    /// 色差效果组件
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class UIChromaticAberration : UIEffectBase
    {
        [SerializeField] private float _strength = 0.01f;
        [SerializeField] private Vector2 _center = new Vector2(0.5f, 0.5f);
        [SerializeField] private bool _radialMode = true;
        [SerializeField] private float _vignette = 0.3f;
        [SerializeField] private float _scanlineIntensity = 0.1f;
        [SerializeField] private float _scanlineScale = 800f;

        protected override void Awake()
        {
            base.Awake();
            _materialInstance = UIShaderLib.GetMaterial(UIShaderLib.CHROMATIC_ABERRATION);
            _targetImage.material = _materialInstance;
            UpdateMaterial();
        }

        private void Update()
        {
            if (_materialInstance != null)
            {
                _materialInstance.SetFloat("_Time", Time.time);
            }
        }

        public void UpdateMaterial()
        {
            if (_materialInstance == null) return;

            _materialInstance.SetFloat("_AberrationStrength", _strength);
            _materialInstance.SetVector("_AberrationCenter", _center);
            _materialInstance.SetFloat("_RadialMode", _radialMode ? 1f : 0f);
            _materialInstance.SetFloat("_Vignette", _vignette);
            _materialInstance.SetFloat("_ScanlineIntensity", _scanlineIntensity);
            _materialInstance.SetFloat("_ScanlineScale", _scanlineScale);
        }

        public void SetStrength(float strength) => _strength = strength;
        public void SetVignette(float vignette) => _vignette = vignette;
    }

    /// <summary>
    /// 全息扫描组件
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class UIHologramScan : UIEffectBase
    {
        [Header("基础颜色")]
        [SerializeField] private Color _baseColor = new Color(0, 1, 1, 1);
        [SerializeField] private Color _scanColor = new Color(0, 1, 1, 1);

        [Header("扫描线")]
        [SerializeField] private float _scanWidth = 0.02f;
        [SerializeField] private float _scanSpeed = 1f;
        [SerializeField] private float _scanDirection = 1f;
        [SerializeField] private float _intensity = 2f;

        [Header("特效")]
        [SerializeField] private float _flickerIntensity = 0.1f;
        [SerializeField] private float _flickerSpeed = 10f;
        [SerializeField] private float _noiseIntensity = 0.2f;
        [SerializeField] private float _noiseScale = 100f;
        [SerializeField] private float _glitchIntensity = 0.05f;
        [SerializeField] private float _glitchSpeed = 2f;

        [Header("边缘发光")]
        [SerializeField] private float _edgeGlow = 1f;
        [SerializeField] private Color _edgeColor = new Color(0, 0.5f, 1, 1);

        [Header("脉冲")]
        [SerializeField] private float _pulseSpeed = 2f;
        [SerializeField] private float _pulseIntensity = 0.3f;

        [Header("溶解")]
        [SerializeField] private Texture2D _dissolveTexture;
        [SerializeField] private float _dissolveAmount = 0f;
        [SerializeField] private float _dissolveEdgeWidth = 0.01f;
        [SerializeField] private Color _dissolveEdgeColor = Color.white;

        protected override void Awake()
        {
            base.Awake();
            _materialInstance = UIShaderLib.GetMaterial(UIShaderLib.HOLOGRAM_SCAN);
            _targetImage.material = _materialInstance;
            UpdateMaterial();
        }

        private void Update()
        {
            if (_materialInstance != null)
            {
                _materialInstance.SetFloat("_Time", Time.time);
            }
        }

        public void UpdateMaterial()
        {
            if (_materialInstance == null) return;

            _materialInstance.SetColor("_Color", _baseColor);
            _materialInstance.SetColor("_ScanColor", _scanColor);
            _materialInstance.SetFloat("_ScanWidth", _scanWidth);
            _materialInstance.SetFloat("_ScanSpeed", _scanSpeed);
            _materialInstance.SetFloat("_ScanDirection", _scanDirection);
            _materialInstance.SetFloat("_Intensity", _intensity);
            _materialInstance.SetFloat("_FlickerIntensity", _flickerIntensity);
            _materialInstance.SetFloat("_FlickerSpeed", _flickerSpeed);
            _materialInstance.SetFloat("_NoiseIntensity", _noiseIntensity);
            _materialInstance.SetFloat("_NoiseScale", _noiseScale);
            _materialInstance.SetFloat("_GlitchIntensity", _glitchIntensity);
            _materialInstance.SetFloat("_GlitchSpeed", _glitchSpeed);
            _materialInstance.SetFloat("_EdgeGlow", _edgeGlow);
            _materialInstance.SetColor("_EdgeColor", _edgeColor);
            _materialInstance.SetFloat("_PulseSpeed", _pulseSpeed);
            _materialInstance.SetFloat("_PulseIntensity", _pulseIntensity);
            if (_dissolveTexture != null) _materialInstance.SetTexture("_DissolveTex", _dissolveTexture);
            _materialInstance.SetFloat("_DissolveAmount", _dissolveAmount);
            _materialInstance.SetFloat("_DissolveEdgeWidth", _dissolveEdgeWidth);
            _materialInstance.SetColor("_DissolveEdgeColor", _dissolveEdgeColor);
        }

        public void SetDissolve(float amount) => _dissolveAmount = Mathf.Clamp01(amount);
        public void SetColors(Color baseCol, Color scanCol) { _baseColor = baseCol; _scanColor = scanCol; }
    }

    /// <summary>
    /// 边缘发光组件
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class UIEdgeGlow : UIEffectBase
    {
        [Header("发光颜色")]
        [SerializeField] private Color _glowColor = Color.white;
        [SerializeField] private float _intensity = 2f;
        [SerializeField] private float _width = 0.1f;
        [SerializeField] private float _falloff = 2f;

        [Header("动画")]
        [SerializeField] private bool _animated = true;
        [SerializeField] private float _pulseSpeed = 2f;
        [SerializeField] private float _pulseIntensity = 0.5f;
        [SerializeField] private float _rotationSpeed = 30f;
        [SerializeField] private int _waveCount = 3;
        [SerializeField] private float _waveSpeed = 2f;
        [SerializeField] private float _waveAmplitude = 0.02f;

        [Header("内发光")]
        [SerializeField] private bool _innerGlow = false;
        [SerializeField] private Color _innerGlowColor = Color.white;
        [SerializeField] private float _innerGlowWidth = 0.05f;

        [Header("色差")]
        [SerializeField] private float _chromaticAberration = 0f;

        protected override void Awake()
        {
            base.Awake();
            _materialInstance = UIShaderLib.GetMaterial(UIShaderLib.EDGE_GLOW);
            _targetImage.material = _materialInstance;
            UpdateMaterial();
        }

        private void Update()
        {
            if (_materialInstance != null)
            {
                _materialInstance.SetFloat("_Time", Time.time);
            }
        }

        public void UpdateMaterial()
        {
            if (_materialInstance == null) return;

            _materialInstance.SetColor("_GlowColor", _glowColor);
            _materialInstance.SetFloat("_GlowIntensity", _intensity);
            _materialInstance.SetFloat("_GlowWidth", _width);
            _materialInstance.SetFloat("_GlowFalloff", _falloff);
            _materialInstance.SetFloat("_Animated", _animated ? 1f : 0f);
            _materialInstance.SetFloat("_PulseSpeed", _pulseSpeed);
            _materialInstance.SetFloat("_PulseIntensity", _pulseIntensity);
            _materialInstance.SetFloat("_RotationSpeed", _rotationSpeed);
            _materialInstance.SetFloat("_WaveCount", _waveCount);
            _materialInstance.SetFloat("_WaveSpeed", _waveSpeed);
            _materialInstance.SetFloat("_WaveAmplitude", _waveAmplitude);
            _materialInstance.SetFloat("_InnerGlow", _innerGlow ? 1f : 0f);
            _materialInstance.SetColor("_InnerGlowColor", _innerGlowColor);
            _materialInstance.SetFloat("_InnerGlowWidth", _innerGlowWidth);
            _materialInstance.SetFloat("_ChromaticAberration", _chromaticAberration);
        }

        public void SetGlowColor(Color color) => _glowColor = color;
        public void SetIntensity(float intensity) => _intensity = intensity;
        public void SetPulse(float speed, float intensity) { _pulseSpeed = speed; _pulseIntensity = intensity; }
    }
}