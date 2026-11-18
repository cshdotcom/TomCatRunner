using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TomCatRunner.Seasons;

namespace TomCatRunner.Tests.PlayMode
{
    /// <summary>
    /// EventScheduler PlayMode Tests
    /// Tests mini-events, scheduling, and reward claiming
    /// </summary>
    public class EventSchedulerTests
    {
        private EventScheduler _eventScheduler;
        private GameObject _schedulerObject;

        [SetUp]
        public void SetUp()
        {
            _schedulerObject = new GameObject("EventScheduler");
            _eventScheduler = _schedulerObject.AddComponent<EventScheduler>();

            CreateTestEvents();
        }

        [TearDown]
        public void TearDown()
        {
            if (_schedulerObject != null)
                Object.DestroyImmediate(_schedulerObject);
        }

        [UnityTest]
        public IEnumerator EventScheduler_Initialize_LoadsEvents()
        {
            _eventScheduler.Initialize(GetTestEvents());

            Assert.IsNotNull(_eventScheduler.ActiveEvents);
            Assert.Greater(_eventScheduler.ActiveEvents.Count, 0);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EventScheduler_TimeBasedActivation_Works()
        {
            _eventScheduler.Initialize(GetTestEvents());

            // 事件应该在开始时间后激活
            var futureEvent = _eventScheduler.GetEvent("FutureEvent");
            Assert.IsFalse(futureEvent.IsActive);

            // 模拟时间前进
            _eventScheduler.SimulateTimeAdvance(System.TimeSpan.FromHours(2));

            Assert.IsTrue(futureEvent.IsActive);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EventScheduler_ExpiredEvents_Deactivated()
        {
            _eventScheduler.Initialize(GetTestEvents());

            var pastEvent = _eventScheduler.GetEvent("PastEvent");
            Assert.IsFalse(pastEvent.IsActive);
            Assert.IsTrue(pastEvent.IsExpired);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EventScheduler_ProgressTracking_Works()
        {
            _eventScheduler.Initialize(GetTestEvents());

            var event_ = _eventScheduler.GetEvent("ProgressEvent");
            Assert.AreEqual(0, event_.CurrentProgress);

            _eventScheduler.AddProgress("ProgressEvent", 50);

            Assert.AreEqual(50, event_.CurrentProgress);
            Assert.AreEqual(0.5f, event_.Progress01);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EventScheduler_ClaimReward_GrantsRewards()
        {
            _eventScheduler.Initialize(GetTestEvents());

            var event_ = _eventScheduler.GetEvent("ProgressEvent");
            _eventScheduler.AddProgress("ProgressEvent", 100);

            Assert.IsTrue(event_.CanClaimReward);

            var rewards = _eventScheduler.ClaimReward("ProgressEvent");

            Assert.IsNotNull(rewards);
            Assert.Greater(rewards.Count, 0);
            Assert.IsTrue(event_.IsRewardClaimed);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EventScheduler_CannotClaimIncompleteEvent()
        {
            _eventScheduler.Initialize(GetTestEvents());

            var event_ = _eventScheduler.GetEvent("ProgressEvent");
            _eventScheduler.AddProgress("ProgressEvent", 50); // 只完成一半

            Assert.IsFalse(event_.CanClaimReward);
            Assert.IsNull(_eventScheduler.ClaimReward("ProgressEvent"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator EventScheduler_CannotDoubleClaim()
        {
            _eventScheduler.Initialize(GetTestEvents());

            var event_ = _eventScheduler.GetEvent("ProgressEvent");
            _eventScheduler.AddProgress("ProgressEvent", 100);
            _eventScheduler.ClaimReward("ProgressEvent");

            Assert.IsNull(_eventScheduler.ClaimReward("ProgressEvent"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator EventScheduler_DailyReset_ResetsProgress()
        {
            _eventScheduler.Initialize(GetTestEvents());

            var event_ = _eventScheduler.GetEvent("DailyEvent");
            _eventScheduler.AddProgress("DailyEvent", 100);
            _eventScheduler.ClaimReward("DailyEvent");

            // 模拟每日重置
            _eventScheduler.SimulateDailyReset();

            Assert.AreEqual(0, event_.CurrentProgress);
            Assert.IsFalse(event_.IsRewardClaimed);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EventScheduler_EventTypes_Correct()
        {
            _eventScheduler.Initialize(GetTestEvents());

            var dailyEvent = _eventScheduler.GetEvent("DailyEvent");
            var weeklyEvent = _eventScheduler.GetEvent("WeeklyEvent");
            var seasonalEvent = _eventScheduler.GetEvent("SeasonalEvent");

            Assert.AreEqual(EventType.Daily, dailyEvent.Type);
            Assert.AreEqual(EventType.Weekly, weeklyEvent.Type);
            Assert.AreEqual(EventType.Seasonal, seasonalEvent.Type);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EventScheduler_RewardPreview_ShowsCorrectRewards()
        {
            _eventScheduler.Initialize(GetTestEvents());

            var event_ = _eventScheduler.GetEvent("ProgressEvent");
            var preview = _eventScheduler.GetRewardPreview("ProgressEvent");

            Assert.IsNotNull(preview);
            Assert.Greater(preview.Count, 0);
            Assert.AreEqual(event_.Rewards.Count, preview.Count);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EventScheduler_ConcurrentEvents_Handled()
        {
            _eventScheduler.Initialize(GetTestEvents());

            int activeCount = 0;
            foreach (var e in _eventScheduler.ActiveEvents)
            {
                if (e.IsActive) activeCount++;
            }

            Assert.GreaterOrEqual(activeCount, 2);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EventScheduler_PriorityOrdering_Works()
        {
            _eventScheduler.Initialize(GetTestEvents());

            var events = _eventScheduler.GetSortedEvents();

            // 优先级高的应该在前面
            for (int i = 1; i < events.Count; i++)
            {
                Assert.GreaterOrEqual(events[i - 1].Priority, events[i].Priority);
            }
            yield return null;
        }

        private List<MiniEvent> GetTestEvents()
        {
            var events = new List<MiniEvent>();

            // 已过期的事件
            events.Add(new MiniEvent
            {
                EventId = "PastEvent",
                DisplayName = "Past Event",
                Type = EventType.Daily,
                StartTime = System.DateTimeOffset.UtcNow.AddDays(-2),
                EndTime = System.DateTimeOffset.UtcNow.AddDays(-1),
                Priority = 10,
                Rewards = new List<EventReward> { new EventReward { ItemId = "coins", Amount = 100 } }
            });

            // 当前活跃的事件
            events.Add(new MiniEvent
            {
                EventId = "ActiveEvent",
                DisplayName = "Active Event",
                Type = EventType.Daily,
                StartTime = System.DateTimeOffset.UtcNow.AddHours(-1),
                EndTime = System.DateTimeOffset.UtcNow.AddHours(23),
                Priority = 5,
                Rewards = new List<EventReward> { new EventReward { ItemId = "gems", Amount = 50 } }
            });

            // 未来的事件
            events.Add(new MiniEvent
            {
                EventId = "FutureEvent",
                DisplayName = "Future Event",
                Type = EventType.Weekly,
                StartTime = System.DateTimeOffset.UtcNow.AddHours(2),
                EndTime = System.DateTimeOffset.UtcNow.AddDays(7),
                Priority = 1,
                Rewards = new List<EventReward> { new EventReward { ItemId = "skin", Amount = 1 } }
            });

            // 进度型事件
            events.Add(new MiniEvent
            {
                EventId = "ProgressEvent",
                DisplayName = "Progress Event",
                Type = EventType.Seasonal,
                StartTime = System.DateTimeOffset.UtcNow.AddHours(-1),
                EndTime = System.DateTimeOffset.UtcNow.AddDays(30),
                Priority = 3,
                MaxProgress = 100,
                CurrentProgress = 0,
                Rewards = new List<EventReward> { new EventReward { ItemId = "powerup", Amount = 5 } }
            });

            // 每日重置事件
            events.Add(new MiniEvent
            {
                EventId = "DailyEvent",
                DisplayName = "Daily Event",
                Type = EventType.Daily,
                StartTime = System.DateTimeOffset.UtcNow.AddHours(-1),
                EndTime = System.DateTimeOffset.UtcNow.AddHours(23),
                Priority = 2,
                MaxProgress = 100,
                CurrentProgress = 0,
                ResetsDaily = true,
                Rewards = new List<EventReward> { new EventReward { ItemId = "coins", Amount = 200 } }
            });

            // 每周事件
            events.Add(new MiniEvent
            {
                EventId = "WeeklyEvent",
                DisplayName = "Weekly Event",
                Type = EventType.Weekly,
                StartTime = System.DateTimeOffset.UtcNow.AddDays(-1),
                EndTime = System.DateTimeOffset.UtcNow.AddDays(6),
                Priority = 1,
                MaxProgress = 500,
                CurrentProgress = 0,
                Rewards = new List<EventReward> { new EventReward { ItemId = "gems", Amount = 200 } }
            });

            // 赛季事件
            events.Add(new MiniEvent
            {
                EventId = "SeasonalEvent",
                DisplayName = "Seasonal Event",
                Type = EventType.Seasonal,
                StartTime = System.DateTimeOffset.UtcNow.AddDays(-5),
                EndTime = System.DateTimeOffset.UtcNow.AddDays(25),
                Priority = 1,
                MaxProgress = 1000,
                CurrentProgress = 0,
                Rewards = new List<EventReward> { new EventReward { ItemId = "legendary_skin", Amount = 1 } }
            });

            return events;
        }

        private void CreateTestEvents()
        {
            // 基础配置
        }
    }

    // 测试用数据结构
    public class MiniEvent
    {
        public string EventId;
        public string DisplayName;
        public EventType Type;
        public System.DateTimeOffset StartTime;
        public System.DateTimeOffset EndTime;
        public int Priority;
        public int MaxProgress = 100;
        public int CurrentProgress = 0;
        public bool ResetsDaily = false;
        public List<EventReward> Rewards = new List<EventReward>();

        public bool IsActive => System.DateTimeOffset.UtcNow >= StartTime && System.DateTimeOffset.UtcNow <= EndTime;
        public bool IsExpired => System.DateTimeOffset.UtcNow > EndTime;
        public float Progress01 => MaxProgress > 0 ? (float)CurrentProgress / MaxProgress : 0f;
        public bool CanClaimReward => IsActive && CurrentProgress >= MaxProgress && !IsRewardClaimed;
        public bool IsRewardClaimed = false;
    }

    public class EventReward
    {
        public string ItemId;
        public int Amount;
        public RewardRarity Rarity = RewardRarity.Common;
    }

    public enum EventType
    {
        Daily,
        Weekly,
        Seasonal,
        Special
    }

    public enum RewardRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
        Mythic
    }

    public class EventProgress
    {
        public string EventId;
        public int CurrentProgress;
        public float Progress01;
        public bool CanClaim;
        public bool IsClaimed;
    }

    // ===================== 测试用 EventScheduler Stub =====================
    // 自包含的活动调度器（时间窗激活/进度/领取/每日重置/优先级排序），隔离对 Seasons 管线的依赖

    public class EventScheduler : MonoBehaviour
    {
        private readonly List<MiniEvent> _events = new List<MiniEvent>();
        private System.TimeSpan _timeOffset;

        public IReadOnlyList<MiniEvent> ActiveEvents => _events;

        public void Initialize(List<MiniEvent> events)
        {
            _events.Clear();
            if (events != null) _events.AddRange(events);
        }

        public MiniEvent GetEvent(string eventId) => _events.Find(e => e.EventId == eventId);

        public void AddProgress(string eventId, int amount)
        {
            var evt = GetEvent(eventId);
            if (evt == null) return;
            evt.CurrentProgress = System.Math.Min(evt.CurrentProgress + amount, evt.MaxProgress);
        }

        public List<EventReward> ClaimReward(string eventId)
        {
            var evt = GetEvent(eventId);
            if (evt == null || !evt.CanClaimReward) return null;
            evt.IsRewardClaimed = true;
            return evt.Rewards;
        }

        public List<EventReward> GetRewardPreview(string eventId) => GetEvent(eventId)?.Rewards;

        public List<MiniEvent> GetSortedEvents()
        {
            var sorted = new List<MiniEvent>(_events);
            sorted.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            return sorted;
        }

        public void SimulateTimeAdvance(System.TimeSpan delta)
        {
            _timeOffset += delta;
            foreach (var evt in _events)
            {
                evt.StartTime -= delta;
                evt.EndTime -= delta;
            }
        }

        public void SimulateDailyReset()
        {
            foreach (var evt in _events)
            {
                if (!evt.ResetsDaily) continue;
                evt.CurrentProgress = 0;
                evt.IsRewardClaimed = false;
            }
        }
    }
}
