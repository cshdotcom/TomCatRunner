using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Cysharp.Threading.Tasks;
using TomCatRunner.Common;
using TomCatRunner.Common.Reactive;
using TomCatRunner.Core.AI;
using TomCatRunner.Core.Networking;
using TomCatRunner.Core.Data;
using TomCatRunner.Core.Generation;
using TomCatRunner.Core.Level;
using TomCatRunner.Core.Season;
using TomCatRunner.UI.Framework;

namespace TomCatRunner.Common.DI
{
    /// <summary>
    /// 游戏组合根 - 所有核心系统的依赖注入配置
    /// 挂载在 Boot 场景的 GameObject 上
    /// </summary>
    public class GameLifetimeScope : LifetimeScope
    {
        [Header("Configuration")]
        [SerializeField] private bool _useMockServer = true;
        [SerializeField] private bool _enableAutoRunnerBot = false;
        [SerializeField] private bool _offlineMode = true;

        [Header("Optional Asset Overrides")]
        [SerializeField] private GrammarConfig _grammarConfigOverride;
        [SerializeField] private ThemeColorPalette _paletteOverride;

        private SchedulerDriver _schedulerDriver;

        protected override void Configure(IContainerBuilder builder)
        {
            // ========== 核心配置 ==========
            builder.RegisterInstance(new GameConfig
            {
                UseMockServer = _useMockServer,
                EnableAutoRunnerBot = _enableAutoRunnerBot,
                OfflineMode = _offlineMode
            }).AsSelf();

            // ========== 语法配置（运行时兜底创建：无资产依赖的完整默认文法） ==========
            var grammarConfig = _grammarConfigOverride != null
                ? _grammarConfigOverride
                : GrammarConfig.CreateDefault();
            builder.RegisterInstance(grammarConfig).AsSelf();

            // ========== 响应式基础设施 ==========
            builder.Register<MessageBroker>(Lifetime.Singleton).AsSelf();
            builder.Register<Scheduler>(Lifetime.Singleton).AsSelf();

            // ========== 赛季系统（Core 流式管线） ==========
            builder.Register<MockServer>(Lifetime.Singleton).AsSelf();

            builder.Register<SeasonManager>(Lifetime.Singleton)
                   .As<IInitializable>()
                   .As<ITickable>()
                   .As<IPausable>()
                   .AsSelf();

            // ========== 关卡流式系统 ==========
            builder.Register<ChunkPool>(Lifetime.Singleton)
                   .As<IInitializable>()
                   .AsSelf();

            builder.Register<ProceduralLevelGenerator>(Lifetime.Singleton)
                   .As<IInitializable>()
                   .AsSelf();

            builder.Register<LevelStreamer>(Lifetime.Singleton)
                   .As<IInitializable>()
                   .As<ITickable>()
                   .As<IPausable>()
                   .AsSelf();

            // ========== AI 验证 (可选) ==========
            builder.Register<LevelValidator>(Lifetime.Singleton).AsSelf();
            if (_enableAutoRunnerBot)
            {
                // AutoRunnerBot 为 MonoBehaviour，由场景/验证器按需创建
                Debug.Log("[GameLifetimeScope] AutoRunnerBot validation enabled");
            }

            // ========== UI 框架 ==========
            builder.Register<CanvasManager>(Lifetime.Singleton).AsSelf();
            builder.Register<ViewRegistry>(Lifetime.Singleton).AsSelf();
            builder.Register<UIManager>(Lifetime.Singleton)
                   .As<IInitializable>()
                   .As<ITickable>()
                   .AsSelf();
        }

        private void Start()
        {
            // Scheduler 需要每帧 Tick，这里用一个轻量驱动器承载
            var driverGo = new GameObject("[SchedulerDriver]");
            driverGo.transform.SetParent(transform, false);
            _schedulerDriver = driverGo.AddComponent<SchedulerDriver>();
            _schedulerDriver.Construct(Container.Resolve<Scheduler>());
        }

        protected override void OnDestroy()
        {
            _schedulerDriver = null;
            base.OnDestroy();
        }

        /// <summary>
        /// 每帧驱动 Scheduler 的轻量 MonoBehaviour
        /// </summary>
        private sealed class SchedulerDriver : MonoBehaviour
        {
            private Scheduler _scheduler;

            public void Construct(Scheduler scheduler) => _scheduler = scheduler;

            private void Update()
            {
                _scheduler?.Tick(Time.deltaTime);
            }
        }
    }
}
