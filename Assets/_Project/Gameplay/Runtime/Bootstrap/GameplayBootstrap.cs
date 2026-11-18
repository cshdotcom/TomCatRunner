using System;
using UnityEngine;
using VContainer;
using TomCatRunner.Common.Reactive;
using TomCatRunner.Core.Level;
using TomCatRunner.Core.Season;
using TomCatRunner.Common.DI;
using TomCatRunner.Gameplay.Core;
using TomCatRunner.Gameplay.UI;
using TomCatRunner.Gameplay.World;
using CanvasManager = TomCatRunner.UI.Framework.CanvasManager;

namespace TomCatRunner.Gameplay.Bootstrap
{
    /// <summary>
    /// 游戏引导 - 场景唯一入口。Awake 构建 DI 容器（GameLifetimeScope），
    /// Start 解析共享服务并组装完整游戏：世界、猫、关卡内容、UI、流程控制器
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameplayBootstrap : MonoBehaviour
    {
        private GameLifetimeScope _scope;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            QualitySettings.shadowDistance = 70f;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // 构建 DI 容器（GameLifetimeScope 注册全部共享基础设施）
            var scopeHost = new GameObject("[GameLifetimeScope]");
            scopeHost.transform.SetParent(transform, false);
            _scope = scopeHost.AddComponent<GameLifetimeScope>();
        }

        private void Start()
        {
            var resolver = _scope.Container;

            // ---- 共享服务 ----
            var broker = resolver.Resolve<MessageBroker>();
            var seasonManager = resolver.Resolve<SeasonManager>();
            var streamer = resolver.Resolve<LevelStreamer>();
            var canvasManager = resolver.Resolve<CanvasManager>();

            // ---- 游戏服务 ----
            var save = new SaveService();
            var input = new InputService();
            var score = new ScoreSystem();
            var audio = new ProceduralAudioService();
            audio.ApplySettings(save.SfxOn, save.MusicOn);

            var theme = new ThemeRuntime();
            theme.Initialize();
            theme.BindSeasonManager(seasonManager, broker);
            // 若赛季已同步加载完成则直接派生主题，否则按日期选择
            var season = seasonManager.CurrentSeason;
            if (season != null) theme.SwitchFromSeason(season);
            else theme.Switch(theme.PickByDate(DateTime.UtcNow));

            // ---- 世界 ----
            var world = new WorldBuilder(theme);
            world.Build();

            // ---- 猫 ----
            var cat = CatBuilder.Build(world.WorldRoot);

            // ---- 关卡内容（金币/道具收集回调仅做特效，计分由控制器完成）----
            Action<World.CoinBehaviour> onCoinFx = _ => { };
            Action<World.PowerupBehaviour> onPowerupFx = _ => { };
            new ChunkContentBuilder(streamer, theme, cat.transform, onCoinFx, onPowerupFx);

            // ---- UI（先占位，控制器构造后回填）----
            Action startRun = () => { };
            Action<bool> musicToggle = _ => { };
            Action<bool> sfxToggle = _ => { };
            Action pausePressed = () => { };
            Action resume = () => { };
            Action restartFromPause = () => { };
            Action goMenu = () => { };
            Action retry = () => { };

            var menu = new MenuScreen(canvasManager, theme, save,
                () => startRun(), on => musicToggle(on), on => sfxToggle(on));
            var hud = new HudScreen(canvasManager, theme, () => pausePressed());
            var toast = new ToastService(canvasManager);
            var pauseScreen = new PauseScreen(canvasManager,
                () => resume(), () => restartFromPause(), () => goMenu());
            var gameOverScreen = new GameOverScreen(canvasManager, () => retry(), () => goMenu());
            var countdown = new CountdownScreen(canvasManager);

            // ---- 流程控制器 ----
            var controller = new GameFlowController(
                input, score, save, audio, cat, world.CameraRig, streamer, seasonManager,
                theme, menu, hud, pauseScreen, gameOverScreen, countdown, toast);

            // 回填 UI 回调
            startRun = controller.StartRun;
            musicToggle = audio.ApplyMusic;
            sfxToggle = audio.ApplySfx;
            pausePressed = controller.Pause;
            resume = controller.Resume;
            restartFromPause = controller.RestartFromPause;
            goMenu = controller.GoToMenu;
            retry = controller.RestartRun;

            // 收集特效回调（飘字/音效已由控制器处理，这里留粒子扩展位）
            onCoinFx = _ => { };
            onPowerupFx = _ => { };

            // ---- 帧驱动 ----
            var driverGo = new GameObject("[GameplayDriver]");
            driverGo.transform.SetParent(transform, false);
            var driver = driverGo.AddComponent<GameplayDriver>();
            driver.Construct(controller);

            controller.Begin();

            Debug.Log($"[GameplayBootstrap] 游戏组装完成 · 主题={theme.Current.DisplayName}");
        }
    }
}
