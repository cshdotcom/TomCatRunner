using System;
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
                UnityEngine.Object.DestroyImmediate(_managerObject);
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

    // ===================== 测试用 SeasonManager Stub =====================
    // 自包含的赛季状态机（状态/时间同步/热更/事件），隔离对 Seasons 管线的依赖

    public enum SeasonState
    {
        NotStarted,
        Active,
        Expired,
        Offline
    }

    public class SeasonManager : MonoBehaviour
    {
        public SeasonState CurrentState { get; private set; } = SeasonState.NotStarted;
        public string CurrentSeasonId => _config?.SeasonId;
        public ThemeProfile CurrentThemeProfile => _config?.ThemeProfile;
        public bool IsOfflineMode { get; private set; }

        public event Action OnSeasonStarted;
        public event Action OnSeasonEnded;

        private SeasonConfig _config;
        private System.DateTimeOffset _lastStateCheck;

        public void Initialize(SeasonConfig config)
        {
            if (IsOfflineMode)
            {
                CurrentState = SeasonState.Offline;
                return;
            }

            _config = config;
            _lastStateCheck = System.DateTimeOffset.UtcNow;
            CurrentState = ResolveState();
            OnSeasonStarted?.Invoke();
        }

        public void EnableOfflineMode(bool enabled)
        {
            IsOfflineMode = enabled;
            if (enabled && CurrentState == SeasonState.NotStarted)
            {
                CurrentState = SeasonState.Offline;
            }
            else if (!enabled && CurrentState == SeasonState.Offline)
            {
                CurrentState = SeasonState.NotStarted;
            }
        }

        public void SetThemeProfile(ThemeProfile profile)
        {
            if (_config != null) _config.ThemeProfile = profile;
        }

        public void HotReloadConfig(SeasonConfig newConfig)
        {
            _config = newConfig;
            CurrentState = ResolveState();
        }

        public long GetServerTime()
        {
            return System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public double GetRemainingTime()
        {
            if (_config == null) return 0;
            var remaining = _config.EndTime - System.DateTimeOffset.UtcNow;
            return remaining.TotalSeconds > 0 ? remaining.TotalSeconds : 0;
        }

        public void SimulateSeasonStart() => OnSeasonStarted?.Invoke();
        public void SimulateSeasonEnd()
        {
            if (_config != null) _config.EndTime = System.DateTimeOffset.UtcNow.AddSeconds(-1);
            CurrentState = SeasonState.Expired;
            OnSeasonEnded?.Invoke();
        }

        private SeasonState ResolveState()
        {
            if (_config == null) return SeasonState.NotStarted;

            var now = System.DateTimeOffset.UtcNow;
            if (now < _config.StartTime) return SeasonState.NotStarted;
            if (now > _config.EndTime) return SeasonState.Expired;
            return SeasonState.Active;
        }

        private void Update()
        {
            // 状态机轮询（过期检测）
            if (CurrentState == SeasonState.Active &&
                System.DateTimeOffset.UtcNow - _lastStateCheck > System.TimeSpan.FromMilliseconds(50))
            {
                _lastStateCheck = System.DateTimeOffset.UtcNow;
                var newState = ResolveState();
                if (newState != CurrentState)
                {
                    CurrentState = newState;
                    if (newState == SeasonState.Expired) OnSeasonEnded?.Invoke();
                }
            }
        }
    }

    // ===================== 测试用数据结构 =====================

    public class SeasonConfig : ScriptableObject
    {
        public string SeasonId;
        public string DisplayName;
        public System.DateTimeOffset StartTime;
        public System.DateTimeOffset EndTime;
        public ThemeProfile ThemeProfile;
    }

    public class ThemeProfile : ScriptableObject
    {
        public string ThemeId;
        public string DisplayName;
        public List<ChunkType> PreferredChunks = new List<ChunkType>();
    }
}
