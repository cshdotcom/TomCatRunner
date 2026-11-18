using System;
using UnityEngine;
using UnityEngine.Rendering;
using TomCatRunner.Core.Data;
using TomCatRunner.Core.Season;
using TomCatRunner.Common.Reactive;

namespace TomCatRunner.Gameplay.World
{
    /// <summary>
    /// 运行时主题 - 一套世界配色（天空、雾、草地、道路、树木、UI 强调色）
    /// 默认提供春夏秋冬四套 + 从 SeasonConfig/ThemeProfile 热切换
    /// </summary>
    public class GameTheme
    {
        public string Id;
        public string DisplayName;
        public Color SkyTop = new Color(0.35f, 0.62f, 0.95f);
        public Color SkyBottom = new Color(0.85f, 0.93f, 1f);
        public Color SunColor = new Color(1f, 0.96f, 0.85f);
        public float SunIntensity = 1.15f;
        public Color Fog = new Color(0.82f, 0.9f, 0.98f);
        public Color Grass = new Color(0.45f, 0.72f, 0.36f);
        public Color GrassDark = new Color(0.33f, 0.6f, 0.3f);
        public Color Road = new Color(0.82f, 0.78f, 0.72f);
        public Color Curb = new Color(0.9f, 0.86f, 0.8f);
        public Color TreeTrunk = new Color(0.45f, 0.33f, 0.22f);
        public Color TreeLeaf1 = new Color(0.36f, 0.68f, 0.34f);
        public Color TreeLeaf2 = new Color(0.3f, 0.58f, 0.28f);
        public Color Accent = new Color(0.98f, 0.45f, 0.2f);   // UI 强调色
        public Color LampGlow = new Color(1f, 0.9f, 0.6f);

        public static GameTheme SpringSakura() => new GameTheme
        {
            Id = "spring_sakura",
            DisplayName = "春 · 樱花猫季",
            SkyTop = new Color(0.42f, 0.65f, 0.95f),
            SkyBottom = new Color(0.98f, 0.9f, 0.94f),
            Fog = new Color(0.95f, 0.88f, 0.92f),
            Grass = new Color(0.5f, 0.75f, 0.42f),
            GrassDark = new Color(0.38f, 0.63f, 0.35f),
            Road = new Color(0.85f, 0.8f, 0.76f),
            TreeLeaf1 = new Color(0.95f, 0.72f, 0.8f),   // 樱花粉
            TreeLeaf2 = new Color(0.9f, 0.6f, 0.72f),
            Accent = new Color(0.96f, 0.45f, 0.62f),
        };

        public static GameTheme SummerOcean() => new GameTheme
        {
            Id = "summer_ocean",
            DisplayName = "夏 · 海风冲浪季",
            SkyTop = new Color(0.2f, 0.55f, 0.95f),
            SkyBottom = new Color(0.75f, 0.92f, 1f),
            Fog = new Color(0.8f, 0.9f, 0.98f),
            Grass = new Color(0.95f, 0.85f, 0.55f),      // 沙滩色
            GrassDark = new Color(0.88f, 0.76f, 0.45f),
            Road = new Color(0.9f, 0.88f, 0.82f),
            TreeLeaf1 = new Color(0.35f, 0.72f, 0.4f),
            TreeLeaf2 = new Color(0.28f, 0.62f, 0.35f),
            Accent = new Color(0.15f, 0.65f, 0.95f),
        };

        public static GameTheme AutumnMaple() => new GameTheme
        {
            Id = "autumn_maple",
            DisplayName = "秋 · 枫叶物语",
            SkyTop = new Color(0.5f, 0.55f, 0.85f),
            SkyBottom = new Color(1f, 0.88f, 0.72f),
            Fog = new Color(0.95f, 0.85f, 0.75f),
            Grass = new Color(0.72f, 0.6f, 0.35f),
            GrassDark = new Color(0.6f, 0.48f, 0.28f),
            Road = new Color(0.78f, 0.7f, 0.62f),
            TreeLeaf1 = new Color(0.9f, 0.45f, 0.2f),    // 枫叶红
            TreeLeaf2 = new Color(0.85f, 0.6f, 0.18f),
            Accent = new Color(0.9f, 0.45f, 0.15f),
        };

        public static GameTheme WinterSnow() => new GameTheme
        {
            Id = "winter_snow",
            DisplayName = "冬 · 雪夜灯语",
            SkyTop = new Color(0.25f, 0.32f, 0.5f),
            SkyBottom = new Color(0.65f, 0.72f, 0.88f),
            SunColor = new Color(0.85f, 0.88f, 1f),
            SunIntensity = 0.9f,
            Fog = new Color(0.78f, 0.82f, 0.92f),
            Grass = new Color(0.92f, 0.94f, 0.98f),      // 雪
            GrassDark = new Color(0.82f, 0.86f, 0.94f),
            Road = new Color(0.75f, 0.78f, 0.85f),
            TreeLeaf1 = new Color(0.55f, 0.68f, 0.6f),   // 松柏
            TreeLeaf2 = new Color(0.45f, 0.58f, 0.52f),
            Accent = new Color(0.4f, 0.6f, 0.95f),
            LampGlow = new Color(1f, 0.82f, 0.5f),
        };

        /// <summary>从 SeasonConfig 的 ThemeProfile 派生</summary>
        public static GameTheme FromThemeProfile(ThemeProfile profile)
        {
            if (profile?.ColorPalette == null) return SpringSakura();
            var t = SpringSakura();
            t.Id = profile.ThemeId ?? "season";
            t.DisplayName = profile.DisplayName ?? t.DisplayName;
            var p = profile.ColorPalette;
            t.Accent = p.Primary;
            t.Fog = Color.Lerp(p.Background, p.Primary, 0.18f);
            t.Road = Color.Lerp(p.Surface, Color.gray, 0.12f);
            t.Curb = Color.Lerp(p.Surface, p.Secondary, 0.3f);
            t.Grass = Color.Lerp(p.Tertiary, Color.black, 0.12f);
            t.GrassDark = Color.Lerp(p.Tertiary, Color.black, 0.28f);
            t.TreeLeaf1 = Color.Lerp(p.Secondary, p.Tertiary, 0.5f);
            t.TreeLeaf2 = Color.Lerp(p.Secondary, Color.black, 0.15f);
            return t;
        }
    }

    /// <summary>
    /// 主题运行时 - 应用主题到天空/雾/光照，并广播主题变更
    /// （ChunkContentBuilder、UI、WorldBuilder 订阅 ApplyRequested 重新着色）
    /// </summary>
    public class ThemeRuntime
    {
        public GameTheme Current { get; private set; }

        /// <summary>主题应用请求（含是否需要重建已有物件颜色）</summary>
        public event Action<GameTheme> ApplyRequested;

        private Material _skyboxMaterial;
        private Light _sun;
        private readonly System.Random _rng = new System.Random();

        public ThemeRuntime()
        {
            Current = GameTheme.SpringSakura();
        }

        public void SetSun(Light sun) => _sun = sun;

        /// <summary>初始化：创建天空盒材质并应用当前主题</summary>
        public void Initialize()
        {
            var shader = Shader.Find("Skybox/Procedural");
            if (shader != null)
            {
                _skyboxMaterial = new Material(shader);
                RenderSettings.skybox = _skyboxMaterial;
            }
            ApplyCurrent();
        }

        /// <summary>切换主题（可来自赛季配置）</summary>
        public void Switch(GameTheme theme)
        {
            if (theme == null) return;
            Current = theme;
            ApplyCurrent();
        }

        public void SwitchFromSeason(SeasonConfig season)
        {
            if (season?.ThemeProfile != null)
                Switch(GameTheme.FromThemeProfile(season.ThemeProfile));
        }

        private void ApplyCurrent()
        {
            var t = Current;

            if (_skyboxMaterial != null)
            {
                _skyboxMaterial.SetColor("_TopColor", t.SkyTop);
                _skyboxMaterial.SetColor("_BottomColor", t.SkyBottom);
                _skyboxMaterial.SetColor("_SunColor", t.SunColor);
                _skyboxMaterial.SetFloat("_SunSize", 0.045f);
                _skyboxMaterial.SetFloat("_Exposure", 1.08f);
                _skyboxMaterial.SetFloat("_AtmosphereThickness", 0.95f);
                DynamicGI.UpdateEnvironment();
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 240f;
            RenderSettings.fogColor = t.Fog;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(t.SkyTop, Color.white, 0.35f);
            RenderSettings.ambientEquatorColor = t.Fog;
            RenderSettings.ambientGroundColor = t.GrassDark;

            if (_sun != null)
            {
                _sun.color = t.SunColor;
                _sun.intensity = t.SunIntensity;
            }

            ApplyRequested?.Invoke(t);
        }

        /// <summary>按赛季周期（时间）选择主题，供菜单展示用</summary>
        public GameTheme PickByDate(System.DateTime utcNow)
        {
            switch (utcNow.Month)
            {
                case 3: case 4: case 5: return GameTheme.SpringSakura();
                case 6: case 7: case 8: return GameTheme.SummerOcean();
                case 9: case 10: case 11: return GameTheme.AutumnMaple();
                default: return GameTheme.WinterSnow();
            }
        }

        /// <summary>订阅赛季系统：赛季/主题变化时自动应用</summary>
        public void BindSeasonManager(SeasonManager seasonManager, MessageBroker broker)
        {
            if (seasonManager != null)
            {
                seasonManager.OnSeasonChanged += season =>
                {
                    if (season?.ThemeProfile != null)
                        Switch(GameTheme.FromThemeProfile(season.ThemeProfile));
                    else
                        Switch(PickByDate(System.DateTime.UtcNow));
                };
            }
            if (broker != null)
            {
                broker.Subscribe<SeasonManager.SeasonChangedMessage>(msg =>
                {
                    // OnSeasonChanged 可能已处理；此处兜底（幂等）
                    if (msg.Season?.ThemeProfile == null)
                        Switch(PickByDate(System.DateTime.UtcNow));
                });
            }
        }
    }
}
