using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using TomCatRunner.Seasons;
using TomCatRunner.Core;

namespace TomCatRunner.Tests.EditMode
{
    /// <summary>
    /// EditMode Tests for configuration validation
    /// Validates ScriptableObject configurations without entering PlayMode
    /// </summary>
    public class ConfigurationValidationTests
    {
        [Test]
        public void SeasonConfig_Validation_PassesForValidConfig()
        {
            var config = CreateValidSeasonConfig();
            var errors = config.Validate();

            Assert.AreEqual(0, errors.Count, $"Validation failed: {string.Join(", ", errors)}");
        }

        [Test]
        public void SeasonConfig_Validation_FailsForMissingThemeProfile()
        {
            var config = CreateValidSeasonConfig();
            config.ThemeProfile = null;

            var errors = config.Validate();

            Assert.Greater(errors.Count, 0);
            Assert.IsTrue(errors.Exists(e => e.Contains("ThemeProfile")));
        }

        [Test]
        public void SeasonConfig_Validation_FailsForInvalidTimeRange()
        {
            var config = CreateValidSeasonConfig();
            config.StartTime = System.DateTimeOffset.UtcNow.AddDays(1);
            config.EndTime = System.DateTimeOffset.UtcNow; // 结束在开始之前

            var errors = config.Validate();

            Assert.Greater(errors.Count, 0);
            Assert.IsTrue(errors.Exists(e => e.Contains("time") || e.Contains("Time")));
        }

        [Test]
        public void SeasonConfig_Validation_FailsForEmptySeasonId()
        {
            var config = CreateValidSeasonConfig();
            config.SeasonId = "";

            var errors = config.Validate();

            Assert.Greater(errors.Count, 0);
            Assert.IsTrue(errors.Exists(e => e.Contains("SeasonId") || e.Contains("Id")));
        }

        [Test]
        public void ThemeProfile_Validation_PassesForValidProfile()
        {
            var profile = CreateValidThemeProfile();
            var errors = profile.Validate();

            Assert.AreEqual(0, errors.Count, $"Validation failed: {string.Join(", ", errors)}");
        }

        [Test]
        public void ThemeProfile_Validation_FailsForMissingColorPalette()
        {
            var profile = CreateValidThemeProfile();
            profile.ColorPalette = null;

            var errors = profile.Validate();

            Assert.Greater(errors.Count, 0);
            Assert.IsTrue(errors.Exists(e => e.Contains("ColorPalette") || e.Contains("Palette")));
        }

        [Test]
        public void ThemeProfile_Validation_FailsForInvalidGradient()
        {
            var profile = CreateValidThemeProfile();
            profile.ColorPalette.PrimaryGradient = new Gradient(); // 空渐变

            var errors = profile.Validate();

            Assert.Greater(errors.Count, 0);
        }

        [Test]
        public void BattlePassTable_Validation_PassesForValidTable()
        {
            var table = CreateValidBattlePassTable();
            var errors = table.Validate();

            Assert.AreEqual(0, errors.Count, $"Validation failed: {string.Join(", ", errors)}");
        }

        [Test]
        public void BattlePassTable_Validation_FailsForMismatchedRewardCounts()
        {
            var table = CreateValidBattlePassTable();
            table.FreeRewards.RemoveAt(0); // 免费奖励少一个

            var errors = table.Validate();

            Assert.Greater(errors.Count, 0);
            Assert.IsTrue(errors.Exists(e => e.Contains("reward") || e.Contains("Reward") || e.Contains("count")));
        }

        [Test]
        public void BattlePassTable_Validation_FailsForInvalidXPCurve()
        {
            var table = CreateValidBattlePassTable();
            table.XPGrowthFactor = 0f; // 无效增长因子

            var errors = table.Validate();

            Assert.Greater(errors.Count, 0);
        }

        [Test]
        public void GrammarConfig_Validation_PassesForValidGrammar()
        {
            var grammar = CreateValidGrammarConfig();
            var errors = grammar.Validate();

            Assert.AreEqual(0, errors.Count, $"Validation failed: {string.Join(", ", errors)}");
        }

        [Test]
        public void GrammarConfig_Validation_FailsForUnreachableChunks()
        {
            var grammar = CreateValidGrammarConfig();
            // 添加一个无法到达的 Chunk 类型
            grammar.Rules.Add(new GrammarRule
            {
                From = (ChunkType)999, // 不存在的类型
                To = new List<ChunkType> { ChunkType.Straight },
                Weight = 1f
            });

            var errors = grammar.Validate();

            Assert.Greater(errors.Count, 0);
        }

        [Test]
        public void GrammarConfig_Validation_FailsForMissingStartRule()
        {
            var grammar = CreateValidGrammarConfig();
            grammar.Rules.RemoveAt(0); // 移除起始规则

            var errors = grammar.Validate();

            Assert.Greater(errors.Count, 0);
            Assert.IsTrue(errors.Exists(e => e.Contains("Start") || e.Contains("start")));
        }

        [Test]
        public void DifficultyCurve_Validation_PassesForValidCurve()
        {
            var curve = CreateValidDifficultyCurve();
            var errors = curve.Validate();

            Assert.AreEqual(0, errors.Count, $"Validation failed: {string.Join(", ", errors)}");
        }

        [Test]
        public void DifficultyCurve_Validation_FailsForNonMonotonicCurve()
        {
            var curve = CreateValidDifficultyCurve();
            // 创建非单调曲线
            curve.ObstacleDensityCurve = AnimationCurve.Linear(0, 1, 1, 0);

            var errors = curve.Validate();

            Assert.Greater(errors.Count, 0);
        }

        [Test]
        public void AllScriptableObjects_HaveUniqueIds()
        {
            var configs = new List<ScriptableObject>
            {
                CreateValidSeasonConfig(),
                CreateValidThemeProfile(),
                CreateValidBattlePassTable(),
                CreateValidGrammarConfig(),
                CreateValidDifficultyCurve()
            };

            var ids = new HashSet<string>();
            foreach (var config in configs)
            {
                var idProp = config.GetType().GetProperty("Id") ?? config.GetType().GetProperty("SeasonId") ?? config.GetType().GetProperty("ThemeId");
                if (idProp != null)
                {
                    string id = (string)idProp.GetValue(config);
                    Assert.IsFalse(ids.Contains(id), $"Duplicate ID: {id}");
                    ids.Add(id);
                }
            }
        }

        [Test]
        public void AllScriptableObjects_HaveValidReferences()
        {
            var seasonConfig = CreateValidSeasonConfig();
            Assert.IsNotNull(seasonConfig.ThemeProfile);
            Assert.IsNotNull(seasonConfig.BattlePassTable);
            Assert.IsNotNull(seasonConfig.GrammarConfig);
            Assert.IsNotNull(seasonConfig.DifficultyCurve);
        }

        private SeasonConfig CreateValidSeasonConfig()
        {
            var config = ScriptableObject.CreateInstance<SeasonConfig>();
            config.SeasonId = "TestSeason";
            config.DisplayName = "Test Season";
            config.StartTime = System.DateTimeOffset.UtcNow.AddDays(-1);
            config.EndTime = System.DateTimeOffset.UtcNow.AddDays(30);
            config.ThemeProfile = CreateValidThemeProfile();
            config.BattlePassTable = CreateValidBattlePassTable();
            config.GrammarConfig = CreateValidGrammarConfig();
            config.DifficultyCurve = CreateValidDifficultyCurve();
            return config;
        }

        private ThemeProfile CreateValidThemeProfile()
        {
            var profile = ScriptableObject.CreateInstance<ThemeProfile>();
            profile.ThemeId = "TestTheme";
            profile.DisplayName = "Test Theme";
            profile.ColorPalette = CreateValidColorPalette();
            return profile;
        }

        private ThemeColorPalette CreateValidColorPalette()
        {
            var palette = ScriptableObject.CreateInstance<ThemeColorPalette>();
            palette.PaletteId = "TestPalette";
            palette.PrimaryGradient = CreateTestGradient(Color.cyan, Color.blue);
            palette.SecondaryGradient = CreateTestGradient(Color.magenta, Color.red);
            palette.AccentColor = Color.yellow;
            palette.BackgroundColor = new Color(0.1f, 0.1f, 0.2f);
            palette.TextColor = Color.white;
            palette.DisabledColor = Color.gray;
            return palette;
        }

        private Gradient CreateTestGradient(Color start, Color end)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            return gradient;
        }

        private BattlePassTable CreateValidBattlePassTable()
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
                table.FreeRewards.Add(new BattlePassReward { Level = i, RewardType = RewardType.Coins, Value = i * 10 });
                table.PremiumRewards.Add(new BattlePassReward { Level = i, RewardType = RewardType.Gems, Value = i * 20 });
            }

            return table;
        }

        private GrammarConfig CreateValidGrammarConfig()
        {
            var config = ScriptableObject.CreateInstance<GrammarConfig>();
            config.Rules = new List<GrammarRule>
            {
                new GrammarRule { From = ChunkType.Start, To = new List<ChunkType> { ChunkType.Straight, ChunkType.Curve }, Weight = 1f },
                new GrammarRule { From = ChunkType.Straight, To = new List<ChunkType> { ChunkType.Jump, ChunkType.Slide, ChunkType.Coin }, Weight = 1f },
                new GrammarRule { From = ChunkType.Curve, To = new List<ChunkType> { ChunkType.Straight, ChunkType.Attack }, Weight = 1f },
                new GrammarRule { From = ChunkType.Jump, To = new List<ChunkType> { ChunkType.Straight, ChunkType.Curve }, Weight = 1f },
                new GrammarRule { From = ChunkType.Slide, To = new List<ChunkType> { ChunkType.Straight, ChunkType.Curve }, Weight = 1f },
            };
            config.RequiredChunks = new List<ChunkType> { ChunkType.Start };
            return config;
        }

        private DifficultyCurve CreateValidDifficultyCurve()
        {
            var curve = ScriptableObject.CreateInstance<DifficultyCurve>();
            curve.ObstacleDensityCurve = AnimationCurve.Linear(0, 0.1f, 1, 0.8f);
            curve.EnemyDensityCurve = AnimationCurve.Linear(0, 0.05f, 1, 0.5f);
            curve.PowerupFrequencyCurve = AnimationCurve.Linear(0, 0.2f, 1, 0.1f);
            curve.SpeedMultiplierCurve = AnimationCurve.Linear(0, 1f, 1, 2f);
            return curve;
        }
    }

    // 测试用数据结构（需要与运行时代码匹配）
    public class SeasonConfig : ScriptableObject
    {
        public string SeasonId;
        public string DisplayName;
        public System.DateTimeOffset StartTime;
        public System.DateTimeOffset EndTime;
        public ThemeProfile ThemeProfile;
        public BattlePassTable BattlePassTable;
        public GrammarConfig GrammarConfig;
        public DifficultyCurve DifficultyCurve;

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(SeasonId)) errors.Add("SeasonId is empty");
            if (ThemeProfile == null) errors.Add("ThemeProfile is missing");
            if (StartTime >= EndTime) errors.Add("StartTime must be before EndTime");
            if (BattlePassTable == null) errors.Add("BattlePassTable is missing");
            if (GrammarConfig == null) errors.Add("GrammarConfig is missing");
            if (DifficultyCurve == null) errors.Add("DifficultyCurve is missing");
            return errors;
        }
    }

    public class ThemeProfile : ScriptableObject
    {
        public string ThemeId;
        public string DisplayName;
        public ThemeColorPalette ColorPalette;
        public List<ChunkType> PreferredChunks;

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(ThemeId)) errors.Add("ThemeId is empty");
            if (ColorPalette == null) errors.Add("ColorPalette is missing");
            else
            {
                var paletteErrors = ColorPalette.Validate();
                errors.AddRange(paletteErrors);
            }
            return errors;
        }
    }

    public class ThemeColorPalette : ScriptableObject
    {
        public string PaletteId;
        public Gradient PrimaryGradient;
        public Gradient SecondaryGradient;
        public Color AccentColor;
        public Color BackgroundColor;
        public Color TextColor;
        public Color DisabledColor;

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (PrimaryGradient == null || PrimaryGradient.colorKeys.Length == 0) errors.Add("PrimaryGradient is empty");
            if (SecondaryGradient == null || SecondaryGradient.colorKeys.Length == 0) errors.Add("SecondaryGradient is empty");
            return errors;
        }
    }

    public class BattlePassTable : ScriptableObject
    {
        public string SeasonId;
        public int MaxLevel;
        public int BaseXPPerLevel;
        public float XPGrowthFactor;
        public System.DateTimeOffset SeasonEndTime;
        public List<BattlePassReward> FreeRewards;
        public List<BattlePassReward> PremiumRewards;

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (MaxLevel <= 0) errors.Add("MaxLevel must be positive");
            if (BaseXPPerLevel <= 0) errors.Add("BaseXPPerLevel must be positive");
            if (XPGrowthFactor <= 0) errors.Add("XPGrowthFactor must be positive");
            if (FreeRewards == null || FreeRewards.Count != MaxLevel) errors.Add($"FreeRewards count ({FreeRewards?.Count ?? 0}) must match MaxLevel ({MaxLevel})");
            if (PremiumRewards == null || PremiumRewards.Count != MaxLevel) errors.Add($"PremiumRewards count ({PremiumRewards?.Count ?? 0}) must match MaxLevel ({MaxLevel})");
            return errors;
        }
    }

    public class BattlePassReward
    {
        public int Level;
        public RewardType RewardType;
        public int Value;
        public string ItemId;
    }

    public enum RewardType { Coins, Gems, CharacterSkin, PowerUp, Emote, Title }

    public class GrammarConfig : ScriptableObject
    {
        public List<GrammarRule> Rules;
        public List<ChunkType> RequiredChunks;

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (Rules == null || Rules.Count == 0) errors.Add("No grammar rules defined");
            if (!Rules.Exists(r => r.From == ChunkType.Start)) errors.Add("Missing Start rule");
            return errors;
        }
    }

    public class GrammarRule
    {
        public ChunkType From;
        public List<ChunkType> To;
        public float Weight;
    }

    public class DifficultyCurve : ScriptableObject
    {
        public AnimationCurve ObstacleDensityCurve;
        public AnimationCurve EnemyDensityCurve;
        public AnimationCurve PowerupFrequencyCurve;
        public AnimationCurve SpeedMultiplierCurve;

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (ObstacleDensityCurve == null || ObstacleDensityCurve.length == 0) errors.Add("ObstacleDensityCurve is empty");
            if (!IsMonotonic(ObstacleDensityCurve)) errors.Add("ObstacleDensityCurve must be monotonic");
            return errors;
        }

        private bool IsMonotonic(AnimationCurve curve)
        {
            if (curve.length < 2) return true;
            float prev = curve.Evaluate(0);
            for (float t = 0.01f; t <= 1f; t += 0.01f)
            {
                float curr = curve.Evaluate(t);
                if (curr < prev) return false;
                prev = curr;
            }
            return true;
        }
    }

    public enum ChunkType
    {
        Start, Straight, Curve, Jump, Slide, Coin, Attack, Hazard, Powerup, Boss, Checkpoint, Tutorial
    }
}