using System;
using VContainer;
using VContainer.Unity;
using TomCatRunner.Core.Season;
using TomCatRunner.Core.Level;
using TomCatRunner.Core.Generation;
using TomCatRunner.Core.BattlePass;
using TomCatRunner.Core.Events;
using TomCatRunner.Core.Addressables;
using TomCatRunner.Core.AI;
using TomCatRunner.UI.Framework;
using TomCatRunner.Common.Reactive;

namespace TomCatRunner.Common.DI
{
    /// <summary>
    /// 游戏核心 LifetimeScope - 所有核心系统的依赖注入配置
    /// 挂载在 Boot 场景的 GameObject 上
    /// </summary>
    public class GameLifetimeScope : LifetimeScope
    {
        [Header("Configuration")]
        [SerializeField] private bool _useMockServer = true;
        [SerializeField] private bool _enableAutoRunnerBot = false;
        [SerializeField] private bool _offlineMode = true;

        protected override void Configure(IContainerBuilder builder)
        {
            // ========== 核心配置 ==========
            builder.RegisterInstance(new GameConfig
            {
                UseMockServer = _useMockServer,
                EnableAutoRunnerBot = _enableAutoRunnerBot,
                OfflineMode = _offlineMode
            }).AsSelf();

            // ========== 季节系统 ==========
            builder.Register<SeasonManager>(Lifetime.Singleton)
                   .As<IPausable>()
                   .AsSelf();

            // ========== 关卡流式系统 ==========
            builder.Register<LevelStreamer>(Lifetime.Singleton)
                   .As<IInitializable>()
                   .As<ITickable>()
                   .As<IPausable>()
                   .AsSelf();

            builder.Register<ChunkPool>(Lifetime.Singleton)
                   .AsSelf();

            // ========== 程序化生成 ==========
            builder.Register<ProceduralLevelGenerator>(Lifetime.Singleton)
                   .As<IInitializable>()
                   .AsSelf();

            builder.Register<GrammarParser>(Lifetime.Singleton)
                   .AsSelf();

            builder.Register<MarkovChain>(Lifetime.Singleton)
                   .AsSelf();

            // ========== 通行证系统 ==========
            builder.Register<BattlePassSystem>(Lifetime.Singleton)
                   .As<IInitializable>()
                   .As<ITickable>()
                   .AsSelf();

            builder.Register<XPManager>(Lifetime.Singleton)
                   .AsSelf();

            // ========== 活动系统 ==========
            builder.Register<EventScheduler>(Lifetime.Singleton)
                   .As<IInitializable>()
                   .As<ITickable>()
                   .AsSelf();

            // ========== Addressables 热更 ==========
            builder.Register<ContentUpdater>(Lifetime.Singleton)
                   .As<IInitializable>()
                   .As<ITickable>()
                   .AsSelf();

            builder.Register<MockServer>(Lifetime.Singleton)
                   .As<IInitializable>()
                   .AsSelf();

            // ========== AI 验证 (可选) ==========
            if (_enableAutoRunnerBot)
            {
                builder.Register<AutoRunnerBot>(Lifetime.Singleton)
                       .As<IInitializable>()
                       .As<ITickable>()
                       .AsSelf();

                builder.Register<LevelValidator>(Lifetime.Singleton)
                       .AsSelf();
            }

            // ========== UI 框架 ==========
            builder.Register<UIManager>(Lifetime.Singleton)
                   .As<IInitializable>()
                   .As<ITickable>()
                   .AsSelf();

            builder.Register<CanvasManager>(Lifetime.Singleton)
                   .AsSelf();

            builder.Register<ViewRegistry>(Lifetime.Singleton)
                   .AsSelf();

            // ========== 响应式工具 ==========
            builder.Register<MessageBroker>(Lifetime.Singleton)
                   .AsSelf();

            builder.Register<Scheduler>(Lifetime.Singleton)
                   .AsSelf();
        }

        /// <summary>
        /// 游戏运行时配置
        /// </summary>
        [Serializable]
        public class GameConfig
        {
            public bool UseMockServer = true;
            public bool EnableAutoRunnerBot = false;
            public bool OfflineMode = true;
        }
    }
}