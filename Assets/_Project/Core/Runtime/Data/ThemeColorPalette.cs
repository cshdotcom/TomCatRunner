using UnityEngine;

namespace TomCatRunner.Core.Data
{
    /// <summary>
    /// 主题色板 - 定义一套完整的 UI 配色方案
    /// 支持亮/暗模式、动态插值、Material Design 3 语义色彩
    /// </summary>
    [CreateAssetMenu(fileName = "ThemeColorPalette_", menuName = "TomCatRunner/Theme Color Palette", order = 2)]
    public class ThemeColorPalette : ScriptableObject
    {
        [Header("Brand Colors (品牌色)")]
        [SerializeField] private Color _primary = new Color(0.12f, 0.58f, 0.95f);
        [SerializeField] private Color _primaryVariant = new Color(0.06f, 0.42f, 0.72f);
        [SerializeField] private Color _secondary = new Color(0.95f, 0.45f, 0.12f);
        [SerializeField] private Color _secondaryVariant = new Color(0.72f, 0.32f, 0.06f);
        [SerializeField] private Color _tertiary = new Color(0.12f, 0.75f, 0.55f);

        [Header("Surface Colors (表面色)")]
        [SerializeField] private Color _surface = new Color(0.98f, 0.98f, 0.99f);
        [SerializeField] private Color _surfaceVariant = new Color(0.92f, 0.93f, 0.95f);
        [SerializeField] private Color _background = new Color(1f, 1f, 1f);
        [SerializeField] private Color _backgroundVariant = new Color(0.96f, 0.97f, 0.98f);

        [Header("Error Colors (错误色)")]
        [SerializeField] private Color _error = new Color(0.85f, 0.18f, 0.18f);
        [SerializeField] private Color _errorContainer = new Color(1f, 0.85f, 0.85f);

        [Header("On Colors (前景色 - 用于文字/图标)")]
        [SerializeField] private Color _onPrimary = Color.white;
        [SerializeField] private Color _onSecondary = Color.white;
        [SerializeField] private Color _onTertiary = Color.white;
        [SerializeField] private Color _onSurface = new Color(0.1f, 0.1f, 0.12f);
        [SerializeField] private Color _onSurfaceVariant = new Color(0.3f, 0.3f, 0.35f);
        [SerializeField] private Color _onBackground = new Color(0.1f, 0.1f, 0.12f);
        [SerializeField] private Color _onError = Color.white;

        [Header("Outline & Shadow (描边/阴影)")]
        [SerializeField] private Color _outline = new Color(0.1f, 0.1f, 0.12f, 0.12f);
        [SerializeField] private Color _shadow = new Color(0f, 0f, 0f, 0.2f);
        [SerializeField] private Color _shadowStrong = new Color(0f, 0f, 0f, 0.4f);

        [Header("State Overlays (状态覆盖层)")]
        [SerializeField] private Color _hoverOverlay = new Color(0f, 0f, 0f, 0.08f);
        [SerializeField] private Color _pressedOverlay = new Color(0f, 0f, 0f, 0.12f);
        [SerializeField] private Color _focusOverlay = new Color(0.12f, 0.58f, 0.95f, 0.2f);
        [SerializeField] private Color _disabledOverlay = new Color(0.1f, 0.1f, 0.12f, 0.38f);

        [Header("Gradients (渐变)")]
        [SerializeField] private Gradient _primaryGradient;
        [SerializeField] private Gradient _secondaryGradient;
        [SerializeField] private Gradient _surfaceGradient;
        [SerializeField] private Gradient _backgroundGradient;
        [SerializeField] private Gradient _glowGradient;

        [Header("Particle Colors (粒子色)")]
        [SerializeField] private Gradient _particlePrimary;
        [SerializeField] private Gradient _particleSecondary;
        [SerializeField] private Gradient _particleAccent;
        [SerializeField] private Gradient _particleMagic;

        // Properties
        public Color Primary => _primary;
        public Color PrimaryVariant => _primaryVariant;
        public Color Secondary => _secondary;
        public Color SecondaryVariant => _secondaryVariant;
        public Color Tertiary => _tertiary;

        public Color Surface => _surface;
        public Color SurfaceVariant => _surfaceVariant;
        public Color Background => _background;
        public Color BackgroundVariant => _backgroundVariant;

        public Color Error => _error;
        public Color ErrorContainer => _errorContainer;

        public Color OnPrimary => _onPrimary;
        public Color OnSecondary => _onSecondary;
        public Color OnTertiary => _onTertiary;
        public Color OnSurface => _onSurface;
        public Color OnSurfaceVariant => _onSurfaceVariant;
        public Color OnBackground => _onBackground;
        public Color OnError => _onError;

        public Color Outline => _outline;
        public Color Shadow => _shadow;
        public Color ShadowStrong => _shadowStrong;

        public Color HoverOverlay => _hoverOverlay;
        public Color PressedOverlay => _pressedOverlay;
        public Color FocusOverlay => _focusOverlay;
        public Color DisabledOverlay => _disabledOverlay;

        public Gradient PrimaryGradient => _primaryGradient;
        public Gradient SecondaryGradient => _secondaryGradient;
        public Gradient SurfaceGradient => _surfaceGradient;
        public Gradient BackgroundGradient => _backgroundGradient;
        public Gradient GlowGradient => _glowGradient;

        public Gradient ParticlePrimary => _particlePrimary;
        public Gradient ParticleSecondary => _particleSecondary;
        public Gradient ParticleAccent => _particleAccent;
        public Gradient ParticleMagic => _particleMagic;

        /// <summary>
        /// 获取语义色彩 - 兼容 Material Design 3
        /// </summary>
        public Color GetSemanticColor(SemanticColor role)
        {
            return role switch
            {
                SemanticColor.Primary => _primary,
                SemanticColor.PrimaryVariant => _primaryVariant,
                SemanticColor.PrimaryContainer => Color.Lerp(_surface, _primary, 0.12f),
                SemanticColor.OnPrimary => _onPrimary,
                SemanticColor.OnPrimaryContainer => _onSurface,

                SemanticColor.Secondary => _secondary,
                SemanticColor.SecondaryVariant => _secondaryVariant,
                SemanticColor.SecondaryContainer => Color.Lerp(_surface, _secondary, 0.12f),
                SemanticColor.OnSecondary => _onSecondary,
                SemanticColor.OnSecondaryContainer => _onSurface,

                SemanticColor.Tertiary => _tertiary,
                SemanticColor.TertiaryContainer => Color.Lerp(_surface, _tertiary, 0.12f),
                SemanticColor.OnTertiary => _onTertiary,
                SemanticColor.OnTertiaryContainer => _onSurface,

                SemanticColor.Error => _error,
                SemanticColor.ErrorContainer => _errorContainer,
                SemanticColor.OnError => _onError,
                SemanticColor.OnErrorContainer => _onSurface,

                SemanticColor.Surface => _surface,
                SemanticColor.SurfaceVariant => _surfaceVariant,
                SemanticColor.SurfaceDim => Color.Lerp(_surface, _onSurface, 0.03f),
                SemanticColor.SurfaceBright => Color.Lerp(_surface, Color.white, 0.1f),
                SemanticColor.OnSurface => _onSurface,
                SemanticColor.OnSurfaceVariant => _onSurfaceVariant,

                SemanticColor.Background => _background,
                SemanticColor.OnBackground => _onBackground,

                SemanticColor.Outline => _outline,
                SemanticColor.OutlineVariant => Color.Lerp(_outline, Color.clear, 0.5f),
                SemanticColor.Shadow => _shadow,

                SemanticColor.InverseSurface => _onSurface,
                SemanticColor.InverseOnSurface => _surface,
                SemanticColor.InversePrimary => _primaryVariant,

                _ => _surface
            };
        }

        /// <summary>
        /// 两个色板插值 - 用于主题切换过渡动画
        /// </summary>
        public static ThemeColorPalette Lerp(ThemeColorPalette a, ThemeColorPalette b, float t)
        {
            var result = CreateInstance<ThemeColorPalette>();

            result._primary = Color.Lerp(a._primary, b._primary, t);
            result._primaryVariant = Color.Lerp(a._primaryVariant, b._primaryVariant, t);
            result._secondary = Color.Lerp(a._secondary, b._secondary, t);
            result._secondaryVariant = Color.Lerp(a._secondaryVariant, b._secondaryVariant, t);
            result._tertiary = Color.Lerp(a._tertiary, b._tertiary, t);

            result._surface = Color.Lerp(a._surface, b._surface, t);
            result._surfaceVariant = Color.Lerp(a._surfaceVariant, b._surfaceVariant, t);
            result._background = Color.Lerp(a._background, b._background, t);
            result._backgroundVariant = Color.Lerp(a._backgroundVariant, b._backgroundVariant, t);

            result._error = Color.Lerp(a._error, b._error, t);
            result._errorContainer = Color.Lerp(a._errorContainer, b._errorContainer, t);

            result._onPrimary = Color.Lerp(a._onPrimary, b._onPrimary, t);
            result._onSecondary = Color.Lerp(a._onSecondary, b._onSecondary, t);
            result._onTertiary = Color.Lerp(a._onTertiary, b._onTertiary, t);
            result._onSurface = Color.Lerp(a._onSurface, b._onSurface, t);
            result._onSurfaceVariant = Color.Lerp(a._onSurfaceVariant, b._onSurfaceVariant, t);
            result._onBackground = Color.Lerp(a._onBackground, b._onBackground, t);
            result._onError = Color.Lerp(a._onError, b._onError, t);

            result._outline = Color.Lerp(a._outline, b._outline, t);
            result._shadow = Color.Lerp(a._shadow, b._shadow, t);
            result._shadowStrong = Color.Lerp(a._shadowStrong, b._shadowStrong, t);

            result._hoverOverlay = Color.Lerp(a._hoverOverlay, b._hoverOverlay, t);
            result._pressedOverlay = Color.Lerp(a._pressedOverlay, b._pressedOverlay, t);
            result._focusOverlay = Color.Lerp(a._focusOverlay, b._focusOverlay, t);
            result._disabledOverlay = Color.Lerp(a._disabledOverlay, b._disabledOverlay, t);

            // Gradient 插值需要重建
            result._primaryGradient = GradientLerp(a._primaryGradient, b._primaryGradient, t);
            result._secondaryGradient = GradientLerp(a._secondaryGradient, b._secondaryGradient, t);
            result._surfaceGradient = GradientLerp(a._surfaceGradient, b._surfaceGradient, t);
            result._backgroundGradient = GradientLerp(a._backgroundGradient, b._backgroundGradient, t);
            result._glowGradient = GradientLerp(a._glowGradient, b._glowGradient, t);

            result._particlePrimary = GradientLerp(a._particlePrimary, b._particlePrimary, t);
            result._particleSecondary = GradientLerp(a._particleSecondary, b._particleSecondary, t);
            result._particleAccent = GradientLerp(a._particleAccent, b._particleAccent, t);
            result._particleMagic = GradientLerp(a._particleMagic, b._particleMagic, t);

            return result;
        }

        private static Gradient GradientLerp(Gradient a, Gradient b, float t)
        {
            if (a == null) return b;
            if (b == null) return a;

            var result = new Gradient();
            var keysA = a.colorKeys;
            var keysB = b.colorKeys;
            int count = Mathf.Max(keysA.Length, keysB.Length);
            var newKeys = new GradientColorKey[count];

            for (int i = 0; i < count; i++)
            {
                var keyA = keysA.Length > i ? keysA[i] : keysA[keysA.Length - 1];
                var keyB = keysB.Length > i ? keysB[i] : keysB[keysB.Length - 1];

                newKeys[i] = new GradientColorKey(
                    Color.Lerp(keyA.color, keyB.color, t),
                    Mathf.Lerp(keyA.time, keyB.time, t)
                );
            }

            result.colorKeys = newKeys;

            // Alpha keys
            var alphaKeysA = a.alphaKeys;
            var alphaKeysB = b.alphaKeys;
            int alphaCount = Mathf.Max(alphaKeysA.Length, alphaKeysB.Length);
            var newAlphaKeys = new GradientAlphaKey[alphaCount];

            for (int i = 0; i < alphaCount; i++)
            {
                var keyA = alphaKeysA.Length > i ? alphaKeysA[i] : alphaKeysA[alphaKeysA.Length - 1];
                var keyB = alphaKeysB.Length > i ? alphaKeysB[i] : alphaKeysB[alphaKeysB.Length - 1];

                newAlphaKeys[i] = new GradientAlphaKey(
                    Mathf.Lerp(keyA.alpha, keyB.alpha, t),
                    Mathf.Lerp(keyA.time, keyB.time, t)
                );
            }

            result.alphaKeys = newAlphaKeys;

            return result;
        }
    }

    /// <summary>
    /// Material Design 3 语义色彩角色
    /// </summary>
    public enum SemanticColor
    {
        // Primary
        Primary,
        PrimaryVariant,
        PrimaryContainer,
        OnPrimary,
        OnPrimaryContainer,

        // Secondary
        Secondary,
        SecondaryVariant,
        SecondaryContainer,
        OnSecondary,
        OnSecondaryContainer,

        // Tertiary
        Tertiary,
        TertiaryContainer,
        OnTertiary,
        OnTertiaryContainer,

        // Error
        Error,
        ErrorContainer,
        OnError,
        OnErrorContainer,

        // Surface
        Surface,
        SurfaceVariant,
        SurfaceDim,
        SurfaceBright,
        OnSurface,
        OnSurfaceVariant,

        // Background
        Background,
        OnBackground,

        // Outline/Shadow
        Outline,
        OutlineVariant,
        Shadow,

        // Inverse
        InverseSurface,
        InverseOnSurface,
        InversePrimary
    }
}