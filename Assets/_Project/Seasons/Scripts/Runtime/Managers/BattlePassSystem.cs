using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;
using TomCatRunner.Common;

namespace TomCatRunner.Seasons
{
    /// <summary>
    /// 通行证系统 - 双轨奖励 (免费/付费)、XP 进度、付费验证
    /// </summary>
    public class BattlePassSystem : MonoSingleton<BattlePassSystem>
    {
        [Header("配置引用")]
        [SerializeField] private AssetReference _battlePassTableRef;
        [SerializeField] private BattlePassTable _fallbackTable;

        [Header("XP 设置")]
        [SerializeField] private int _xpPerDistance = 10;           // 每米 XP
        [SerializeField] private int _xpPerCoin = 5;                // 每金币 XP
        [SerializeField] private int _xpPerEnemy = 50;              // 每击败敌人 XP
        [SerializeField] private int _xpPerDailyChallenge = 500;    // 每日挑战完成 XP
        [SerializeField] private float _weeklyCapMultiplier = 1.5f; // 周上限倍率 (付费用户)

        [Header("调试")]
        [SerializeField] private bool _debugLog = true;
        [SerializeField] private bool _useMockIAP = true;

        // 状态
        private BattlePassTable _currentTable;
        private SeasonRuntimeData _runtimeData;
        private int _currentSeasonId;
        private bool _isInitialized = false;

        // 事件
        public event Action<int> OnLevelChanged;
        public event Action<int> OnXpChanged;
        public event Action<int, RewardTrack, RewardBundle> OnRewardClaimed;
        public event Action<bool> OnPremiumStatusChanged;
        public event Action OnBattlePassInitialized;

        // 属性
        public BattlePassTable CurrentTable => _currentTable;
        /// <summary>当前通行证表（与 CurrentTable 同义，供 ViewModel 使用）</summary>
        public BattlePassTable BattlePassTable => CurrentTable;
        public int CurrentLevel => _runtimeData?.battlePassLevel ?? 1;
        public int CurrentXp => _runtimeData?.battlePassXp ?? 0;
        public int XpToNextLevel => GetXpToNextLevel();
        public float LevelProgress => GetLevelProgress();
        public bool IsPremiumUnlocked => _runtimeData?.isPremiumUnlocked ?? false;
        public int MaxLevel => _currentTable?.maxLevel ?? 100;
        public int WeeklyXpCap => GetEffectiveWeeklyCap();
        public bool IsInitialized => _isInitialized;

        protected override void OnInit()
        {
            base.OnInit();
        }

        /// <summary>
        /// 初始化通行证系统
        /// </summary>
        public async UniTask InitializeAsync(int seasonId, SeasonRuntimeData runtimeData)
        {
            _currentSeasonId = seasonId;
            _runtimeData = runtimeData;

            // 加载通行证表
            await LoadBattlePassTableAsync();

            // 如果运行时数据为空，创建新的
            if (_runtimeData.battlePassLevel == 0)
            {
                _runtimeData.battlePassLevel = 1;
                _runtimeData.battlePassXp = 0;
            }

            // 确保等级与 XP 一致
            SyncLevelWithXp();

            _isInitialized = true;
            OnBattlePassInitialized?.Invoke();

            if (_debugLog) Debug.Log($"[BattlePassSystem] Initialized: Level {CurrentLevel}, XP {CurrentXp}, Premium: {IsPremiumUnlocked}");
        }

        private async UniTask LoadBattlePassTableAsync()
        {
            if (_battlePassTableRef.RuntimeKeyIsValid())
            {
                var handle = _battlePassTableRef.LoadAssetAsync<BattlePassTable>();
                await handle.Task;

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    _currentTable = handle.Result;
                }
                else
                {
                    Debug.LogError($"[BattlePassSystem] Failed to load BattlePassTable: {handle.OperationException}");
                    _currentTable = _fallbackTable;
                }
            }
            else
            {
                _currentTable = _fallbackTable;
            }

            if (_currentTable == null)
            {
                _currentTable = CreateDefaultTable();
                Debug.LogWarning("[BattlePassSystem] Using default BattlePassTable");
            }
        }

        private BattlePassTable CreateDefaultTable()
        {
            var table = ScriptableObject.CreateInstance<BattlePassTable>();
            table.maxLevel = 100;
            table.weeklyXpCap = 50000;
            table.xpCurve = AnimationCurve.Linear(0, 0, 100, 1000000);
            table.tiers = new BattlePassTier[100];

            for (int i = 0; i < 100; i++)
            {
                int level = i + 1;
                int xpRequired = Mathf.RoundToInt(table.xpCurve.Evaluate(level / 100f));

                table.tiers[i] = new BattlePassTier
                {
                    level = level,
                    xpRequired = xpRequired,
                    freeReward = new RewardBundle
                    {
                        items = level % 5 == 0
                            ? new[] { new RewardItem { type = RewardType.Currency, itemId = "coins", quantity = level * 100, rarity = Rarity.Common } }
                            : new[] { new RewardItem { type = RewardType.Experience, itemId = "xp", quantity = 100, rarity = Rarity.Common } }
                    },
                    premiumReward = new RewardBundle
                    {
                        items = new RewardItem[]
                        {
                            new RewardItem { type = RewardType.Currency, itemId = "gems", quantity = level * 10, rarity = Rarity.Uncommon },
                            new RewardItem { type = level % 10 == 0 ? RewardType.Skin : RewardType.Currency, itemId = level % 10 == 0 ? $"skin_{level}" : "coins", quantity = level % 10 == 0 ? 1 : level * 200, rarity = level % 10 == 0 ? Rarity.Epic : Rarity.Common }
                        }
                    },
                    isMilestone = level % 10 == 0,
                    milestoneTitle = level % 10 == 0 ? $"Tier {level / 10} Milestone" : ""
                };
            }

            return table;
        }

        // ===================== XP 管理 =====================

        /// <summary>
        /// 增加 XP (游戏内调用)
        /// </summary>
        public void AddXp(int amount, XpSource source = XpSource.Gameplay)
        {
            if (!_isInitialized || _runtimeData == null) return;

            // 检查周上限
            if (_runtimeData.battlePassXp >= WeeklyXpCap)
            {
                if (_debugLog) Debug.Log("[BattlePassSystem] Weekly XP cap reached");
                return;
            }

            int oldXp = _runtimeData.battlePassXp;
            int newXp = Mathf.Min(oldXp + amount, WeeklyXpCap);
            _runtimeData.battlePassXp = newXp;

            // 检查升级
            int oldLevel = _runtimeData.battlePassLevel;
            int newLevel = CalculateLevelFromXp(newXp);

            if (newLevel > oldLevel)
            {
                _runtimeData.battlePassLevel = newLevel;
                OnLevelChanged?.Invoke(newLevel);

                if (_debugLog) Debug.Log($"[BattlePassSystem] Level Up! {oldLevel} -> {newLevel}");
            }

            OnXpChanged?.Invoke(newXp);
        }

        /// <summary>
        /// 便捷方法：跑酷距离奖励
        /// </summary>
        public void AddDistanceXp(float distanceMeters)
        {
            AddXp(Mathf.RoundToInt(distanceMeters * _xpPerDistance), XpSource.Distance);
        }

        /// <summary>
        /// 便捷方法：金币奖励
        /// </summary>
        public void AddCoinXp(int coinCount)
        {
            AddXp(coinCount * _xpPerCoin, XpSource.Coins);
        }

        /// <summary>
        /// 便捷方法：击败敌人奖励
        /// </summary>
        public void AddEnemyXp(int enemyCount)
        {
            AddXp(enemyCount * _xpPerEnemy, XpSource.Enemies);
        }

        /// <summary>
        /// 便捷方法：每日挑战完成
        /// </summary>
        public void AddDailyChallengeXp()
        {
            AddXp(_xpPerDailyChallenge, XpSource.DailyChallenge);
        }

        private void SyncLevelWithXp()
        {
            int correctLevel = CalculateLevelFromXp(_runtimeData.battlePassXp);
            if (correctLevel != _runtimeData.battlePassLevel)
            {
                _runtimeData.battlePassLevel = correctLevel;
            }
        }

        private int CalculateLevelFromXp(int xp)
        {
            if (_currentTable?.tiers == null) return 1;

            for (int i = _currentTable.tiers.Length - 1; i >= 0; i--)
            {
                if (xp >= _currentTable.tiers[i].xpRequired)
                    return _currentTable.tiers[i].level;
            }
            return 1;
        }

        private int GetXpToNextLevel()
        {
            if (_currentTable?.tiers == null) return 0;

            int currentLevel = CurrentLevel;
            if (currentLevel >= MaxLevel) return 0;

            var nextTier = _currentTable.tiers.FirstOrDefault(t => t.level == currentLevel + 1);
            if (nextTier == null) return 0;

            return Mathf.Max(0, nextTier.xpRequired - CurrentXp);
        }

        private float GetLevelProgress()
        {
            if (_currentTable?.tiers == null) return 0f;

            int currentLevel = CurrentLevel;
            if (currentLevel >= MaxLevel) return 1f;

            var currentTier = _currentTable.tiers.FirstOrDefault(t => t.level == currentLevel);
            var nextTier = _currentTable.tiers.FirstOrDefault(t => t.level == currentLevel + 1);

            if (currentTier == null || nextTier == null) return 0f;

            int currentXp = CurrentXp;
            int startXp = currentTier.xpRequired;
            int endXp = nextTier.xpRequired;

            return Mathf.InverseLerp(startXp, endXp, currentXp);
        }

        private int GetEffectiveWeeklyCap()
        {
            int baseCap = _currentTable?.weeklyXpCap ?? 50000;
            return IsPremiumUnlocked ? Mathf.RoundToInt(baseCap * _weeklyCapMultiplier) : baseCap;
        }

        // ===================== 奖励领取 =====================

        /// <summary>
        /// 检查奖励是否可领取
        /// </summary>
        public bool CanClaim(int level, RewardTrack track)
        {
            if (!_isInitialized || _runtimeData == null || _currentTable == null) return false;
            if (level > CurrentLevel) return false;

            var claimedList = track == RewardTrack.Free ? _runtimeData.claimedFreeTiers : _runtimeData.claimedPremiumTiers;
            return !claimedList.Contains(level);
        }

        /// <summary>
        /// 获取指定等级的奖励预览
        /// </summary>
        public RewardBundle GetRewardPreview(int level, RewardTrack track)
        {
            if (_currentTable == null) return null;

            var tier = _currentTable.tiers.FirstOrDefault(t => t.level == level);
            if (tier == null) return null;

            return track == RewardTrack.Free ? tier.freeReward : tier.premiumReward;
        }

        /// <summary>
        /// 领取奖励
        /// </summary>
        public async UniTask<RewardBundle> ClaimRewardAsync(int level, RewardTrack track)
        {
            if (!CanClaim(level, track))
                throw new InvalidOperationException($"Cannot claim level {level} {track} reward");

            var reward = GetRewardPreview(level, track);
            if (reward == null) throw new ArgumentException($"No reward for level {level}");

            // 标记已领取
            if (track == RewardTrack.Free)
                _runtimeData.claimedFreeTiers.Add(level);
            else
                _runtimeData.claimedPremiumTiers.Add(level);

            OnRewardClaimed?.Invoke(level, track, reward);

            // 同步到服务器
            if (!_useMockIAP)
            {
                // await ApiClient.ClaimBattlePassReward(_currentSeasonId, level, track);
            }

            // 发放奖励物品
            await GrantRewardItemsAsync(reward);

            return reward;
        }

        /// <summary>
        /// 一键领取所有可用奖励
        /// </summary>
        public async UniTask<List<RewardBundle>> ClaimAllAvailableAsync()
        {
            var results = new List<RewardBundle>();

            for (int level = 1; level <= CurrentLevel; level++)
            {
                if (CanClaim(level, RewardTrack.Free))
                {
                    var reward = await ClaimRewardAsync(level, RewardTrack.Free);
                    results.Add(reward);
                }

                if (IsPremiumUnlocked && CanClaim(level, RewardTrack.Premium))
                {
                    var reward = await ClaimRewardAsync(level, RewardTrack.Premium);
                    results.Add(reward);
                }
            }

            return results;
        }

        private async UniTask GrantRewardItemsAsync(RewardBundle bundle)
        {
            if (bundle?.items == null) return;

            foreach (var item in bundle.items)
            {
                await GrantSingleItemAsync(item);
            }
        }

        private async UniTask GrantSingleItemAsync(RewardItem item)
        {
            // 这里对接实际的背包/货币系统
            switch (item.type)
            {
                case RewardType.Currency:
                    // await CurrencySystem.Add(item.itemId, item.quantity);
                    break;
                case RewardType.Skin:
                    // await InventorySystem.UnlockSkin(item.itemId);
                    break;
                case RewardType.Mount:
                    // await InventorySystem.UnlockMount(item.itemId);
                    break;
                case RewardType.Pet:
                    // await InventorySystem.UnlockPet(item.itemId);
                    break;
                case RewardType.Emote:
                    // await InventorySystem.UnlockEmote(item.itemId);
                    break;
                case RewardType.Title:
                    // await ProfileSystem.UnlockTitle(item.itemId);
                    break;
                case RewardType.ProfileFrame:
                    // await ProfileSystem.UnlockFrame(item.itemId);
                    break;
                case RewardType.LootBox:
                    // await InventorySystem.AddLootBox(item.itemId, item.quantity);
                    break;
                case RewardType.Experience:
                    AddXp(item.quantity, XpSource.Reward);
                    break;
            }

            await UniTask.Yield();
        }

        // ===================== 付费解锁 =====================

        /// <summary>
        /// 购买高级通行证 (IAP)
        /// </summary>
        public async UniTask<bool> PurchasePremiumPassAsync(string productId)
        {
            if (IsPremiumUnlocked)
            {
                if (_debugLog) Debug.Log("[BattlePassSystem] Premium already unlocked");
                return true;
            }

            bool success = false;

            if (_useMockIAP)
            {
                // 模拟购买成功
                await UniTask.Delay(500);
                success = true;
            }
            else
            {
                // 真实 IAP 流程
                // success = await IAPManager.Purchase(productId);
            }

            if (success)
            {
                _runtimeData.isPremiumUnlocked = true;
                OnPremiumStatusChanged?.Invoke(true);

                // 补发已解锁等级的付费奖励
                var rewards = await ClaimAllAvailableAsync();

                if (_debugLog) Debug.Log($"[BattlePassSystem] Premium unlocked! Retroactive rewards: {rewards.Count}");
            }

            return success;
        }

        /// <summary>
        /// 恢复购买
        /// </summary>
        public async UniTask RestorePurchasesAsync()
        {
            if (_useMockIAP)
            {
                // 模拟：检查本地记录
                _runtimeData.isPremiumUnlocked = true;
                OnPremiumStatusChanged?.Invoke(true);
            }
            else
            {
                // await IAPManager.RestorePurchases();
            }
        }

        // ===================== 数据查询 =====================

        /// <summary>
        /// 获取所有等级的奖励信息 (用于 UI 列表)
        /// </summary>
        public List<BattlePassTierViewData> GetAllTiersViewData()
        {
            if (_currentTable?.tiers == null) return new List<BattlePassTierViewData>();

            return _currentTable.tiers.Select(t => new BattlePassTierViewData
            {
                level = t.level,
                xpRequired = t.xpRequired,
                isCurrentLevel = t.level == CurrentLevel,
                isUnlocked = t.level <= CurrentLevel,
                isClaimedFree = _runtimeData.claimedFreeTiers.Contains(t.level),
                isClaimedPremium = _runtimeData.claimedPremiumTiers.Contains(t.level),
                canClaimFree = CanClaim(t.level, RewardTrack.Free),
                canClaimPremium = IsPremiumUnlocked && CanClaim(t.level, RewardTrack.Premium),
                freeReward = t.freeReward,
                premiumReward = t.premiumReward,
                isMilestone = t.isMilestone,
                milestoneTitle = t.milestoneTitle
            }).ToList();
        }

        /// <summary>
        /// 获取下一个里程碑等级
        /// </summary>
        public int GetNextMilestoneLevel()
        {
            if (_currentTable?.tiers == null) return -1;

            return _currentTable.tiers
                .Where(t => t.isMilestone && t.level > CurrentLevel)
                .Select(t => t.level)
                .FirstOrDefault();
        }

        /// <summary>
        /// 获取 UI 快照数据（ViewModel 一次性拉取）
        /// </summary>
        public BattlePassViewDataSnapshot GetBattlePassViewData()
        {
            var runtime = _runtimeData;
            return new BattlePassViewDataSnapshot
            {
                currentTier = CurrentLevel,
                currentXP = CurrentXp,
                nextTierXP = XpToNextLevel,
                tierProgress = LevelProgress,
                overallProgress = MaxLevel > 1
                    ? Mathf.Clamp01((CurrentLevel - 1) / (float)(MaxLevel - 1))
                    : 0f,
                isPremiumUnlocked = IsPremiumUnlocked,
                weeklyXPEarned = runtime?.battlePassXp ?? 0,
                weeklyCap = WeeklyXpCap,
                lastWeeklyReset = runtime?.lastLoginTimestamp ?? 0,
                claimedFreeTiers = runtime?.claimedFreeTiers ?? new List<int>(),
                claimedPremiumTiers = runtime?.claimedPremiumTiers ?? new List<int>()
            };
        }
    }

    /// <summary>
    /// UI 一次性拉取的通行证快照数据
    /// </summary>
    [Serializable]
    public class BattlePassViewDataSnapshot
    {
        public int currentTier;
        public int currentXP;
        public int nextTierXP;
        public float tierProgress;
        public float overallProgress;
        public bool isPremiumUnlocked;
        public int weeklyXPEarned;
        public int weeklyCap;
        public long lastWeeklyReset;
        public List<int> claimedFreeTiers = new List<int>();
        public List<int> claimedPremiumTiers = new List<int>();
    }

    /// <summary>
    /// UI 显示用的通行证等级数据
    /// </summary>
    [Serializable]
    public class BattlePassTierViewData
    {
        public int level;
        public int xpRequired;
        public bool isCurrentLevel;
        public bool isUnlocked;
        public bool isClaimedFree;
        public bool isClaimedPremium;
        public bool canClaimFree;
        public bool canClaimPremium;
        public RewardBundle freeReward;
        public RewardBundle premiumReward;
        public bool isMilestone;
        public string milestoneTitle;
    }

    public enum XpSource
    {
        Gameplay,
        Distance,
        Coins,
        Enemies,
        DailyChallenge,
        Reward,
        Purchase
    }
}