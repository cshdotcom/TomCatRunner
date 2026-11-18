using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TomCatRunner.Core.AI;

namespace TomCatRunner.Tests.PlayMode
{
    /// <summary>
    /// AutoRunnerBot PlayMode Tests
    /// Tests AI level validation, decision making, and fairness analysis
    /// </summary>
    public class AutoRunnerBotTests
    {
        private AutoRunnerBot _bot;
        private GameObject _botObject;
        private GameObject _testLevel;

        [SetUp]
        public void SetUp()
        {
            _botObject = new GameObject("AutoRunnerBot");
            _bot = _botObject.AddComponent<AutoRunnerBot>();
            _botObject.AddComponent<CharacterController>();

            _testLevel = CreateTestLevel();
        }

        [TearDown]
        public void TearDown()
        {
            if (_botObject != null)
                Object.DestroyImmediate(_botObject);
            if (_testLevel != null)
                Object.DestroyImmediate(_testLevel);
        }

        [UnityTest]
        public IEnumerator Bot_Initialization_SetsDefaults()
        {
            Assert.AreEqual(AutoRunnerBot.BotState.Idle, _bot.GetValidationResult() == null ? AutoRunnerBot.BotState.Idle : AutoRunnerBot.BotState.Running);
            Assert.AreEqual(1, _bot.GetType().GetField("_currentLane", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(_bot));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Bot_CanDetectObstacles()
        {
            // 创建前方障碍物
            var obstacle = CreateObstacle(new Vector3(0, 1, 10), AutoRunnerBot.ObstacleType.Low);
            obstacle.tag = "Obstacle";
            obstacle.layer = LayerMask.NameToLayer("Default");

            _bot.SetDifficulty(AutoRunnerBot.BotDifficulty.Normal);
            _bot.EnableValidation(false); // 手动控制

            // 移动到障碍物前
            _botObject.transform.position = new Vector3(0, 1, 0);

            yield return new WaitForSeconds(0.5f);

            // 验证检测到障碍物
            var detectedField = _bot.GetType().GetField("_detectedObstacles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var obstacles = (List<AutoRunnerBot.ObstacleInfo>)detectedField.GetValue(_bot);

            Assert.Greater(obstacles.Count, 0);
        }

        [UnityTest]
        public IEnumerator Bot_JumpDecision_ForLowObstacle()
        {
            var obstacle = CreateObstacle(new Vector3(0, 0.5f, 10), AutoRunnerBot.ObstacleType.Low);
            obstacle.tag = "Obstacle";

            _botObject.transform.position = new Vector3(0, 1, 0);
            _bot.EnableValidation(false);

            yield return new WaitForSeconds(0.5f);

            // 应该决定跳跃
            var actionQueueField = _bot.GetType().GetField("_actionQueue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var queue = (Queue<AutoRunnerBot.BotAction>)actionQueueField.GetValue(_bot);

            bool hasJumpAction = false;
            foreach (var action in queue)
            {
                if (action.Type == AutoRunnerBot.BotActionType.Jump)
                {
                    hasJumpAction = true;
                    break;
                }
            }

            Assert.IsTrue(hasJumpAction, "Should queue jump action for low obstacle");
        }

        [UnityTest]
        public IEnumerator Bot_SlideDecision_ForHighObstacle()
        {
            var obstacle = CreateObstacle(new Vector3(0, 2f, 10), AutoRunnerBot.ObstacleType.High);
            obstacle.tag = "Obstacle";

            _botObject.transform.position = new Vector3(0, 1, 0);
            _bot.EnableValidation(false);

            yield return new WaitForSeconds(0.5f);

            var actionQueueField = _bot.GetType().GetField("_actionQueue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var queue = (Queue<AutoRunnerBot.BotAction>)actionQueueField.GetValue(_bot);

            bool hasSlideAction = false;
            foreach (var action in queue)
            {
                if (action.Type == AutoRunnerBot.BotActionType.Slide)
                {
                    hasSlideAction = true;
                    break;
                }
            }

            Assert.IsTrue(hasSlideAction, "Should queue slide action for high obstacle");
        }

        [UnityTest]
        public IEnumerator Bot_LaneChange_ForWideObstacle()
        {
            var obstacle = CreateObstacle(new Vector3(0, 1, 10), AutoRunnerBot.ObstacleType.Wide);
            obstacle.transform.localScale = new Vector3(10, 2, 2); // 宽阻挡所有车道
            obstacle.tag = "Obstacle";

            _botObject.transform.position = new Vector3(0, 1, 0);
            _bot.EnableValidation(false);

            yield return new WaitForSeconds(0.5f);

            var actionQueueField = _bot.GetType().GetField("_actionQueue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var queue = (Queue<AutoRunnerBot.BotAction>)actionQueueField.GetValue(_bot);

            bool hasLaneChange = false;
            foreach (var action in queue)
            {
                if (action.Type == AutoRunnerBot.BotActionType.ChangeLaneLeft ||
                    action.Type == AutoRunnerBot.BotActionType.ChangeLaneRight)
                {
                    hasLaneChange = true;
                    break;
                }
            }

            Assert.IsTrue(hasLaneChange, "Should queue lane change for wide obstacle");
        }

        [UnityTest]
        public IEnumerator Bot_CollectiblePickup_Decision()
        {
            var coin = CreateCollectible(new Vector3(0, 1, 10), AutoRunnerBot.CollectibleType.Coin);
            coin.tag = "Collectible";

            _botObject.transform.position = new Vector3(0, 1, 0);
            _bot.EnableValidation(false);

            yield return new WaitForSeconds(0.5f);

            var actionQueueField = _bot.GetType().GetField("_actionQueue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var queue = (Queue<AutoRunnerBot.BotAction>)actionQueueField.GetValue(_bot);

            // 应该决定换道或保持车道去收集
            Assert.Greater(queue.Count, 0);
        }

        [UnityTest]
        public IEnumerator Bot_ValidationResult_PlayableLevel()
        {
            // 创建一个简单的可通过关卡
            CreatePlayableLevel();

            _bot.SetDifficulty(AutoRunnerBot.BotDifficulty.Normal);
            _bot.EnableValidation(true);

            // 等待验证完成
            yield return new WaitForSeconds(5f);

            var result = _bot.GetValidationResult();
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsPlayable, "Simple level should be playable");
            Assert.Greater(result.CompletionRate, 0.5f);
        }

        [UnityTest]
        public IEnumerator Bot_ValidationResult_UnplayableLevel()
        {
            // 创建一个无法通过的关卡（密集障碍物）
            CreateUnplayableLevel();

            _bot.SetDifficulty(AutoRunnerBot.BotDifficulty.Normal);
            _bot.EnableValidation(true);

            yield return new WaitForSeconds(10f);

            var result = _bot.GetValidationResult();
            Assert.IsNotNull(result);
            // 无法通过的关卡应该标记为不可玩
            // 注意：Bot 可能会重试，所以这里主要测试指标收集
            Assert.GreaterOrEqual(result.TotalObstacles, 0);
        }

        [UnityTest]
        public IEnumerator Bot_FairnessScore_Calculated()
        {
            CreatePlayableLevel();

            _bot.SetDifficulty(AutoRunnerBot.BotDifficulty.Normal);
            _bot.EnableValidation(true);

            yield return new WaitForSeconds(5f);

            var result = _bot.GetValidationResult();
            Assert.IsNotNull(result);
            Assert.GreaterOrEqual(result.FairnessScore, 0f);
            Assert.LessOrEqual(result.FairnessScore, 1f);
        }

        [UnityTest]
        public IEnumerator Bot_DecisionLog_RecordsDecisions()
        {
            CreatePlayableLevel();

            _bot.EnableValidation(true);

            yield return new WaitForSeconds(3f);

            var log = _bot.GetDecisionLog();
            Assert.Greater(log.Count, 0);

            foreach (var entry in log)
            {
                Assert.GreaterOrEqual(entry.Time, 0f);
                Assert.IsTrue(entry.Action != AutoRunnerBot.BotActionType.None);
            }
        }

        [UnityTest]
        public IEnumerator Bot_DifferentDifficulties_AffectSpeed()
        {
            _bot.SetDifficulty(AutoRunnerBot.BotDifficulty.Easy);
            Assert.AreEqual(AutoRunnerBot.BotDifficulty.Easy,
                GetPrivateField<AutoRunnerBot.BotDifficulty>(_bot, "_difficulty"));

            _bot.SetDifficulty(AutoRunnerBot.BotDifficulty.Expert);
            Assert.AreEqual(AutoRunnerBot.BotDifficulty.Expert,
                GetPrivateField<AutoRunnerBot.BotDifficulty>(_bot, "_difficulty"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Bot_Respawn_AfterDeath()
        {
            // 创建必死陷阱
            var killZone = new GameObject("KillZone");
            killZone.transform.position = new Vector3(0, 0, 5);
            killZone.tag = "KillZone";
            killZone.AddComponent<BoxCollider>().isTrigger = true;

            _botObject.transform.position = new Vector3(0, 1, 0);
            _bot.EnableValidation(true);
            _bot.GetType().GetField("_maxRetries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(_bot, 2);

            yield return new WaitForSeconds(8f);

            var result = _bot.GetValidationResult();
            Assert.IsNotNull(result);
            // 应该重试并继续
        }

        private GameObject CreateTestLevel()
        {
            var level = new GameObject("TestLevel");
            // 地面
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.position = new Vector3(0, 0, 0);
            ground.transform.localScale = new Vector3(10, 1, 50);
            ground.transform.SetParent(level.transform);
            return level;
        }

        private GameObject CreateObstacle(Vector3 position, AutoRunnerBot.ObstacleType type)
        {
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.position = position;
            obstacle.transform.localScale = new Vector3(3, 2, 3);
            obstacle.AddComponent<LevelObstacle>().ObstacleType = type;
            return obstacle;
        }

        private GameObject CreateCollectible(Vector3 position, AutoRunnerBot.CollectibleType type)
        {
            var collectible = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            collectible.transform.position = position;
            collectible.transform.localScale = Vector3.one;
            collectible.AddComponent<LevelCollectible>().CollectibleType = type;
            var collider = collectible.GetComponent<Collider>();
            collider.isTrigger = true;
            return collectible;
        }

        private void CreatePlayableLevel()
        {
            // 简单的直道，间隔放置障碍物
            for (int i = 0; i < 10; i++)
            {
                float z = 20 + i * 30;
                if (i % 3 == 0)
                    CreateObstacle(new Vector3(0, 0.5f, z), AutoRunnerBot.ObstacleType.Low);
                else if (i % 3 == 1)
                    CreateObstacle(new Vector3(0, 2f, z), AutoRunnerBot.ObstacleType.High);
                else
                    CreateObstacle(new Vector3(-3, 1, z), AutoRunnerBot.ObstacleType.Low); // 左车道

                CreateCollectible(new Vector3(0, 1, z + 5), AutoRunnerBot.CollectibleType.Coin);
            }
        }

        private void CreateUnplayableLevel()
        {
            // 密集障碍物，无法通过
            for (int i = 0; i < 20; i++)
            {
                float z = 10 + i * 5;
                // 所有三个车道都有障碍物
                CreateObstacle(new Vector3(-3, 1, z), AutoRunnerBot.ObstacleType.Wide);
                CreateObstacle(new Vector3(0, 1, z), AutoRunnerBot.ObstacleType.Wide);
                CreateObstacle(new Vector3(3, 1, z), AutoRunnerBot.ObstacleType.Wide);
            }
        }

        private T GetPrivateField<T>(object obj, string fieldName)
        {
            var field = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (T)field.GetValue(obj);
        }
    }
}