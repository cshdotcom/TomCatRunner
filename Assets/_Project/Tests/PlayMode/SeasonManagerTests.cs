using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TomCatRunner.Seasons;

namespace TomCatRunner.Tests.PlayMode
{
    /// <summary>
    /// SeasonManager PlayMode Tests
    /// Tests season state machine, time synchronization, and configuration hot-reload
    /// </summary>
    public class SeasonManagerTests
    {
        private SeasonManager _seasonManager;
        private GameObject _managerObject;

        [SetUp]
        public void SetUp()
        {
            _managerObject = new GameObject("SeasonManager");
            _seasonManager = _managerObject.AddComponent<SeasonManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_managerObject != null)
                Object.DestroyImmediate(_managerObject);
        }

        [UnityTest]
        public IEnumerator SeasonManager_InitialState_IsNotStarted()
        {
            Assert.AreEqual(SeasonState.NotStarted, _seasonManager.CurrentState);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SeasonManager_Initialize_SetsUpCorrectly()
        {
            var config = CreateTestSeasonConfig();
            _seasonManager.Initialize(config);

            Assert.AreEqual(SeasonState.Active, _seasonManager.CurrentState);
            Assert.AreEqual(config.SeasonId, _seasonManager.CurrentSeasonId);
            Assert.IsNotNull(_seasonManager.CurrentThemeProfile);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SeasonManager_TimeSync_ReturnsValidTime()
        {
            var config = CreateTestSeasonConfig();
            _seasonManager.Initialize(config);

            var serverTime = _seasonManager.GetServerTime();
            Assert.Greater(serverTime, 0);

            var remaining = _seasonManager.GetRemainingTime();
            Assert.GreaterOrEqual(remaining, 0);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SeasonManager_SeasonExpired_ReturnsExpiredState()
        {
            var config = CreateTestSeasonConfig();
            config.EndTime = System.DateTimeOffset.UtcNow.AddSeconds(-10); // 已过期
            _seasonManager.Initialize(config);

            // 等待状态机更新
            yield return new WaitForSeconds(0.1f);

            Assert.AreEqual(SeasonState.Expired, _seasonManager.CurrentState);
        }

        [UnityTest]
        public IEnumerator SeasonManager_ThemeChange_UpdatesThemeProfile()
        {
            var config = CreateTestSeasonConfig();
            _seasonManager.Initialize(config);

            var originalTheme = _seasonManager.CurrentThemeProfile;
            var newTheme = CreateTestThemeProfile("NewTheme");

            _seasonManager.SetThemeProfile(newTheme);

            Assert.AreEqual(newTheme, _seasonManager.CurrentThemeProfile);
            Assert.AreNotEqual(originalTheme, _seasonManager.CurrentThemeProfile);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SeasonManager_OfflineMode_WorksWithoutServer()
        {
            // 不初始化配置，测试离线模式
            _seasonManager.EnableOfflineMode(true);

            Assert.IsTrue(_seasonManager.IsOfflineMode);
            Assert.AreEqual(SeasonState.Offline, _seasonManager.CurrentState);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SeasonManager_ConfigHotReload_UpdatesConfig()
        {
            var config1 = CreateTestSeasonConfig("Season1");
            _seasonManager.Initialize(config1);

            var config2 = CreateTestSeasonConfig("Season2");
            _seasonManager.HotReloadConfig(config2);

            Assert.AreEqual("Season2", _seasonManager.CurrentSeasonId);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SeasonManager_SeasonTransition_FiresEvents()
        {
            var config = CreateTestSeasonConfig();
            _seasonManager.Initialize(config);

            bool seasonStarted = false;
            bool seasonEnded = false;

            _seasonManager.OnSeasonStarted += () => seasonStarted = true;
            _seasonManager.OnSeasonEnded += () => seasonEnded = true;

            // 模拟赛季开始
            _seasonManager.SimulateSeasonStart();
            yield return null;
            Assert.IsTrue(seasonStarted);

            // 模拟赛季结束
            _seasonManager.SimulateSeasonEnd();
            yield return null;
            Assert.IsTrue(seasonEnded);
        }

        private SeasonConfig CreateTestSeasonConfig(string seasonId = "TestSeason")
        {
            var config = ScriptableObject.CreateInstance<SeasonConfig>();
            config.SeasonId = seasonId;
            config.DisplayName = "Test Season";
            config.StartTime = System.DateTimeOffset.UtcNow.AddMinutes(-5);
            config.EndTime = System.DateTimeOffset.UtcNow.AddDays(30);
            config.ThemeProfile = CreateTestThemeProfile("DefaultTheme");
            return config;
        }

        private ThemeProfile CreateTestThemeProfile(string themeId)
        {
            var profile = ScriptableObject.CreateInstance<ThemeProfile>();
            profile.ThemeId = themeId;
            profile.DisplayName = themeId;
            return profile;
        }
    }
}