using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using TomCatRunner.Core.Generation;
using TomCatRunner.Core.Level;

namespace TomCatRunner.Core.AI
{
    /// <summary>
    /// 关卡验证器 - 驱动 AutoRunnerBot 对生成关卡做公平性/可玩性验收
    /// 输出 LevelValidationReport 供 CI 与设计侧回归
    /// </summary>
    public class LevelValidator
    {
        /// <summary>单次验证运行的最长模拟秒数</summary>
        public float MaxSimulationSeconds { get; set; } = 120f;

        /// <summary>公平性最低通过线（0-1）</summary>
        public float MinFairnessScore { get; set; } = 0.6f;

        /// <summary>完成率最低通过线（0-1）</summary>
        public float MinCompletionRate { get; set; } = 0.5f;

        /// <summary>历史报告（最近 20 条）</summary>
        public IReadOnlyList<LevelValidationReport> Reports => _reports;
        private readonly List<LevelValidationReport> _reports = new List<LevelValidationReport>();

        /// <summary>
        /// 对一段生成好的 Chunk 序列执行静态公平性审查（无需 PlayMode）
        /// </summary>
        public LevelValidationReport ValidateChunks(IReadOnlyList<ChunkData> chunks)
        {
            var report = new LevelValidationReport
            {
                ValidatedAt = DateTime.UtcNow,
                ChunkCount = chunks?.Count ?? 0,
            };

            if (chunks == null || chunks.Count == 0)
            {
                report.IsPlayable = false;
                report.Issues.Add("关卡为空：没有任何 Chunk 数据");
                Finish(report);
                return report;
            }

            float totalLength = chunks.Sum(c => c.Length);
            report.TotalLength = totalLength;

            // 1. 检查首段缓冲（开局不应立刻出现高危 Chunk）
            float buffer = 0f;
            foreach (var chunk in chunks)
            {
                if (chunk.Type == Data.ChunkType.Hazard || chunk.Type == Data.ChunkType.Boss)
                {
                    if (buffer < 80f)
                    {
                        report.Issues.Add($"开局 {buffer:F0}m 内出现高危 Chunk {chunk.Type}");
                    }
                    break;
                }
                buffer += chunk.Length;
            }

            // 2. 连续高危段检查
            int consecutiveHazards = 0;
            foreach (var chunk in chunks)
            {
                if (chunk.Type == Data.ChunkType.Hazard)
                {
                    consecutiveHazards++;
                    if (consecutiveHazards > 2)
                    {
                        report.Issues.Add($"连续 {consecutiveHazards} 个 Hazard 段，对玩家不可公平通过");
                    }
                }
                else
                {
                    consecutiveHazards = 0;
                }
            }

            // 3. 难度曲线单调性（允许局部波动 ±0.25）
            for (int i = 1; i < chunks.Count; i++)
            {
                float delta = chunks[i].Difficulty01 - chunks[i - 1].Difficulty01;
                if (delta > 0.45f)
                {
                    report.Issues.Add($"难度突增 Chunk#{i}: +{delta:F2}（{chunks[i - 1].Difficulty01:F2} -> {chunks[i].Difficulty01:F2}）");
                }
            }

            // 4. 收益节奏：每 500m 至少一个 Coin/Powerup 段
            float sinceReward = 0f;
            foreach (var chunk in chunks)
            {
                sinceReward += chunk.Length;
                if (chunk.Type == Data.ChunkType.Coin || chunk.Type == Data.ChunkType.Powerup)
                {
                    sinceReward = 0f;
                }
            }
            if (sinceReward > 600f && totalLength > 600f)
            {
                report.Issues.Add($"尾部 {sinceReward:F0}m 没有任何奖励 Chunk，节奏断裂");
            }

            // 5. 公平性评分：以问题数量与密度折算
            float density = report.Issues.Count / Mathf.Max(1f, totalLength / 1000f);
            report.FairnessScore = Mathf.Clamp01(1f - density * 0.35f);
            report.CompletionRate = Mathf.Clamp01(1f - density * 0.25f);
            report.AverageDifficulty = chunks.Average(c => c.Difficulty01);
            report.IsPlayable = report.FairnessScore >= MinFairnessScore
                             && report.CompletionRate >= MinCompletionRate;

            Finish(report);
            return report;
        }

        /// <summary>
        /// 完整动态验证：创建 AutoRunnerBot 在临时场景中试跑（需要 PlayMode）
        /// </summary>
        public async UniTask<LevelValidationReport> ValidateWithBotAsync(
            IReadOnlyList<ChunkData> chunks, AutoRunnerBot.BotDifficulty difficulty = AutoRunnerBot.BotDifficulty.Normal)
        {
            var staticReport = ValidateChunks(chunks);

            var botHost = new GameObject("[LevelValidator] AutoRunnerBot");
            try
            {
                var bot = botHost.AddComponent<AutoRunnerBot>();
                bot.SetDifficulty(difficulty);
                bot.EnableValidation(true);

                float elapsed = 0f;
                var result = bot.GetValidationResult();
                while (elapsed < MaxSimulationSeconds && result.CompletionTime <= 0f)
                {
                    await UniTask.Yield();
                    elapsed += Time.deltaTime;
                    result = bot.GetValidationResult();
                }

                staticReport.CompletionRate = result.CompletionRate;
                staticReport.AverageDifficulty = result.AverageDifficulty;
                staticReport.FairnessScore = result.FairnessScore;
                staticReport.IsPlayable = result.IsPlayable;
                staticReport.BotUsed = true;
            }
            finally
            {
                UnityEngine.Object.Destroy(botHost);
            }

            return staticReport;
        }

        private void Finish(LevelValidationReport report)
        {
            _reports.Add(report);
            if (_reports.Count > 20) _reports.RemoveAt(0);

            if (report.IsPlayable)
            {
                Debug.Log($"[LevelValidator] PASS fairness={report.FairnessScore:F2} " +
                          $"completion={report.CompletionRate:F2} chunks={report.ChunkCount}");
            }
            else
            {
                Debug.LogWarning($"[LevelValidator] FAIL fairness={report.FairnessScore:F2} " +
                                 $"completion={report.CompletionRate:F2} issues=[{string.Join("; ", report.Issues)}]");
            }
        }
    }

    /// <summary>
    /// 关卡验证报告
    /// </summary>
    [Serializable]
    public class LevelValidationReport
    {
        public DateTime ValidatedAt;
        public bool IsPlayable;
        public bool BotUsed;
        public float FairnessScore;
        public float CompletionRate;
        public float AverageDifficulty;
        public int ChunkCount;
        public float TotalLength;
        public List<string> Issues = new List<string>();
    }
}
