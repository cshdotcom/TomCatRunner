# ⚡ Tom Cat Runner - 快速上手指南

> **5 分钟从零到跑通全流程** | 适合新入职开发者、CI 验证、快速 Demo

---

## 🎯 目标
双击 `Builds/Windows/TomCatRunner.exe` → 看到赛季主界面 → 点击开始 → 进入跑酷玩法 → 完成一局 → 看到奖励结算 → 打开通行证升级

---

## 1️⃣ 环境准备 (2 分钟)

### 必装工具
| 工具 | 下载链接 | 验证命令 |
|------|----------|----------|
| **Unity Hub** | [unity.com/download](https://unity.com/download) | `unity --version` |
| **Unity 2022.3.15f1 LTS** | Hub 内安装 | `Unity 2022.3.15f1` |
| **Git + LFS** | [git-scm.com](https://git-scm.com) | `git lfs version` |
| **VS Code / Rider** | 任选其一 | - |

### 克隆项目
```bash
# 1. 安装 LFS
git lfs install

# 2. 克隆 (浅克隆加速)
git clone --depth=1 https://github.com/your-org/tom-cat-runner.git
cd tom-cat-runner

# 3. 拉取 LFS 大文件 (Addressables、贴图、模型)
git lfs pull
```

---

## 2️⃣ 打开项目 (30 秒)

1. 打开 **Unity Hub** → **Projects** → **Add project from disk**
2. 选择 `tom-cat-runner` 文件夹
3. 确保显示 **Unity 2022.3.15f1** → 点击打开
4. 等待 **Package Manager** 解析依赖 (首次约 2-5 分钟)
   - 自动安装：URP、Addressables、TextMeshPro、DOTween、UniRx、UniTask、VContainer

---

## 3️⃣ 首次运行 (1 分钟)

### 方式 A：编辑器运行 (推荐调试)
```text
1. Project 窗口 → Assets/_Project/Scenes/Boot.unity → 双击打开
2. 顶部工具栏 ▶ Play 按钮
3. 观察 Game 窗口 + Console 日志
```

**预期 Console 输出**：
```
[Bootstrap] Starting TomCatRunner v1.0.0
[DI] Container built: 47 registrations
[SeasonManager] Initialized. Current: Spring_2024 (Theme: Sakura)
[LevelStreamer] Pool warmed: 50 chunks
[ContentUpdater] Offline mode - skipping remote check
[BattlePassSystem] Loaded. Level 1/50, XP 0/100
[EventScheduler] Active events: DailyLogin, WeekendRush
[UIManager] Registered 6 views
[SeasonHubView] Show - Theme applied: Sakura
```

### 方式 B：直接运行构建包 (验证交付物)
```text
1. 确保已构建：菜单栏 TomCatRunner > CI > Build Windows Standalone
2. 资源管理器打开 Builds/Windows/
3. 双击 TomCatRunner.exe
```

---

## 4️⃣ 体验全流程 (2 分钟)

### 步骤 1：赛季主界面
- **看到**：3D 汤姆猫模型、樱花飘落粒子、赛季倒计时、主题切换按钮
- **操作**：点击 **开始游戏** 按钮 (弹性缩放动画)
- **验证**：Scene 切换到 Gameplay，LoadingScreen 显示流体噪波进度条

### 步骤 2：跑酷玩法
- **操作**：
  - `A/D` 或 `←/→` - 左右换道 (3 轨道)
  - `Space/W/↑` - 跳跃 (越过低障碍)
  - `S/↓` - 滑铲 (通过高障碍)
  - `J/鼠标左键` - 攻击 (破坏障碍物)
- **观察 HUD**：
  - 左上：距离、分数、倍率
  - 右上：金币数、钻石数
  - 底部：车道指示器、道具栏
- **体验**：程序化生成的无限关卡，难度随距离递增

### 步骤 3：结算与奖励
- **触发**：碰撞障碍物 3 次或按 `Esc` 结束
- **看到**：RewardPopup 弹窗、金币飞入动画、数字滚动、粒子爆发
- **点击**：继续 → 返回 SeasonHub

### 步骤 4：通行证升级
- **点击主界面**：通行证图标 (底部导航栏)
- **看到**：双轨奖励轨道、进度条、免费/付费对比
- **操作**：模拟获得 XP (测试用 `BattlePassTester` 组件)
- **验证**：等级提升、奖励解锁、领取动画

---

## 5️⃣ 常用开发命令

### 编辑器菜单 (TomCatRunner 菜单栏)
| 菜单项 | 作用 |
|--------|------|
| **CI > Build Windows Standalone** | 构建 Windows 可执行文件 |
| **CI > Build WebGL** | 构建 WebGL 版本 |
| **CI > Build Addressables** | 构建热更资源包 |
| **CI > Build All** | 全平台构建 |
| **Tools > Validate All Configs** | 验证所有 ScriptableObject 配置 |
| **Tools > Bake Current Level** | 烘焙当前关卡为 LevelData |
| **Tools > Run AutoRunnerBot** | 启动 AI 验证当前关卡 |
| **Debug > Force Season Switch** | 强制切换赛季主题 |
| **Debug > Simulate Purchase** | 模拟通行证购买 |
| **Debug > Trigger Event** | 手动触发测试活动 |

### 快捷键 (Play Mode 下)
| 按键 | 功能 |
|------|------|
| `F1` | 切换显示/隐藏 HUD |
| `F2` | 切换显示/隐藏调试面板 |
| `F3` | 瞬间完成当前关卡 (测试用) |
| `F4` | 重置玩家位置 |
| `F5` | 切换主题 (Spring/Summer/Autumn/Winter) |
| `F6` | 给予 1000 XP (通行证测试) |
| `F7` | 生成奖励弹窗 (测试动画) |
| `F8` | 切换无敌模式 |

---

## 6️⃣ 目录速查

```
Assets/_Project/
├── Scenes/                 # 3 个核心场景
│   ├── Boot.unity         # 启动初始化 (必须第一个加载)
│   ├── SeasonHub.unity    # 赛季主界面
│   └── Gameplay.unity     # 跑酷玩法场景
├── Common/Runtime/         # 共享基础设施
│   ├── DI/                # VContainer 安装器
│   ├── MVVM/              # ViewModel/View/Binder 基类
│   └── Reactive/          # UniRx 扩展
├── Core/Runtime/           # 核心系统 (按依赖顺序)
│   ├── Season/            # SeasonManager ← 无依赖
│   ├── Level/             # LevelStreamer ← Season
│   ├── Generation/        # ProceduralGenerator ← Level
│   ├── BattlePass/        # BattlePassSystem ← Season
│   ├── Events/            # EventScheduler ← Season
│   ├── Addressables/      # ContentUpdater ← 无依赖
│   └── AI/                # AutoRunnerBot ← Level, Generation
├── UI/Runtime/             # UI 系统
│   ├── Framework/         # UIManager, CanvasManager
│   ├── Views/             # 6 个 View 实现
│   ├── Shaders/           # 5 个 Shader + Material
│   ├── Particles/         # 7 个粒子预设
│   ├── Motion/            # MotionLibrary
│   └── Theming/           # ThemeColorPalette, Applicator
├── Seasons/Configs/        # 赛季配置资产
│   ├── SeasonConfig_Spring.asset
│   ├── ThemeProfile_Sakura.asset
│   ├── BattlePassTable_Spring.asset
│   └── ...
└── Editor/CI/              # CIBuildScript.cs
```

---

## 7️⃣ 常见问题 (FAQ)

| 问题 | 解决方案 |
|------|----------|
| **打开项目报错 "Package not found"** | Unity Hub → 模块管理 → 确保勾选 **WebGL Build Support**、**Windows Build Support (IL2CPP)** |
| **URP 材质显示粉红** | Project Settings → Graphics → Scriptable Render Pipeline Settings → 赋值 URP Asset |
| **Addressables 构建失败** | Window > Asset Management > Addressables > Build > New Build > Default Build Script |
| **TextMeshPro 报错 TMP_EssentialResources** | Window > TextMeshPro > Import TMP Essential Resources |
| **DOTween 未初始化** | Tools > DOTween Utility Panel > Setup DOTween |
| **VContainer 容器为空** | 检查 Boot 场景是否有 `LifetimeScope` 预制体实例 |
| **WebGL 构建内存不足** | Player Settings > WebGL > Initial Memory Size 设为 256MB+ |
| **热更不生效** | 检查 ContentUpdater.RemoteLoadUrl 是否指向正确 CDN，或开启 OfflineMode |

---

## 8️⃣ 下一步建议

### 新入职开发者
1. 📖 阅读 `README.md` 完整架构文档
2. 🧪 运行所有测试：`Window > General > Test Runner > Run All`
3. 🔧 尝试修改 `ThemeColorPalette_Sakura` 观察实时换肤
4. 🤖 运行 `AutoRunnerBot` 验证关卡 (菜单：Tools > Run AutoRunnerBot)

### CI/CD 维护者
1. 🔐 GitHub 仓库 Settings > Secrets 配置 `UNITY_LICENSE`
2. 🏷️ 推送 tag 触发 Release：`git tag v1.0.1 && git push origin v1.0.1`
3. 📄 检查 `.github/workflows/ci.yml` 适配项目路径

### 美术/策划
1. 🎨 替换 `Seasons/Spring/` 下的模型、贴图、音频
2. 📝 修改 `SeasonConfig` 的时间、奖励表
3. 🎮 在 `GrammarConfig` 调整 Chunk 权重改变关卡节奏

---

## 🆘 获取帮助

| 资源 | 链接 |
|------|------|
| **完整文档** | [README.md](README.md) |
| **API 参考** | `Assets/_Project/Docs/API.md` (生成中) |
| **问题反馈** | [GitHub Issues](https://github.com/your-org/tom-cat-runner/issues) |
| **内部 Wiki** | [Confluence](https://your-org.atlassian.net/wiki/) |
| **Slack 频道** | `#tomcatrunner-dev` |

---

<div align="center">

**🎉 恭喜！你已跑通 Tom Cat Runner 全流程**

*如有问题，查看 Console 错误堆栈，或在 Slack `@dev-support` 求助*

</div>