# 汤姆猫跑酷 —— 多主题赛季系统设计文档

> 版本：v1.0  
> 日期：2026-11-17  
> 适用引擎：Unity 2022.3 LTS / URP  
> 目标平台：iOS / Android / WebGL（可选）

---

## 1. 核心玩法循环（不变）
| 模块 | 说明 |
|------|------|
| **跑酷核心** | 三条跑道、左右滑动切道、上滑跳跃、下滑滑铲、点击攻击/互动 |
| **收集/消耗** | 金币、钻石、道具箱、能量槽（冲刺/无敌/磁铁） |
| **成长线** | 角色升级、皮肤解锁、坐骑/宠物、装备强化 |
| **社交/竞技** | 好友分数榜、段位赛、公会合作任务 |

> 赛季系统**只在「内容层」扩展**，不改动核心循环。

---

## 2. 赛季架构总览

```
Season (赛季)        ── 4~6 周为一个大周期
   │
   ├── Theme (主题)   ── 视觉/音频/关卡/奖励的统一风格包
   │       ├─ Visual Pack   (环境、角色、UI、特效、音乐)
   │       ├─ Level Pack    (程序化段落 + 手工精修段落)
   │       ├─ Reward Track  (免费/付费 双轨通行证)
   │       └─ Event Pack    (限时活动、小游戏、联动)
   │
   ├── Phase (阶段)   ── 赛季内 3~4 个小阶段，每 1~2 周切换一次子主题
   │       └─ 对应 1 套 Visual Sub-Pack + 新增 5~8 个程序化段落
   │
   └── Cycle (循环)   ── 赛季结束 → 结算 → 预热下一赛季 → 新赛季开启
```

---

## 3. 主题设计规范（Theme Design Kit）

### 3.1 主题要素清单（每个主题必须交付）

| 类别 | 交付物 | 命名规范 | 备注 |
|------|--------|----------|------|
| **环境** | 3~5 套程序化段落 | `ThemeName_Env_{01~05}` | 每套 200~400m，含起/终点过渡 |
| **角色** | 1 套主题皮肤 (Tom) + 1 套伙伴皮肤 | `ThemeName_Tom` / `ThemeName_Buddy` | 含 LOD0~2、表情、动作覆盖 |
| **坐骑/宠** | 1 坐骑 + 1 宠物 | `ThemeName_Mount` / `ThemeName_Pet` | 坐骑含冲刺特效变体 |
| **UI** | 通行证界面、主菜单背景、HUD 皮肤 | `ThemeName_UI` | 支持动态分辨率适配 |
| **音频** | BGM (2~3 首循环) + SFX Pack | `ThemeName_BGM` / `ThemeName_SFX` | BGM 分层：Base / Tension / Victory |
| **特效** | 粒子、后处理、Shader Graph | `ThemeName_VFX` | 含天气、魔法、科技等风格化效果 |
| **文案** | 赛季标题、通行证描述、任务文本 | `ThemeName_Loc_{lang}` | 多语言 CSV/JSON |
| **配置** | 赛季时间表、掉率、难度曲线 | `ThemeName_Config` | ScriptableObject / Addressable |

### 3.2 主题风格坐标系（用于美术/策划对齐）

```
X 轴：写实 ────────────────── 卡通
Y 轴：轻松/治愈 ──────────── 紧张/硬核
Z 轴：现实/生活 ──────────── 奇幻/科幻/赛博
```

> 每个主题在立方体内打一个坐标点，美术/音频/关卡据此发散。

---

## 4. 赛季节奏与运营日历（示例：6 周赛季）

| 周 | 阶段 | 核心动作 | 版本/热更 |
|----|------|----------|-----------|
| -1 | **预热** | 预告片、社区投票皮肤、预下载资源包 | 热更 |
| 1  | **S1 启幕** | 开启通行证、新手引导重制、首周双倍奖励 | 版本/热更 |
| 2  | S1 深化 | 新增 2 段落、限时挑战「主题大冒险」 | 热更 |
| 3  | **S2 切换** | 子主题上线、新皮肤/坐骑投放、段位赛开启 | 热更 |
| 4  | S2 深化 | 公会合作 Boss 战、跨服排行榜 | 热更 |
| 5  | **S3 冲刺** | 最终大奖预告、回归玩家召回、双倍通行证经验 | 热更 |
| 6  | **结算** | 发奖、赛季回顾视频、下赛季预告入口 | 版本 |
| +1 | **间歇期** | 旧皮肤折扣、资源回收、技术债偿还 | 版本/热更 |

> **关键节点必须可配置**：`SeasonConfig.asset` 控制开始/结束/阶段切换时间戳。

---

## 5. 技术落地方案

### 5.1 资源管理（Addressables + Content Catalog）
```
Addressable Groups
 ├─ Season_{ID}_Core       (必须随版本下发，~200 MB)
 │    ├─ Environments
 │    ├─ Characters
 │    ├─ UI
 │    └─ Audio
 ├─ Season_{ID}_Phase_{N}  (热更下发，~50~80 MB)
 │    ├─ New_Levels
 │    ├─ New_Skins
 │    └─ Events
 └─ Shared_Base            (常驻，不随赛季变动)
```

> **Catalog 版本号 = SeasonID * 100 + PhaseID**，客户端启动时自动拉取最新 Catalog。

### 5.2 程序化关卡生成（Procedural Level Generator）
- **Segment（段落）**：长度 200~400m，含 `StartGate` / `EndGate` / `Checkpoint`。
- **Chunk（块）**：20~50m 可复用预制体，标签化：`Jump` `Slide` `Attack` `Coin` `Powerup` `Hazard`。
- **ThemeProfile**：每个主题一套 `ScriptableObject`，定义
  - 允许的 Chunk 权重表
  - 障碍/奖励密度曲线（距离 → 密度）
  - 特色机制（如：沙漠风暴周期性遮挡视野、赛博轨道变色提示）
- **生成流程**：
  1. 读取 `ThemeProfile` + `DifficultyCurve(当前段位)`
  2. 马尔可夫链/语法树拼装 Segment 序列
  3. 离线烘焙成 `LevelData` (JSON) → Addressable 打包
  4. 运行时 `LevelStreamer` 按距离增量实例化/回收

### 5.3 赛季状态机（SeasonStateMachine）
```csharp
public enum SeasonPhase { Preheat, Active, Settling, Intermission }
public class SeasonManager : MonoBehaviour
{
    SeasonConfig config;           // 当前赛季静态配置
    SeasonRuntimeData runtime;     // 服务器下发的动态数据
    SeasonPhase currentPhase;

    void Awake() => StartCoroutine(CoSyncSeasonState());
    IEnumerator CoSyncSeasonState() { /* 每 5 分钟 / 登录 / 切前台 同步一次 */ }
    public bool IsRewardClaimable(RewardTier tier) => /* 通行证逻辑 */;
}
```

### 5.4 通行证系统（Battle Pass）
- **双轨**：Free / Premium（付费解锁）
- **等级**：1~100 级，每级固定 XP，周上限防刷
- **奖励表**：`BattlePassRewardTable` (ScriptableObject) —— 皮肤、货币、抽奖券、表情动作
- **付费验证**：IAP + 服务器签名收据 → 解锁 Premium 轨道
- **追赶机制**：赛季中途加入可用钻石补齐 XP

### 5.5 限时活动框架（Mini-Event Framework）
```csharp
public abstract class MiniEvent : ScriptableObject
{
    public string EventId;
    public DateTime StartUtc, EndUtc;
    public abstract void OnEnter();
    public abstract void OnExit();
    public abstract void OnUpdate(float dt);
}
// 例：ThemeBossRush, ThemeTokenCollect, ThemePuzzle
```
- 通过 `EventScheduler` 自动按时间表启停
- 进度存 `PlayerPrefs` + 服务器双份，防作弊

### 5.6 热更流程（Unity Cloud Content Delivery / 自建 CDN）
1. 美术/策划在 Unity Editor → `Build Addressables` → 上传到 CDN
2. CI 自动生成 `catalog_<version>.json` + `version.txt`
3. 客户端启动/切前台 → 下载 `version.txt` → 比对 → 增量下载
4. 下载完成 → `Addressables.InitializeAsync()` → 新内容生效，**无需重启**

---

## 6. 美术/音频制作管线（Art & Audio Pipeline）

| 步骤 | 责任人 | 产出 | 校验 |
|------|--------|------|------|
| 概念定稿 | 美术总监 + 策划 | Moodboard + ThemeProfile 填表 | 策划签字 |
| 白模关卡 | 关卡策划 | 5 个白模 Segment | 自动化跑酷 AI 验证通过率 > 95% |
| 高模/贴图 | 环境/角色组 | FBX + PBR 贴图 (4k→1k/512 LOD) | Tri < 50k/段落，DrawCall < 30 |
| 动作/表情 | 动作组 | .anim + AnimationClipOverride | 根节点位移量一致性检查 |
| 音频实现 | 音频组 | WAV 48kHz + Wwise/Unity Audio Mixer | LUFS -14 ±1, 峰值 -1 dBTP |
| 特效/Shader | 技术美术 | VFX Graph / Shader Graph | Overdraw < 2.5, GPU < 2ms/帧 |
| 打包入库 | 构建工程师 | Addressable Bundle | 单包 < 50 MB, 总增量 < 150 MB |
| 灰度测试 | QA + 少量用户 | TestFlight / 内测包 | 崩溃率 < 0.1%, 帧率 P50 > 55fps |

---

## 7. 运营配置表（所有数值策划可在不改代码的情况下调整）

| 表名 | 关键字段 | 热更方式 |
|------|----------|----------|
| `SeasonConfig` | `seasonId, name, startUtc, endUtc, phaseSchedule[]` | Addressable JSON |
| `ThemeProfile` | `chunkWeights, densityCurves, specialMechanics[]` | Addressable ScriptableObject |
| `BattlePassTable` | `tier, freeReward, premiumReward, xpRequired` | Addressable CSV |
| `EventSchedule` | `eventId, phase, startOffsetDays, durationDays` | Addressable JSON |
| `IAPCatalog` | `productId, priceTier, rewardBundleId` | 远程配置 + 客户端缓存 |
| `DifficultyCurve` | `segmentIndex, laneChangeFreq, obstacleDensity, speedMultiplier` | Addressable AnimationCurve |

> **所有表均需版本号**，客户端按 `configVersion` 判断是否需重新拉取。

---

## 8. 埋点与数据指标（Analytics）

| 事件 | 关键属性 | 用途 |
|------|----------|------|
| `season_start` | `season_id, phase, source` | 赛季启动转化 |
| `pass_purchase` | `season_id, tier, price_usd` | 付费漏斗 |
| `pass_level_up` | `season_id, level, xp_source` | 通行证节奏 |
| `segment_complete` | `segment_id, theme, time_ms, crashes` | 关卡难度/流失 |
| `event_participate` | `event_id, completed, reward_tier` | 活动 ROI |
| `skin_equip` | `skin_id, theme, source` | 皮肤人气/变现 |
| `churn_risk` | `days_since_login, last_season_level` | 召回模型特征 |

> 埋点统一走 `AnalyticsManager.Track(eventName, params)`，服务端汇总入数仓。

---

## 9. QA / 自动化测试策略

| 层级 | 工具 | 覆盖 |
|------|------|------|
| 单元 | NUnit + Unity Test Framework | 赛季状态机、通行证逻辑、生成器确定性种子 |
| 集成 | EditMode + PlayMode | 资源加载/卸载、热更流程、事件调度 |
| 端到端 | Appium + 模拟器矩阵 (iOS 15/16/17, Android 11~14) | 新赛季首周核心流程：登录→领奖→跑酷→升级通行证 |
| 性能 | Unity Profiler + Frame Timing | 低端机 (Snapdragon 680 / A12) 30 分钟长跑稳定性 |
| 回归 | 每周五自动跑全量 PlayMode | 防止旧赛季资源泄漏/引用丢失 |

---

## 10. 发布检查清单（Definition of Done）

- [ ] 所有 `ThemeProfile` 通过自动化跑酷 AI 验证（0 硬卡死、< 0.5% 软卡死）
- [ ] Addressable 包体增量 < 150 MB，首包 < 200 MB
- [ ] 通关流程：预热 → 启幕 → 3 个阶段 → 结算 → 间歇期 全流程跑通
- [ ] 通行证免费/付费双轨奖励可正常领取、付费验证通过
- [ ] 限时活动按时间表自动启停，进度跨设备同步
- [ ] 多语言文案 100% 落地，无 Key 缺失
- [ ] 低端机 30 分钟稳定 55+ fps，内存峰值 < 1.2 GB
- [ ] 埋点事件 100% 命中，数仓样本校验通过
- [ ] 灰度包留存/付费/崩溃指标达标 → 全量发布

---

## 11. 后续扩展建议

| 方向 | 说明 |
|------|------|
| **UGC 关卡编辑器** | 玩家用 Chunk 拼装 → 审核 → 官方赛季精选池 |
| **跨赛季传家宝** | 完成全成就解锁「传家宝」皮肤，永久账号绑定 |
| **动态难度 (DDA)** | 实时根据玩家技能调整 Chunk 权重，保持心流 |
| **云端程序化** | 服务端每日生成「每日挑战」种子，全服统一排行 |
| **IP 联动框架** | 抽象 `IPThemePlugin` 接口，一周内接入新 IP 皮肤包 |

---

## 12. 文件落地结构（Unity Project）

```
Assets/
 ├─ _Project/
 │   ├─ Seasons/
 │   │   ├─ Configs/           # SeasonConfig, ThemeProfile, BattlePassTable
 │   │   ├─ Prefabs/           # Segment/Chunk/Event 预制体
 │   │   ├─ Scripts/
 │   │   │   ├─ Runtime/       # SeasonManager, LevelStreamer, EventScheduler
 │   │   │   ├─ Editor/        # 关卡生成器、打包菜单、校验工具
 │   │   │   └─ Tests/         # PlayMode/EditMode 测试
 │   │   ├─ AddressableGroups/ # 每赛季/阶段一个 Group 配置
 │   │   └─ Data/              # CSV/JSON 运营表
 │   ├─ Core/                  # 核心跑酷系统（不随赛季变动）
 │   └─ Common/                # 共用工具、基类、Analytics
 ├─ AddressableAssetsData/     # 自动生成
 └─ StreamingAssets/           # 首包只读配置
```

---

## 13. 下一步行动项（Action Items）

| 优先级 | 任务 | 责任人 | 预计工时 |
|--------|------|--------|----------|
| P0 | 搭建空 Unity 工程 + URP + Addressables + CI/CD | 客户端主程 | 3 天 |
| P0 | 实现 `SeasonManager` + `LevelStreamer` 核心骨架 | 客户端主程 | 5 天 |
| P0 | 程序化关卡生成器 MVP（白模 Chunk 10 个） | 关卡策划 + 程序 | 7 天 |
| P1 | 通行证系统（免费/付费双轨 + 服务器校验） | 服务端 + 客户端 | 5 天 |
| P1 | 热更流程跑通（CDN + Catalog + 增量下载） | 运维 + 客户端 | 3 天 |
| P1 | 首个主题「霓虹赛博」完整美术包交付 | 美术总监 | 10 天 |
| P2 | 限时活动框架 + 2 个示例活动 | 玩法策划 + 程序 | 5 天 |
| P2 | 自动化跑酷 AI 验证管线 | QA + 程序 | 5 天 |
| P3 | 灰度测试方案 + 数据看板 | 数据分析 + 运营 | 3 天 |

---

> **文档到此结束。**  
> 如需把设计落地为 Unity 原型、或细化某个子系统（如程序化生成器、通行证服务端），请告诉我，我可继续输出代码/配置/任务拆解。