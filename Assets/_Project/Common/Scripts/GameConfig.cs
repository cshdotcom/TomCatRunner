using System;

namespace TomCatRunner.Common
{
    /// <summary>
    /// 游戏运行时配置 - 由 GameLifetimeScope 持有并注入到各系统
    /// </summary>
    [Serializable]
    public class GameConfig
    {
        /// <summary>是否使用 MockServer（离线开发模式）</summary>
        public bool UseMockServer = true;

        /// <summary>是否启用 AutoRunnerBot 自动验证</summary>
        public bool EnableAutoRunnerBot = false;

        /// <summary>离线模式：跳过服务器时间同步与远端配置拉取</summary>
        public bool OfflineMode = true;

        /// <summary>Chunk 对象池初始大小</summary>
        public int ChunkPoolSize = 50;

        /// <summary>关卡生成随机种子（0 = 随机）</summary>
        public int LevelSeed = 0;
    }
}
