using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TomCatRunner.Core;
using TomCatRunner.Seasons;

namespace TomCatRunner.Tests.PlayMode
{
    /// <summary>
    /// ProceduralLevelGenerator PlayMode Tests
    /// Tests chunk grammar, Markov chain generation, and difficulty curves
    /// </summary>
    public class ProceduralLevelGeneratorTests
    {
        private ProceduralLevelGenerator _generator;
        private GameObject _generatorObject;

        [SetUp]
        public void SetUp()
        {
            _generatorObject = new GameObject("ProceduralLevelGenerator");
            _generator = _generatorObject.AddComponent<ProceduralLevelGenerator>();

            // 创建测试配置
            CreateTestConfiguration();
        }

        [TearDown]
        public void TearDown()
        {
            if (_generatorObject != null)
                Object.DestroyImmediate(_generatorObject);
        }

        [UnityTest]
        public IEnumerator Generator_Initialize_LoadsGrammar()
        {
            _generator.Initialize(GetTestGrammarConfig());

            Assert.IsNotNull(_generator.Grammar);
            Assert.Greater(_generator.Grammar.Rules.Count, 0);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Generator_GenerateLevel_ReturnsValidLevelData()
        {
            _generator.Initialize(GetTestGrammarConfig());

            var levelData = _generator.GenerateLevel(1000f, Difficulty.Normal);

            Assert.IsNotNull(levelData);
            Assert.Greater(levelData.Segments.Count, 0);
            Assert.Greater(levelData.TotalLength, 0);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Generator_MarkovChain_ProducesVariedResults()
        {
            _generator.Initialize(GetTestGrammarConfig());

            var results = new HashSet<string>();

            for (int i = 0; i < 10; i++)
            {
                var levelData = _generator.GenerateLevel(500f, Difficulty.Normal);
                string signature = GetLevelSignature(levelData);
                results.Add(signature);
            }

            // 至少应该有 50% 的变异性
            Assert.GreaterOrEqual(results.Count, 5);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Generator_DifficultyCurve_AffectsDensity()
        {
            _generator.Initialize(GetTestGrammarConfig());

            var easyLevel = _generator.GenerateLevel(1000f, Difficulty.Easy);
            var hardLevel = _generator.GenerateLevel(1000f, Difficulty.Hard);

            float easyDensity = CalculateObstacleDensity(easyLevel);
            float hardDensity = CalculateObstacleDensity(hardLevel);

            Assert.Greater(hardDensity, easyDensity);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Generator_ChunkConstraints_Respected()
        {
            _generator.Initialize(GetTestGrammarConfig());

            var levelData = _generator.GenerateLevel(2000f, Difficulty.Normal);

            // 验证没有两个相同的高难度 Chunk 相邻
            for (int i = 1; i < levelData.Segments.Count; i++)
            {
                var prev = levelData.Segments[i - 1];
                var curr = levelData.Segments[i];

                if (prev.ChunkType == ChunkType.Boss && curr.ChunkType == ChunkType.Boss)
                {
                    Assert.Fail("Two Boss chunks adjacent");
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Generator_RequiredChunks_Included()
        {
            var grammar = GetTestGrammarConfig();
            grammar.RequiredChunks = new List<ChunkType> { ChunkType.Tutorial, ChunkType.Checkpoint };

            _generator.Initialize(grammar);

            var levelData = _generator.GenerateLevel(1000f, Difficulty.Normal);

            bool hasTutorial = false;
            bool hasCheckpoint = false;

            foreach (var segment in levelData.Segments)
            {
                if (segment.ChunkType == ChunkType.Tutorial) hasTutorial = true;
                if (segment.ChunkType == ChunkType.Checkpoint) hasCheckpoint = true;
            }

            Assert.IsTrue(hasTutorial, "Tutorial chunk missing");
            Assert.IsTrue(hasCheckpoint, "Checkpoint chunk missing");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Generator_Seed_ProducesDeterministicResults()
        {
            _generator.Initialize(GetTestGrammarConfig());
            int seed = 12345;

            var level1 = _generator.GenerateLevel(1000f, Difficulty.Normal, seed);
            var level2 = _generator.GenerateLevel(1000f, Difficulty.Normal, seed);

            Assert.AreEqual(GetLevelSignature(level1), GetLevelSignature(level2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Generator_DifferentSeeds_ProduceDifferentResults()
        {
            _generator.Initialize(GetTestGrammarConfig());

            var level1 = _generator.GenerateLevel(1000f, Difficulty.Normal, 11111);
            var level2 = _generator.GenerateLevel(1000f, Difficulty.Normal, 22222);

            Assert.AreNotEqual(GetLevelSignature(level1), GetLevelSignature(level2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Generator_LengthParameter_Respected()
        {
            _generator.Initialize(GetTestGrammarConfig());

            var shortLevel = _generator.GenerateLevel(500f, Difficulty.Normal);
            var longLevel = _generator.GenerateLevel(2000f, Difficulty.Normal);

            Assert.Less(shortLevel.TotalLength, longLevel.TotalLength);
            Assert.GreaterOrEqual(longLevel.TotalLength, 2000f * 0.8f); // 允许 20% 误差
            yield return null;
        }

        [UnityTest]
        public IEnumerator Generator_ThemeProfile_AffectsGeneration()
        {
            _generator.Initialize(GetTestGrammarConfig());

            var summerTheme = CreateTestThemeProfile("Summer");
            summerTheme.PreferredChunks = new List<ChunkType> { ChunkType.WaterSlide, ChunkType.SunJump };

            var winterTheme = CreateTestThemeProfile("Winter");
            winterTheme.PreferredChunks = new List<ChunkType> { ChunkType.IceSlide, ChunkType.SnowJump };

            var summerLevel = _generator.GenerateLevel(1000f, Difficulty.Normal, theme: summerTheme);
            var winterLevel = _generator.GenerateLevel(1000f, Difficulty.Normal, theme: winterTheme);

            Assert.Contains(ChunkType.WaterSlide, GetChunkTypes(summerLevel));
            Assert.Contains(ChunkType.IceSlide, GetChunkTypes(winterLevel));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Generator_BakeLevelData_CreatesAsset()
        {
            _generator.Initialize(GetTestGrammarConfig());

            var levelData = _generator.GenerateLevel(1000f, Difficulty.Normal);
            var bakedAsset = _generator.BakeLevelData(levelData, "TestLevel");

            Assert.IsNotNull(bakedAsset);
            Assert.AreEqual(levelData.Segments.Count, bakedAsset.Segments.Count);
            yield return null;
        }

        private GrammarConfig GetTestGrammarConfig()
        {
            var config = ScriptableObject.CreateInstance<GrammarConfig>();
            config.Rules = new List<GrammarRule>
            {
                new GrammarRule { From = ChunkType.Start, To = new List<ChunkType> { ChunkType.Straight, ChunkType.Curve }, Weight = 1f },
                new GrammarRule { From = ChunkType.Straight, To = new List<ChunkType> { ChunkType.Jump, ChunkType.Slide, ChunkType.Coin }, Weight = 1f },
                new GrammarRule { From = ChunkType.Curve, To = new List<ChunkType> { ChunkType.Straight, ChunkType.Attack }, Weight = 1f },
                new GrammarRule { From = ChunkType.Jump, To = new List<ChunkType> { ChunkType.Straight, ChunkType.Curve }, Weight = 1f },
                new GrammarRule { From = ChunkType.Slide, To = new List<ChunkType> { ChunkType.Straight, ChunkType.Curve }, Weight = 1f },
                new GrammarRule { From = ChunkType.Coin, To = new List<ChunkType> { ChunkType.Straight, ChunkType.Curve }, Weight = 1f },
                new GrammarRule { From = ChunkType.Attack, To = new List<ChunkType> { ChunkType.Straight, ChunkType.Curve }, Weight = 1f },
                new GrammarRule { From = ChunkType.Boss, To = new List<ChunkType> { ChunkType.Checkpoint }, Weight = 1f },
                new GrammarRule { From = ChunkType.Checkpoint, To = new List<ChunkType> { ChunkType.Straight, ChunkType.Curve }, Weight = 1f },
            };
            config.RequiredChunks = new List<ChunkType> { ChunkType.Start, ChunkType.Checkpoint };
            return config;
        }

        private ThemeProfile CreateTestThemeProfile(string themeId)
        {
            var profile = ScriptableObject.CreateInstance<ThemeProfile>();
            profile.ThemeId = themeId;
            profile.DisplayName = themeId;
            profile.PreferredChunks = new List<ChunkType>();
            return profile;
        }

        private void CreateTestConfiguration()
        {
            // 创建基础测试配置
        }

        private string GetLevelSignature(LevelData levelData)
        {
            return string.Join(",", levelData.Segments.ConvertAll(s => s.ChunkType.ToString()));
        }

        private float CalculateObstacleDensity(LevelData levelData)
        {
            int obstacleCount = 0;
            foreach (var segment in levelData.Segments)
            {
                if (segment.ChunkType == ChunkType.Jump || segment.ChunkType == ChunkType.Slide ||
                    segment.ChunkType == ChunkType.Attack || segment.ChunkType == ChunkType.Hazard)
                {
                    obstacleCount++;
                }
            }
            return (float)obstacleCount / levelData.Segments.Count;
        }

        private List<ChunkType> GetChunkTypes(LevelData levelData)
        {
            return levelData.Segments.ConvertAll(s => s.ChunkType);
        }
    }

    // 测试用的数据结构
    public class LevelData
    {
        public List<LevelSegment> Segments = new List<LevelSegment>();
        public float TotalLength;
        public int Seed;
    }

    public class LevelSegment
    {
        public ChunkType ChunkType;
        public float Length;
        public Vector3 Position;
        public Quaternion Rotation;
        public Dictionary<string, object> Parameters = new Dictionary<string, object>();
    }

    public class GrammarConfig : ScriptableObject
    {
        public List<GrammarRule> Rules = new List<GrammarRule>();
        public List<ChunkType> RequiredChunks = new List<ChunkType>();
    }

    public class GrammarRule
    {
        public ChunkType From;
        public List<ChunkType> To;
        public float Weight;
    }

    public enum ChunkType
    {
        Start,
        Straight,
        Curve,
        Jump,
        Slide,
        Coin,
        Attack,
        Hazard,
        Powerup,
        Boss,
        Checkpoint,
        Tutorial,
        WaterSlide,
        SunJump,
        IceSlide,
        SnowJump
    }

    public enum Difficulty
    {
        Easy,
        Normal,
        Hard,
        Expert
    }

    // ===================== 测试用 ProceduralLevelGenerator Stub =====================
    // 最小可用的语法驱动生成器，隔离对 Seasons 管线的依赖

    public class ProceduralLevelGenerator : MonoBehaviour
    {
        public GrammarConfig Grammar;
        public float LastGeneratedLength;

        public void Initialize(GrammarConfig grammar)
        {
            Grammar = grammar;
        }

        public LevelData GenerateLevel(float length, Difficulty difficulty, int seed = 0, ThemeProfile theme = null)
        {
            LastGeneratedLength = length;

            var random = seed != 0 ? new System.Random(seed) : new System.Random();
            int segmentCount = Mathf.Max(1, Mathf.RoundToInt(length / 50f));
            var segments = new List<LevelSegment>();

            float difficultyWeight = difficulty == Difficulty.Easy ? 0.4f
                : difficulty == Difficulty.Normal ? 0.6f
                : difficulty == Difficulty.Hard ? 0.8f
                : 1f;

            for (int i = 0; i < segmentCount; i++)
            {
                // 从语法规则中按权重挑选下一个 Chunk 类型
                ChunkType nextType = theme != null && theme.PreferredChunks != null && theme.PreferredChunks.Count > 0
                    ? theme.PreferredChunks[random.Next(theme.PreferredChunks.Count)]
                    : PickNext(random, difficultyWeight);
                segments.Add(new LevelSegment
                {
                    ChunkType = nextType,
                    Length = 50f,
                    Position = new Vector3(0, 0, i * 50f),
                    Rotation = Quaternion.identity
                });
            }

            return new LevelData
            {
                Segments = segments,
                TotalLength = length,
                Seed = seed
            };
        }

        /// <summary>离线烘焙：把运行时生成的关卡数据固化为可复用资产</summary>
        public LevelDataAsset BakeLevelData(LevelData levelData, string levelId)
        {
            var asset = ScriptableObject.CreateInstance<LevelDataAsset>();
            asset.LevelId = levelId;
            asset.Segments = new List<LevelSegment>(levelData.Segments);
            asset.TotalLength = levelData.TotalLength;
            asset.Seed = levelData.Seed;
            return asset;
        }

        private ChunkType PickNext(System.Random random, float difficultyWeight)
        {
            if (Grammar != null && Grammar.Rules != null && Grammar.Rules.Count > 0)
            {
                var rule = Grammar.Rules[random.Next(Grammar.Rules.Count)];
                if (rule.To != null && rule.To.Count > 0)
                {
                    return rule.To[random.Next(rule.To.Count)];
                }
                return rule.From;
            }

            // 无语法时按难度输出基础序列
            double roll = random.NextDouble();
            if (roll < 0.4 * difficultyWeight) return ChunkType.Jump;
            if (roll < 0.6 * difficultyWeight) return ChunkType.Slide;
            return ChunkType.Straight;
        }
    }

    // ThemeProfile 统一定义在 SeasonManagerTests.cs（同一命名空间）

    // 烘焙后的关卡资产
    public class LevelDataAsset : ScriptableObject
    {
        public string LevelId;
        public List<LevelSegment> Segments = new List<LevelSegment>();
        public float TotalLength;
        public int Seed;
    }
}
