using UnityEngine;

namespace TomCatRunner.Core.Data
{
    /// <summary>
    /// 主题配置文件 - 定义一个完整视觉主题的所有参数
    /// 每个赛季关联一个 ThemeProfile，支持运行时热切换
    /// </summary>
    [CreateAssetMenu(fileName = "ThemeProfile_", menuName = "TomCatRunner/Theme Profile", order = 1)]
    public class ThemeProfile : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _themeId;
        [SerializeField] private string _displayName;
        [SerializeField] private ThemeSeason _season = ThemeSeason.Spring;

        [Header("Color Palette")]
        [SerializeField] private ThemeColorPalette _colorPalette;

        [Header("Shader Parameters")]
        [SerializeField] private ShaderParameters _shaderParams = new();

        [Header("Audio")]
        [SerializeField] private AudioProfile _audioProfile;

        [Header("Prefabs & Assets")]
        [SerializeField] private ThemeAssets _assets = new();

        [Header("Particle Presets Override")]
        [SerializeField] private UIParticlePreset[] _particleOverrides;

        // Properties
        public string ThemeId => _themeId;
        public string DisplayName => _displayName;
        public ThemeSeason Season => _season;
        public ThemeColorPalette ColorPalette => _colorPalette;
        public ShaderParameters ShaderParams => _shaderParams;
        public AudioProfile AudioProfile => _audioProfile;
        public ThemeAssets Assets => _assets;
        public UIParticlePreset[] ParticleOverrides => _particleOverrides;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_themeId))
                _themeId = _displayName.Replace(" ", "_").ToLower();
        }
#endif
    }

    public enum ThemeSeason
    {
        Spring,
        Summer,
        Autumn,
        Winter,
        Special
    }

    /// <summary>
    /// Shader 统一参数 - 对应 UIShaderLib 的 5 个 Shader
    /// </summary>
    [Serializable]
    public class ShaderParameters
    {
        [Header("SDF Rounded Rect")]
        [Range(0f, 1f)] public float CornerRadius = 0.15f;
        [Range(0f, 0.5f)] public float EdgeFeather = 0.02f;

        [Header("Fluid Noise")]
        [Range(0f, 5f)] public float NoiseSpeed = 1.2f;
        [Range(0f, 3f)] public float NoiseScale = 1.8f;
        [Range(0f, 1f)] public float NoiseIntensity = 0.6f;
        [Range(1, 6)] public int NoiseOctaves = 4;

        [Header("Chromatic Aberration")]
        [Range(0f, 0.05f)] public float ChromaticStrength = 0.012f;
        [Range(0f, 2f)] public float ChromaticSpeed = 0.8f;

        [Header("Hologram Scan")]
        [Range(0f, 5f)] public float ScanlineSpeed = 2.5f;
        [Range(0f, 1f)] public float ScanlineWidth = 0.15f;
        [Range(0f, 1f)] public float ScanlineIntensity = 0.7f;
        public Color ScanlineColor = new Color(0.2f, 1f, 0.8f, 1f);

        [Header("Edge Glow")]
        [Range(0f, 2f)] public float GlowWidth = 0.3f;
        [Range(0f, 5f)] public float GlowPulseSpeed = 1.5f;
        [Range(0f, 1f)] public float GlowIntensity = 0.8f;
        public Gradient GlowColorGradient;

        public void ApplyToMaterial(Material mat)
        {
            if (mat == null) return;

            mat.SetFloat("_CornerRadius", CornerRadius);
            mat.SetFloat("_EdgeFeather", EdgeFeather);
            mat.SetFloat("_NoiseSpeed", NoiseSpeed);
            mat.SetFloat("_NoiseScale", NoiseScale);
            mat.SetFloat("_NoiseIntensity", NoiseIntensity);
            mat.SetInt("_NoiseOctaves", NoiseOctaves);
            mat.SetFloat("_ChromaticStrength", ChromaticStrength);
            mat.SetFloat("_ChromaticSpeed", ChromaticSpeed);
            mat.SetFloat("_ScanlineSpeed", ScanlineSpeed);
            mat.SetFloat("_ScanlineWidth", ScanlineWidth);
            mat.SetFloat("_ScanlineIntensity", ScanlineIntensity);
            mat.SetColor("_ScanlineColor", ScanlineColor);
            mat.SetFloat("_GlowWidth", GlowWidth);
            mat.SetFloat("_GlowPulseSpeed", GlowPulseSpeed);
            mat.SetFloat("_GlowIntensity", GlowIntensity);
            if (GlowColorGradient != null)
            {
                // Gradient 需要转为 Texture 或手动采样
                mat.SetColor("_GlowColorStart", GlowColorGradient.Evaluate(0f));
                mat.SetColor("_GlowColorEnd", GlowColorGradient.Evaluate(1f));
            }
        }

        public static ShaderParameters Lerp(ShaderParameters a, ShaderParameters b, float t)
        {
            var result = new ShaderParameters();
            result.CornerRadius = Mathf.Lerp(a.CornerRadius, b.CornerRadius, t);
            result.EdgeFeather = Mathf.Lerp(a.EdgeFeather, b.EdgeFeather, t);
            result.NoiseSpeed = Mathf.Lerp(a.NoiseSpeed, b.NoiseSpeed, t);
            result.NoiseScale = Mathf.Lerp(a.NoiseScale, b.NoiseScale, t);
            result.NoiseIntensity = Mathf.Lerp(a.NoiseIntensity, b.NoiseIntensity, t);
            result.NoiseOctaves = Mathf.RoundToInt(Mathf.Lerp(a.NoiseOctaves, b.NoiseOctaves, t));
            result.ChromaticStrength = Mathf.Lerp(a.ChromaticStrength, b.ChromaticStrength, t);
            result.ChromaticSpeed = Mathf.Lerp(a.ChromaticSpeed, b.ChromaticSpeed, t);
            result.ScanlineSpeed = Mathf.Lerp(a.ScanlineSpeed, b.ScanlineSpeed, t);
            result.ScanlineWidth = Mathf.Lerp(a.ScanlineWidth, b.ScanlineWidth, t);
            result.ScanlineIntensity = Mathf.Lerp(a.ScanlineIntensity, b.ScanlineIntensity, t);
            result.ScanlineColor = Color.Lerp(a.ScanlineColor, b.ScanlineColor, t);
            result.GlowWidth = Mathf.Lerp(a.GlowWidth, b.GlowWidth, t);
            result.GlowPulseSpeed = Mathf.Lerp(a.GlowPulseSpeed, b.GlowPulseSpeed, t);
            result.GlowIntensity = Mathf.Lerp(a.GlowIntensity, b.GlowIntensity, t);
            if (a.GlowColorGradient != null && b.GlowColorGradient != null)
            {
                result.GlowColorGradient = new Gradient();
                var keys1 = a.GlowColorGradient.colorKeys;
                var keys2 = b.GlowColorGradient.colorKeys;
                var newKeys = new GradientColorKey[keys1.Length];
                for (int i = 0; i < keys1.Length; i++)
                {
                    newKeys[i] = new GradientColorKey(
                        Color.Lerp(keys1[i].color, keys2[i].color, t),
                        Mathf.Lerp(keys1[i].time, keys2[i].time, t)
                    );
                }
                result.GlowColorGradient.colorKeys = newKeys;
            }
            return result;
        }
    }

    /// <summary>
    /// 音频配置
    /// </summary>
    [Serializable]
    public class AudioProfile
    {
        public AudioClip BGMLoop;
        public AudioClip AmbientLoop;
        public AudioClip UIClick;
        public AudioClip UIHover;
        public AudioClip UITransition;
        public AudioClip RewardPopup;
        public AudioClip LevelUp;
        [Range(0f, 1f)] public float BGMPitch = 1f;
        [Range(0f, 1f)] public float AmbientVolume = 0.4f;
    }

    /// <summary>
    /// 主题资产引用
    /// </summary>
    [Serializable]
    public class ThemeAssets
    {
        public GameObject CharacterSkin;
        public GameObject EnvironmentDecoration;
        public GameObject[] ChunkDecorations; // 对应不同 Chunk 类型的装饰
        public Material SkyboxMaterial;
        public Texture2D LoadingBackground;
        public Sprite SeasonLogo;
        public Sprite SeasonBanner;
    }
}