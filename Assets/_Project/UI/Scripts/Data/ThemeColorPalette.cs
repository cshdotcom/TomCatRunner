using UnityEngine;

namespace TomCatRunner.UI
{
    /// <summary>
    /// 主题色板 - 定义一套完整的 UI 色彩系统
    /// 支持亮/暗模式、高对比度、无障碍适配
    /// </summary>
    [CreateAssetMenu(fileName = "ThemeColorPalette", menuName = "TomCatRunner/UI/Theme Color Palette")]
    public class ThemeColorPalette : ScriptableObject
    {
        [Header("基本信息")]
        public string paletteId;
        public string displayName;
        public ThemeProfile linkedTheme;  // 关联的赛季主题

        [Header("核心色彩 (Material Design 3 风格)")]

        // Primary - 主品牌色
        public Color primary = new Color(0.0f, 0.7f, 1.0f);           // 霓虹青
        public Color onPrimary = Color.white;
        public Color primaryContainer = new Color(0.0f, 0.3f, 0.5f);
        public Color onPrimaryContainer = Color.white;

        // Secondary - 次要品牌色
        public Color secondary = new Color(1.0f, 0.0f, 0.5f);          // 霓虹粉
        public Color onSecondary = Color.white;
        public Color secondaryContainer = new Color(0.5f, 0.0f, 0.25f);
        public Color onSecondaryContainer = Color.white;

        // Tertiary - 第三强调色
        public Color tertiary = new Color(0.5f, 1.0f, 0.0f);           // 霓虹绿
        public Color onTertiary = Color.black;
        public Color tertiaryContainer = new Color(0.2f, 0.4f, 0.0f);
        public Color onTertiaryContainer = Color.white;

        // Error - 错误/危险色
        public Color error = new Color(1.0f, 0.2f, 0.2f);
        public Color onError = Color.white;
        public Color errorContainer = new Color(0.5f, 0.1f, 0.1f);
        public Color onErrorContainer = Color.white;

        [Header("表面色彩")]

        // Background - 背景
        public Color background = new Color(0.05f, 0.05f, 0.1f);       // 深空黑
        public Color onBackground = new Color(0.95f, 0.95f, 1.0f);

        // Surface - 卡片/面板背景
        public Color surface = new Color(0.1f, 0.1f, 0.15f, 0.9f);     // 半透明深灰
        public Color onSurface = new Color(0.9f, 0.9f, 0.95f);
        public Color surfaceVariant = new Color(0.15f, 0.15f, 0.2f, 0.85f);
        public Color onSurfaceVariant = new Color(0.7f, 0.7f, 0.8f);

        // Outline - 边框/分割线
        public Color outline = new Color(0.3f, 0.4f, 0.5f, 0.5f);
        public Color outlineVariant = new Color(0.2f, 0.3f, 0.4f, 0.3f);

        [Header("特效色彩")]

        // Glow - 发光/高亮
        public Color glowPrimary = new Color(0.0f, 1.0f, 1.0f, 0.8f);
        public Color glowSecondary = new Color(1.0f, 0.0f, 1.0f, 0.8f);
        public Color glowTertiary = new Color(0.5f, 1.0f, 0.0f, 0.8f);
        public Color glowWarm = new Color(1.0f, 0.5f, 0.0f, 0.8f);

        // Particle - 粒子系统
        public Color particlePrimary = new Color(0.0f, 1.0f, 1.0f);
        public Color particleSecondary = new Color(1.0f, 0.0f, 1.0f);
        public Color particleTertiary = new Color(1.0f, 1.0f, 0.0f);
        public Color particleSparkle = new Color(1.0f, 1.0f, 1.0f, 0.9f);

        // Gradient - 渐变
        public Gradient primaryGradient;
        public Gradient secondaryGradient;
        public Gradient backgroundGradient;
        public Gradient glowGradient;

        [Header("状态色彩")]
        public Color success = new Color(0.0f, 0.9f, 0.3f);
        public Color warning = new Color(1.0f, 0.7f, 0.0f);
        public Color info = new Color(0.0f, 0.6f, 1.0f);
        public Color disabled = new Color(0.4f, 0.4f, 0.45f, 0.5f);

        [Header("稀有度色彩")]
        public Color rarityCommon = new Color(0.6f, 0.6f, 0.6f);
        public Color rarityUncommon = new Color(0.2f, 0.8f, 0.2f);
        public Color rarityRare = new Color(0.2f, 0.5f, 1.0f);
        public Color rarityEpic = new Color(0.7f, 0.2f, 1.0f);
        public Color rarityLegendary = new Color(1.0f, 0.6f, 0.0f);
        public Color rarityMythic = new Color(1.0f, 0.0f, 0.5f);

        [Header("动态色彩 (运行时计算)")]
        [NonSerialized] public Color dynamicPrimary;
        [NonSerialized] public Color dynamicSecondary;
        [NonSerialized] public Color dynamicBackground;

        /// <summary>
        /// 初始化动态色彩 (根据时间/进度插值)
        /// </summary>
        public void InitializeDynamicColors(float timeFactor = 0f)
        {
            // 呼吸效果
            float pulse = Mathf.Sin(timeFactor * 2f) * 0.1f + 0.9f;

            dynamicPrimary = Color.Lerp(primaryContainer, primary, pulse);
            dynamicSecondary = Color.Lerp(secondaryContainer, secondary, pulse);
            dynamicBackground = Color.Lerp(background, background * 1.2f, pulse * 0.1f);
        }

        /// <summary>
        /// 获取稀有度对应的颜色
        /// </summary>
        public Color GetRarityColor(Rarity rarity)
        {
            return rarity switch
            {
                Rarity.Common => rarityCommon,
                Rarity.Uncommon => rarityUncommon,
                Rarity.Rare => rarityRare,
                Rarity.Epic => rarityEpic,
                Rarity.Legendary => rarityLegendary,
                Rarity.Mythic => rarityMythic,
                _ => rarityCommon
            };
        }

        /// <summary>
        /// 获取稀有度对应的发光颜色
        /// </summary>
        public Color GetRarityGlowColor(Rarity rarity)
        {
            Color baseColor = GetRarityColor(rarity);
            return new Color(baseColor.r, baseColor.g, baseColor.b, 0.6f);
        }

        /// <summary>
        /// 创建材质属性块 (用于 GPU Instancing)
        /// </summary>
        public MaterialPropertyBlock CreatePropertyBlock(bool useDynamic = false)
        {
            var block = new MaterialPropertyBlock();

            var primaryColor = useDynamic ? dynamicPrimary : primary;
            var secondaryColor = useDynamic ? dynamicSecondary : secondary;
            var bgColor = useDynamic ? dynamicBackground : background;

            block.SetColor("_PrimaryColor", primaryColor);
            block.SetColor("_SecondaryColor", secondaryColor);
            block.SetColor("_BackgroundColor", bgColor);
            block.SetColor("_SurfaceColor", surface);
            block.SetColor("_GlowColor", glowPrimary);
            block.SetColor("_OutlineColor", outline);
            block.SetFloat("_GlowIntensity", 1f);
            block.SetFloat("_Metallic", 0f);
            block.SetFloat("_Smoothness", 0.5f);

            return block;
        }

        /// <summary>
        /// 根据另一个色板插值 (用于主题过渡动画)
        /// </summary>
        public void LerpFrom(ThemeColorPalette from, float t)
        {
            primary = Color.Lerp(from.primary, primary, t);
            secondary = Color.Lerp(from.secondary, secondary, t);
            tertiary = Color.Lerp(from.tertiary, tertiary, t);
            background = Color.Lerp(from.background, background, t);
            surface = Color.Lerp(from.surface, surface, t);
            glowPrimary = Color.Lerp(from.glowPrimary, glowPrimary, t);
            glowSecondary = Color.Lerp(from.glowSecondary, glowSecondary, t);
            outline = Color.Lerp(from.outline, outline, t);
        }

        private void OnValidate()
        {
            // 确保渐变已初始化
            if (primaryGradient == null || primaryGradient.colorKeys.Length == 0)
            {
                primaryGradient = new Gradient
                {
                    colorKeys = new GradientColorKey[]
                    {
                        new GradientColorKey(primaryContainer, 0f),
                        new GradientColorKey(primary, 0.5f),
                        new GradientColorKey(primaryContainer, 1f)
                    },
                    alphaKeys = new GradientAlphaKey[]
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(1f, 1f)
                    }
                };
            }

            if (backgroundGradient == null || backgroundGradient.colorKeys.Length == 0)
            {
                backgroundGradient = new Gradient
                {
                    colorKeys = new GradientColorKey[]
                    {
                        new GradientColorKey(background, 0f),
                        new GradientColorKey(surface, 1f)
                    },
                    alphaKeys = new GradientAlphaKey[]
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(1f, 1f)
                    }
                };
            }

            if (glowGradient == null || glowGradient.colorKeys.Length == 0)
            {
                glowGradient = new Gradient
                {
                    colorKeys = new GradientColorKey[]
                    {
                        new GradientColorKey(glowPrimary, 0f),
                        new GradientColorKey(glowSecondary, 0.5f),
                        new GradientColorKey(glowTertiary, 1f)
                    },
                    alphaKeys = new GradientAlphaKey[]
                    {
                        new GradientAlphaKey(0.8f, 0f),
                        new GradientAlphaKey(1f, 0.5f),
                        new GradientAlphaKey(0.8f, 1f)
                    }
                };
            }
        }

        /// <summary>
        /// 创建预设色板
        /// </summary>
        public static ThemeColorPalette CreateNeonCyber()
        {
            var palette = CreateInstance<ThemeColorPalette>();
            palette.paletteId = "neon_cyber";
            palette.displayName = "霓虹赛博";

            palette.primary = new Color(0.0f, 0.9f, 1.0f);           // 青色
            palette.secondary = new Color(1.0f, 0.0f, 0.6f);         // 品红
            palette.tertiary = new Color(0.6f, 1.0f, 0.0f);          // 酸性绿
            palette.background = new Color(0.02f, 0.02f, 0.08f);
            palette.surface = new Color(0.08f, 0.08f, 0.15f, 0.9f);
            palette.glowPrimary = new Color(0.0f, 1.0f, 1.0f, 0.9f);
            palette.glowSecondary = new Color(1.0f, 0.0f, 1.0f, 0.9f);
            palette.outline = new Color(0.0f, 0.8f, 1.0f, 0.4f);

            return palette;
        }

        public static ThemeColorPalette CreateHealingForest()
        {
            var palette = CreateInstance<ThemeColorPalette>();
            palette.paletteId = "healing_forest";
            palette.displayName = "治愈森林";

            palette.primary = new Color(0.3f, 0.8f, 0.4f);           // 森林绿
            palette.secondary = new Color(0.9f, 0.7f, 0.2f);         // 温暖金
            palette.tertiary = new Color(0.4f, 0.7f, 1.0f);          // 天空蓝
            palette.background = new Color(0.05f, 0.1f, 0.05f);
            palette.surface = new Color(0.1f, 0.15f, 0.1f, 0.9f);
            palette.glowPrimary = new Color(0.3f, 1.0f, 0.4f, 0.7f);
            palette.glowSecondary = new Color(1.0f, 0.9f, 0.3f, 0.7f);
            palette.outline = new Color(0.3f, 0.7f, 0.4f, 0.4f);

            return palette;
        }

        public static ThemeColorPalette CreateCyberpunk()
        {
            var palette = CreateInstance<ThemeColorPalette>();
            palette.paletteId = "cyberpunk";
            palette.displayName = "赛博朋克";

            palette.primary = new Color(1.0f, 0.0f, 0.8f);           // 热粉
            palette.secondary = new Color(0.0f, 1.0f, 1.0f);         // 电蓝
            palette.tertiary = new Color(1.0f, 0.5f, 0.0f);          // 橙红
            palette.background = new Color(0.08f, 0.0f, 0.12f);
            palette.surface = new Color(0.15f, 0.05f, 0.2f, 0.9f);
            palette.glowPrimary = new Color(1.0f, 0.0f, 1.0f, 0.9f);
            palette.glowSecondary = new Color(0.0f, 1.0f, 1.0f, 0.9f);
            palette.outline = new Color(1.0f, 0.0f, 0.8f, 0.5f);

            return palette;
        }
    }

    // 复用 SeasonData 中的 Rarity 枚举
    public enum Rarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
        Mythic
    }
}