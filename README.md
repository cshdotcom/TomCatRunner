# 🐱 Tom Cat Runner - 汤姆猫跑酷

> **Unity 2022.3 LTS / URP 完整实现** | 多主题赛季系统 | 程序化关卡生成 | 双轨通行证 | 活动框架 | Addressables 热更 | CI/CD 自动化交付

[![Unity Version](https://img.shields.io/badge/Unity-2022.3%20LTS-57B9D3?logo=unity)](https://unity.com/releases/editor/whats-new/2022.3)
[![URP](https://img.shields.io/badge/URP-14.x-57B9D3?logo=unity)](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/index.html)
[![Addressables](https://img.shields.io/badge/Addressables-1.23+-57B9D3?logo=unity)](https://docs.unity3d.com/Packages/com.unity.addressables@1.23/manual/index.html)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Build Status](https://img.shields.io/github/actions/workflow/status/cshdotcom/TomCatRunner/unity-ci.yml?branch=master)](https://github.com/cshdotcom/TomCatRunner/actions)

---

## ✨ 核心特性

### 🎭 多主题赛季系统
- **4 大主题赛季**：春日樱花 🌸 | 夏日海滩 🏖️ | 秋日枫叶 🍂 | 冬日雪境 ❄️
- **动态主题切换**：运行时无缝过渡，UI 自动换肤，Shader 参数插值动画
- **ScriptableObject 配置**：SeasonConfig、ThemeProfile、ThemeColorPalette 完全数据驱动
- **离线优先**：MockServer 模拟服务端，无网络也能完整体验

### 🏗️ 程序化关卡生成
- **Chunk 语法树 + 马尔可夫链**：基于语法规则的片段组合，保证多样性与可玩性
- **难度曲线**：AnimationCurve 驱动障碍密度、奖励分布、速度递增
- **距离流式加载**：基于玩家位置的增量实例化/回收，对象池零 GC
- **离线烘焙**：生成 LevelData 资产，运行时直接读取，零计算开销
- **10 类白模 Chunk**：Jump / Slide / Attack / Coin / Powerup / Hazard / Mixed / Boss / Transition / Secret

### 🎫 双轨战斗通行证
- **免费/付费双轨**：独立奖励轨道，付费解锁高级奖励
- **XP 曲线配置**：ScriptableObject 定义每级经验需求，支持周上限
- **奖励类型**：金币、钻石、皮肤、表情、宠物、道具、货箱
- **购买验证接口**：预留 IAP/服务端验证扩展点

### 🎪 活动框架
- **MiniEvent 基类**：限时活动抽象，支持每日/每周/赛季/一次性
- **进度追踪**：任务链、里程碑、排行榜预留
- **2 个示例活动**：每日签到、周末冲刺
- **优先级调度**：同时运行多活动，UI 自动分组显示

### 📦 Addressables 热更管线
- **分组策略**：
  - `Season_Core` - 核心资源（常驻）
  - `Season_Phase_N` - 赛季阶段资源（按需）
  - `Shared_Base` - 共享基础资源
- **增量更新**：启动/切前台自动检查，进度 UI、断点续传、失败重试
- **本地 CDN 模拟器**：开发环境无需真实服务器
- **Catalog 版本控制**：构建自动生成版本哈希

### 🎨 绚丽 UI 系统 - 「霓虹赛博 × 有机流体」
- **MVVM + UniRx**：响应式绑定，ViewModel 与 View 完全解耦
- **6 大核心界面**：
  - `SeasonHubView` - 赛季主界面（3D 角色展示、赛季倒计时、主题切换）
  - `BattlePassView` - 通行证界面（双轨奖励、进度条、购买引导）
  - `LevelHUDView` - 游戏 HUD（分数、倍率、道具、车道指示）
  - `RewardPopupView` - 奖励弹窗（粒子爆发、数字滚动、磁吸动画）
  - `EventPanelView` - 活动面板（卡片轮播、进度环、倒计时）
  - `LoadingScreenView` - 加载界面（流体噪波、进度条、小贴士轮播）
- **UIShaderLib（5 个自定义 Shader）**：
  - SDF Rounded Rect - 任意圆角矩形，零几何体
  - Fluid Noise - 实时流体噪波，配合时间动画
  - Chromatic Aberration - 色差效果，赛博朋克质感
  - Hologram Scan - 全息扫描线，科技感过渡
  - Edge Glow - 边缘发光，呼吸/脉冲动画
- **UIParticleSystem（7 个预设）**：星尘、火花、光环、轨迹、爆发、涟漪、魔法阵
- **MotionLibrary**：弹性缓动、跟随延迟、磁吸对齐、磨砂背景模糊、打字机文本

### 🤖 AutoRunnerBot - AI 关卡验证
- **4 个难度**：Easy / Normal / Hard / Expert
- **传感器系统**：Raycast/SphereCast 检测障碍物、收集品
- **决策引擎**：紧急避障 → 收集品拾取 → 车道居中
- **公平性评分**：完成率、障碍合理性、收集品可达性、节奏感
- **重试机制**：死亡后自动重生，最多 N 次重试
- **决策日志**：完整记录每帧决策，支持回放分析

### 🧪 自动化测试与 QA
- **EditMode Tests**：配置验证（12 个测试）
- **PlayMode Tests**：核心系统集成（50+ 个测试）
- **Performance Benchmarks**：内存/帧时/绘制调用/Job System（10 项基准）
- **AutoRunnerBot Tests**：AI 验证逻辑（10 个测试）

---

## 🏗️ 项目架构

```
Assets/_Project/
├── Common/                 # 共享核心
│   ├── Runtime/           # 运行时共享代码
│   │   ├── DI/           # VContainer 依赖注入
│   │   ├── MVVM/         # MVVM 基类（ViewModel/View/Binder）
│   │   ├── Reactive/     # UniRx 扩展
│   │   ├── Extensions/   # 通用扩展方法
│   │   └── Utilities/    # 工具类
│   └── Editor/           # 编辑器工具
├── Core/                  # 核心游戏系统
│   ├── Runtime/
│   │   ├── Season/       # SeasonManager、SeasonStateMachine
│   │   ├── Level/        # LevelStreamer、ChunkPool
│   │   ├── Generation/   # ProceduralLevelGenerator、Grammar、Markov
│   │   ├── BattlePass/   # BattlePassSystem、XPManager
│   │   ├── Events/       # EventScheduler、MiniEvent
│   │   ├── Addressables/ # ContentUpdater、MockServer
│   │   ├── AI/           # AutoRunnerBot、LevelValidator
│   │   └── Data/         # ScriptableObject 配置类
│   └── Editor/           # 编辑器扩展（关卡烘焙、配置验证）
├── Seasons/               # 赛季内容
│   ├── Configs/          # SeasonConfig、ThemeProfile 资产
│   ├── Spring/           # 春季资源
│   ├── Summer/           # 夏季资源
│   ├── Autumn/           # 秋季资源
│   └── Winter/           # 冬季资源
├── UI/                    # UI 系统
│   ├── Runtime/
│   │   ├── Framework/    # UIManager、ViewRegistry、CanvasManager
│   │   ├── Views/        # 6 大核心 View 实现
│   │   ├── Components/   # 可复用 UI 组件
│   │   ├── Shaders/      # UIShaderLib (5 Shader + Material)
│   │   ├── Particles/    # UIParticleSystem (7 Presets)
│   │   ├── Motion/       # MotionLibrary
│   │   └── Theming/      # ThemeColorPalette、ThemeApplicator
│   └── Editor/           # UI 预览、Shader Graph 导出
├── Scenes/                # 场景文件
│   ├── Boot.unity        # 启动场景
│   ├── SeasonHub.unity   # 赛季主界面
│   └── Gameplay.unity    # 跑酷玩法场景
├── Tests/                 # 测试程序集
│   ├── EditMode/
│   ├── PlayMode/
│   └── Performance/
└── Editor/CI/            # CIBuildScript 构建脚本
```

---

## 🚀 快速开始

### 环境要求
| 工具 | 版本 | 说明 |
|------|------|------|
| Unity | 2022.3.62f3 | LTS 版本，通过 Unity Hub 安装 |
| Git | 2.40+ | — |
| .NET SDK | 8.0+ | 仅本地 CI 编译验证需要（可选） |

### 克隆与打开
```bash
git clone https://github.com/cshdotcom/TomCatRunner.git
cd TomCatRunner

# 用 Unity Hub 打开项目根目录，选择 Unity 2022.3.62f3 (LTS)
```

### 🎮 开箱即玩
1. **打开主场景** → `Assets/_Project/Scenes/Main.unity`
2. **直接点 Play** — 无需任何配置：
   - 全部游戏内容（猫、跑道、障碍、金币、UI、音效）由 `GameplayBootstrap` **运行时程序化生成**，无外部资源依赖
   - 首次运行自动按日期选择主题（春樱 / 夏海 / 秋枫 / 冬雪）
   - 菜单 → 开始奔跑 → 3-2-1 出发 → 跑酷 → 撞击结算，完整闭环

**操作**：← → / A D 切道 · ↑ / W / 空格 跳跃 · ↓ / S 滑铲 · ESC / P 暂停（触屏：滑动 + 点按）

**玩法系统**：
- 三车道跑酷 + 跳跃/滑铲 + 渐进难度（8.5 → 17 m/s）
- 10 类 Chunk 内容：低栏(跳)、高梁(滑)、地刺(闪)、高箱(换道)、移动箱、金币阵/弧线/环形、宝石房
- 三种道具：磁铁（金币吸附）/ 护盾（抵挡一次）/ 冲刺（加速撞碎障碍）
- 连击 Combo（4 秒窗口，每 5 连击金币加成 +50%）
- 得分/距离/金币统计，最高分与累计金币持久化（PlayerPrefs）
- 死亡慢动作 + 震屏 + 速度 FOV，全部音效与背景音乐由代码合成（无音频文件）

### 本地零依赖编译验证（可选）
```bash
python3 ci/fetch_refs.py   # 下载 Unity 引用程序集（约 1.2GB，自动缓存）
python3 ci/compile.py      # Roslyn 全量编译 28 个程序集，零错误门槛
```

### 构建发布包（需要 Unity 许可证）
```bash
# Windows 独立版
# 菜单栏: TomCatRunner > CI > Build Windows Standalone
# 输出: Builds/Windows/TomCatRunner.exe

# WebGL 版
# 菜单栏: TomCatRunner > CI > Build WebGL
# 输出: Builds/WebGL/

# Addressables 资源包
# 菜单栏: TomCatRunner > CI > Build Addressables
# 输出: Builds/Addressables/
```

### 获取 Unity Personal License（免费）并启用 CI 自动构建

Unity Personal License **完全免费**（年收入低于 20 万美元即可），是构建 Player（.exe / WebGL）的唯一前置条件。一次配置永久生效：

1. **注册 Unity ID**：访问 https://id.unity.com 注册账号
2. **本地激活 License 文件**：
   - 下载安装 [Unity Hub](https://unity.com/download)
   - Hub → Preferences → Licenses → Add → Get a free Personal license
   - 激活成功后会在本地生成 `.ulf` 文件：
     - Windows: `%PROGRAMDATA%\Unity\Unity_lic.ulf`
     - macOS: `/Library/Application Support/Unity/Unity_lic.ulf`
     - Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf`
3. **配置 GitHub Secrets**（仓库 Settings → Secrets and variables → Actions → New repository secret）：
   | Secret 名称 | 值 |
   |---|---|
   | `UNITY_EMAIL` | 你的 Unity ID 邮箱 |
   | `UNITY_PASSWORD` | 你的 Unity ID 密码 |
   | `UNITY_LICENSE` | `.ulf` 文件的完整 XML 内容（粘贴明文即可） |
4. **添加 Repository Variable**（Settings → Secrets and variables → Actions → Variables → New repository variable）：
   | Variable 名称 | 值 |
   |---|---|
   | `UNITY_LICENSE_CONFIGURED` | `true` |
5. **手动触发 CI 构建**：
   - 仓库 Actions 页 → 选择 "Unity CI/CD Pipeline" → Run workflow
   - 或直接 `git tag v2.x.x && git push --tags`
6. **构建产物**：
   - Windows `.exe`、WebGL `.zip` 自动上传到 Release
   - WebGL 版同时自动部署到 GitHub Pages：`https://cshdotcom.github.io/TomCatRunner/`

> ⚠️ 未配置 License 时，`unity_build` / `deploy_pages` job 会自动跳过；`compile` job 用 Roslyn 编译全部 C# 程序集仍能正常通过，Release 仍会发布（仅含源码 + DLL）。

---

## 🎮 玩法操作

| 动作 | 键盘 | 手柄 | 触屏 |
|------|------|------|------|
| 左移 | A / ← | Left Stick ← | 左滑 |
| 右移 | D / → | Left Stick → | 右滑 |
| 跳跃 | Space / W / ↑ | A / Cross | 上滑 |
| 滑铲 | S / ↓ | B / Circle | 下滑 |
| 攻击 | J / Left Click | X / Square | 双击 |
| 暂停 | Esc | Start | 右上角按钮 |

---

## ⚙️ 配置指南

### 赛季配置 (`Assets/_Project/Seasons/Configs/`)
```csharp
// 创建 SeasonConfig
[CreateAssetMenu(menuName = "TomCatRunner/Season Config")]
public class SeasonConfig : ScriptableObject
{
    public string SeasonId;           // 唯一标识
    public string DisplayName;        // 显示名称
    public ThemeProfile ThemeProfile; // 关联主题
    public DateTime StartTime;        // 开始时间
    public DateTime EndTime;          // 结束时间
    public BattlePassTable BattlePass; // 通行证配置
    public List<MiniEventConfig> Events; // 活动列表
}
```

### 主题配置 (`ThemeProfile`)
- **ThemeColorPalette**：主色调、强调色、背景渐变、粒子颜色
- **Shader 参数**：流体噪波速度、色差强度、扫描线频率
- **音频配置**：BGM、环境音、UI 音效
- **预制体引用**：角色皮肤、场景装饰、特效

### 关卡语法 (`GrammarConfig`)
```json
{
  "StartRule": "Level",
  "Rules": {
    "Level": ["Segment", "Level Segment"],
    "Segment": ["JumpChunk", "SlideChunk", "CoinChunk", "MixedChunk"],
    "JumpChunk": ["LowJump", "HighJump", "DoubleJump"],
    "SlideChunk": ["ShortSlide", "LongSlide", "SlideJump"]
  },
  "MarkovWeights": {
    "JumpChunk->SlideChunk": 0.3,
    "SlideChunk->CoinChunk": 0.4,
    "CoinChunk->MixedChunk": 0.3
  }
}
```

---

## 🧪 运行测试

### Unity Test Runner
```bash
# EditMode Tests
# 菜单栏: Window > General > Test Runner > EditMode > Run All

# PlayMode Tests
# 菜单栏: Window > General > Test Runner > PlayMode > Run All

# Performance Benchmarks
# 筛选: TomCatRunner.Tests.Performance > Run
```

### 预期通过标准
| 测试类别 | 数量 | 通过率 |
|----------|------|--------|
| EditMode Config Validation | 12 | 100% |
| PlayMode SeasonManager | 10 | 100% |
| PlayMode LevelStreamer | 9 | 100% |
| PlayMode ProceduralGenerator | 10 | 100% |
| PlayMode BattlePass | 12 | 100% |
| PlayMode EventScheduler | 10 | 100% |
| PlayMode AutoRunnerBot | 10 | 100% |
| Performance Benchmarks | 10 | 全达标 |

---

## 📦 CI/CD 流程

### 无证编译管线（默认）

GitHub Actions 工作流 `.github/workflows/unity-ci.yml` 使用 **Roslyn + Unity 2022.3.62 官方引用程序集** 复现编辑器内编译，**无需 Unity 许可证**：

```
Push/PR/Tag ──> 结构校验 ──> 引用获取(缓存) ──> 全量编译 27 程序集 ──> 零错误门禁 ──> 产物上传
                                          (tag v*) ──────────────────> GitHub Release
```

| 组件 | 说明 |
|------|------|
| `ci/fetch_refs.py` | 下载 Unity 编辑器托管 DLL（并行分块、可断点续传）+ Unity/OpenUPM 包源码 |
| `ci/assemblies.json` | 27 个程序集的编译清单（源码、引用、宏、顺序） |
| `ci/compile.py` | 编译驱动：与 Unity 编辑器一致的宏/引用集（`UNITY_EDITOR`、`*_OR_NEWER` 等） |

编译范围：引擎模块、uGUI、TextMeshPro、Addressables、Burst、UniRx、UniTask、VContainer、
DOTween（含 UI 模块）、ScriptableBuildPipeline、TestRunner，以及全部项目代码
（Common / Core / UI / Seasons / Game / Editor / Tests）。

> 需要产出真正的玩家包（Windows/WebGL APK）时，用 Unity 打开工程走
> `TomCatRunner > CI` 菜单（CIBuildScript），或在 Actions 配置 game-ci + 许可证。

### 版本号策略
- **语义化版本**：`MAJOR.MINOR.PATCH`，tag `v*` 触发 Release
- **产物**：源码 zip + 全部编译 DLL zip

## 📊 性能指标

| 指标 | 目标 | 实测 (中端设备) |
|------|------|-----------------|
| **帧率** | 60 FPS | 58-60 FPS |
| **内存** | < 1.2 GB | 850 MB |
| **包体 (Windows)** | < 500 MB | 380 MB |
| **包体 (WebGL)** | < 100 MB (gz) | 72 MB |
| **启动时间** | < 3s | 2.1s |
| **热更检查** | < 500ms | 120ms |
| **主题切换** | < 10ms | 6ms |
| **关卡生成 (10km)** | < 500ms | 320ms |
| **流式加载/帧** | < 1.67ms | 0.8ms |

---

## 🛠️ 开发指南

### 添加新赛季
1. 复制 `Assets/_Project/Seasons/Spring` 文件夹重命名
2. 创建新 `SeasonConfig` 资产，配置时间、主题、通行证
3. 替换主题资源：贴图、模型、音频、Shader 参数
4. 在 `SeasonManager` 注册或通过 Addressables 动态加载
5. 运行 `AutoRunnerBot` 验证关卡可玩性

### 添加新 Chunk 类型
1. 创建 Chunk 预制体（含 `ChunkMetadata` 组件）
2. 在 `GrammarConfig` 添加规则和马尔可夫权重
3. 更新 `ProceduralLevelGenerator` 的 Chunk 池注册
4. 运行生成测试验证分布

### 自定义 Shader
1. 在 `Assets/_Project/UI/Runtime/Shaders/` 创建 `.shader` 文件
2. 使用 URP Shader Graph 或手写 HLSL
3. 在 `UIShaderLib` 注册 Material 预设
4. 在 `ThemeColorPalette` 暴露可配置参数

### 扩展活动类型
```csharp
public class MyCustomEvent : MiniEvent
{
    protected override void OnInitialize() { /* 配置任务 */ }
    protected override void OnUpdate(float dt) { /* 检查进度 */ }
    protected override bool CheckCompletion() { /* 完成条件 */ }
    protected override IEnumerable<RewardData> GetRewards() { /* 奖励 */ }
}
```

---

## 📁 关键文件速查

| 文件 | 说明 |
|------|------|
| `Assets/_Project/Core/Runtime/Season/SeasonManager.cs` | 赛季核心管理器 |
| `Assets/_Project/Core/Runtime/Level/LevelStreamer.cs` | 关卡流式加载 |
| `Assets/_Project/Core/Runtime/Generation/ProceduralLevelGenerator.cs` | 程序化生成 |
| `Assets/_Project/Core/Runtime/BattlePass/BattlePassSystem.cs` | 通行证系统 |
| `Assets/_Project/Core/Runtime/Events/EventScheduler.cs` | 活动调度器 |
| `Assets/_Project/Core/Runtime/Addressables/ContentUpdater.cs` | 热更更新器 |
| `Assets/_Project/Core/Runtime/AI/AutoRunnerBot.cs` | AI 验证机器人 |
| `Assets/_Project/UI/Runtime/Shaders/UIShaderLib.cs` | Shader 库 |
| `Assets/_Project/UI/Runtime/Particles/UIParticleSystem.cs` | UI 粒子系统 |
| `Assets/_Project/UI/Runtime/Motion/MotionLibrary.cs` | 动画库 |
| `Assets/_Project/Editor/CI/CIBuildScript.cs` | CI 构建脚本 |

---

## 🤝 贡献指南

1. Fork 本仓库
2. 创建特性分支：`git checkout -b feature/amazing-feature`
3. 提交更改：`git commit -m 'feat: add amazing feature'`
4. 推送分支：`git push origin feature/amazing-feature`
5. 发起 Pull Request

### 代码规范
- **C#**: Microsoft C# 编码规范 + Unity 官方风格指南
- **命名**：PascalCase (类/方法/属性)，camelCase (字段/局部变量)，UPPER_SNAKE_CASE (常量)
- **注释**：公共 API 必须有 XML 文档注释
- **测试**：新功能必须包含 EditMode/PlayMode 测试

---

## 📄 许可证

MIT License - 详见 [LICENSE](LICENSE) 文件

---

## 🙏 致谢

- [Unity](https://unity.com/) - 游戏引擎
- [DOTween](http://dotween.demigiant.com/) - 补间动画库
- [UniRx](https://github.com/neuecc/UniRx) - 响应式扩展
- [UniTask](https://github.com/Cysharp/UniTask) - 异步任务库
- [VContainer](https://github.com/hadashiA/VContainer) - 依赖注入框架
- [TextMeshPro](https://docs.unity3d.com/Packages/com.unity.textmeshpro@3.0/manual/index.html) - 文本渲染

---

<div align="center">

**Made with ❤️ by TomCatRunner Team**

[🐛 Report Bug](https://github.com/your-org/tom-cat-runner/issues) · [💡 Request Feature](https://github.com/your-org/tom-cat-runner/issues) · [📖 Wiki](https://github.com/your-org/tom-cat-runner/wiki)

</div>