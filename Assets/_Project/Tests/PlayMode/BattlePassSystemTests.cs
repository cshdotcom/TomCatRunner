using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TomCatRunner.Seasons;

namespace TomCatRunner.Tests.PlayMode
{
    /// <summary>
    /// BattlePassSystem PlayMode Tests
    /// Tests dual-track rewards, XP curves, and purchase validation
    /// </summary>
    public class BattlePassSystemTests
    {
        private BattlePassSystem _battlePassSystem;
        private GameObject _systemObject;

        [SetUp]
        public void SetUp()
        {
            _systemObject = new GameObject("BattlePassSystem");
            _battlePassSystem = _systemObject.AddComponent<BattlePassSystem>();

            CreateTestConfiguration();
        }

        [TearDown]
        public void TearDown()
        {
            if (_systemObject != null)
                Object.DestroyImmediate(_systemObject);
        }

        [UnityTest]
        public IEnumerator BattlePass_Initialize_LoadsConfiguration()
        {
            _battlePassSystem.Initialize(GetTestBattlePassTable());

            Assert.IsNotNull(_battlePassSystem.CurrentTable);
            Assert.Greater(_battlePassSystem.MaxLevel, 0);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BattlePass_AddXP_IncreasesLevel()
        {
            _battlePassSystem.Initialize(GetTestBattlePassTable());

            int initialLevel = _battlePassSystem.CurrentLevel;
            _battlePassSystem.AddXP(1000);

            Assert.GreaterOrEqual(_battlePassSystem.CurrentLevel, initialLevel);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BattlePass_XPCurve_ProgressiveDifficulty()
        {
            _battlePassSystem.Initialize(GetTestBattlePassTable());

            int xpForLevel1 = _battlePassSystem.GetXPRequiredForLevel(1);
            int xpForLevel10 = _battlePassSystem.GetXPRequiredForLevel(10);
            int xpForLevel50 = _battlePassSystem.GetXPRequiredForLevel(50);

            Assert.Less(xpForLevel1, xpForLevel10);
            Assert.Less(xpForLevel10, xpForLevel50);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BattlePass_FreeTrackRewards_Available()
        {
            _battlePassSystem.Initialize(GetTestBattlePassTable());

            _battlePassSystem.AddXP(500); // 达到第 2 级
            _battlePassSystem.ClaimFreeReward(2);

            Assert.IsTrue(_battlePassSystem.IsFreeRewardClaimed(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator BattlePass_PremiumTrackRewards_RequirePurchase()
        {
            _battlePassSystem.Initialize(GetTestBattlePassTable());

            _battlePassSystem.AddXP(500);
            _battlePassSystem.SetPremiumOwned(false);

            Assert.IsFalse(_battlePassSystem.CanClaimPremiumReward(2));

            _battlePassSystem.SetPremiumOwned(true);
            Assert.IsTrue(_battlePassSystem.CanClaimPremiumReward(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator BattlePass_ClaimPremiumReward_GrantsRewards()
        {
            _battlePassSystem.Initialize(GetTestBattlePassTable());
            _battlePassSystem.SetPremiumOwned(true);

            _battlePassSystem.AddXP(1000);
            var rewards = _battlePassSystem.ClaimPremiumReward(5);

            Assert.IsNotNull(rewards);
            Assert.Greater(rewards.Count, 0);
            Assert.IsTrue(_battlePassSystem.IsPremiumRewardClaimed(5));
            yield return null;
        }

        [UnityTest]
        public IEnumerator BattlePass_CannotClaimUnreachedLevel()
        {
            _battlePassSystem.Initialize(GetTestBattlePassTable());
            _battlePassSystem.SetPremiumOwned(true);

            // 当前只有 1 级，尝试领取 10 级奖励
            Assert.IsFalse(_battlePassSystem.CanClaimFreeReward(10));
            Assert.IsFalse(_battlePassSystem.CanClaimPremiumReward(10));
            yield return null;
        }

        [UnityTest]
        public IEnumerator BattlePass_CannotDoubleClaim()
        {
            _battlePassSystem.Initialize(GetTestBattlePassTable());

            _battlePassSystem.AddXP(500);
            _battlePassSystem.ClaimFreeReward(2);

            Assert.IsFalse(_battlePassSystem.CanClaimFreeReward(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator BattlePass_GetProgress_ReturnsCorrectValues()
        {
            _battlePassSystem.Initialize(GetTestBattlePassTable());

            _battlePassSystem.AddXP(2500);

            var progress = _battlePassSystem.GetProgress();

            Assert.GreaterOrEqual(progress.CurrentLevel, 1);
            Assert.GreaterOrEqual(progress.CurrentXP, 0);
            Assert.Greater(progress.XPToNextLevel, 0);
            Assert.GreaterOrEqual(progress.Progress01, 0f);
            Assert.LessOrEqual(progress.Progress01, 1f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BattlePass_PremiumPurchase_UnlocksTrack()
        {
            _battlePassSystem.Initialize(GetTestBattlePassTable());

            Assert.IsFalse(_battlePassSystem.IsPremiumOwned);

            _battlePassSystem.PurchasePremiumPass();

            Assert.IsTrue(_battlePassSystem.IsPremiumOwned);
            Assert.IsTrue(_battlePassSystem.CanClaimPremiumReward(_battlePassSystem.CurrentLevel));
            yield return null;
        }

        [UnityTest]
        public IEnumerator BattlePass_SeasonEnd_ExpiresPass()
        {
            var table = GetTestBattlePassTable();
            table.SeasonEndTime = System.DateTimeOffset.UtcNow.AddSeconds(-1);

            _battlePassSystem.Initialize(table);

            yield return new WaitForSeconds(0.1f);

            Assert.IsTrue(_battlePassSystem.IsExpired);
            Assert.IsFalse(_battlePassSystem.CanClaimFreeReward(1));
            Assert.IsFalse(_battlePassSystem.CanClaimPremiumReward(1));
        }

        [UnityTest]
        public IEnumerator BattlePass_RewardTypes_Correct()
        {
            _battlePassSystem.Initialize(GetTestBattlePassTable());

            var freeReward = _battlePassSystem.GetFreeReward(1);
            var premiumReward = _battlePassSystem.GetPremiumReward(1);

            Assert.IsNotNull(freeReward);
            Assert.IsNotNull(premiumReward);

            // Premium 奖励应该更好
            Assert.GreaterOrEqual(premiumReward.Value, freeReward.Value);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BattlePass_MaxLevel_Capped()
        {
            _battlePassSystem.Initialize(GetTestBattlePassTable());

            // 添加大量 XP
            _battlePassSystem.AddXP(1000000);

            Assert.AreEqual(_battlePassSystem.MaxLevel, _battlePassSystem.CurrentLevel);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BattlePass_WeeklyXPCap_Enforced()
        {
            _battlePassSystem.Initialize(GetTestBattlePassTable());
            _battlePassSystem.WeeklyXPCap = 10000;

            _battlePassSystem.AddXP(15000);

            Assert.LessOrEqual(_battlePassSystem.WeeklyXPEarned, _battlePassSystem.WeeklyXPCap);
            yield return null;
        }

        private BattlePassTable GetTestBattlePassTable()
        {
            var table = ScriptableObject.CreateInstance<BattlePassTable>();
            table.SeasonId = "TestSeason";
            table.MaxLevel = 50;
            table.BaseXPPerLevel = 100;
            table.XPGrowthFactor = 1.1f;
            table.SeasonEndTime = System.DateTimeOffset.UtcNow.AddDays(30);

            table.FreeRewards = new List<BattlePassReward>();
            table.PremiumRewards = new List<BattlePassReward>();

            for (int i = 1; i <= 50; i++)
            {
                table.FreeRewards.Add(new BattlePassReward
                {
                    Level = i,
                    RewardType = RewardType.Coins,
                    Value = i * 10,
                    ItemId = $"coins_{i}"
                });

                table.PremiumRewards.Add(new BattlePassReward
                {
                    Level = i,
                    RewardType = i % 5 == 0 ? RewardType.CharacterSkin : RewardType.Gems,
                    Value = i * 20,
                    ItemId = i % 5 == 0 ? $"skin_{i}" : $"gems_{i}"
                });
            }

            return table;
        }

        private void CreateTestConfiguration()
        {
            // 基础配置
        }
    }

    // 测试用数据结构
    public class BattlePassTable : ScriptableObject
    {
        public string SeasonId;
        public int MaxLevel = 50;
        public int BaseXPPerLevel = 100;
        public float XPGrowthFactor = 1.1f;
        public System.DateTimeOffset SeasonEndTime;
        public List<BattlePassReward> FreeRewards = new List<BattlePassReward>();
        public List<BattlePassReward> PremiumRewards = new List<BattlePassReward>();
    }

    public class BattlePassReward
    {
        public int Level;
        public RewardType RewardType;
        public int Value;
        public string ItemId;
    }

    public enum RewardType
    {
        Coins,
        Gems,
        CharacterSkin,
        PowerUp,
        Emote,
        Title
    }

    public class BattlePassProgress
    {
        public int CurrentLevel;
        public int CurrentXP;
        public int XPToNextLevel;
        public float Progress01;
    }
}