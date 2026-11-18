using System;
using UnityEngine;

namespace TomCatRunner.Gameplay.Core
{
    /// <summary>
    /// 得分系统 - 距离得分、金币、连击(combo)
    /// </summary>
    public class ScoreSystem
    {
        private const float ScorePerMeter = 2f;
        private const float CoinBaseValue = 25f;
        private const float ComboWindowSeconds = 4f;   // 连击窗口
        private const int MaxCombo = 20;

        public int Score { get; private set; }
        public int Coins { get; private set; }
        public float Distance { get; private set; }
        public int Combo { get; private set; }
        public bool IsComboActive => Combo >= 2 && _comboTimer > 0f;

        public event Action OnScoreChanged;
        public event Action<int> OnComboChanged;       // combo 数（0 = 断连）
        public event Action<int, int> OnCoinCollected; // (coins, gainedScore)

        private float _comboTimer;

        public void Reset()
        {
            Score = 0;
            Coins = 0;
            Distance = 0f;
            Combo = 0;
            _comboTimer = 0f;
            OnScoreChanged?.Invoke();
            OnComboChanged?.Invoke(0);
        }

        /// <summary>按帧推进（由 GameFlowController 驱动）</summary>
        public void Tick(float deltaTime)
        {
            if (_comboTimer > 0f)
            {
                _comboTimer -= deltaTime;
                if (_comboTimer <= 0f)
                {
                    Combo = 0;
                    OnComboChanged?.Invoke(0);
                }
            }
        }

        public void AddDistance(float deltaZ)
        {
            if (deltaZ <= 0f) return;
            Distance += deltaZ;
            int gained = Mathf.RoundToInt(deltaZ * ScorePerMeter);
            Score += gained;
            OnScoreChanged?.Invoke();
        }

        public void AddCoin()
        {
            Coins++;
            Combo = Mathf.Min(Combo + 1, MaxCombo);
            _comboTimer = ComboWindowSeconds;

            // 连击加成：每 5 连击提升 50% 金币价值
            float multiplier = 1f + Mathf.FloorToInt(Combo / 5f) * 0.5f;
            int gained = Mathf.RoundToInt(CoinBaseValue * multiplier);
            Score += gained;

            OnCoinCollected?.Invoke(Coins, gained);
            OnComboChanged?.Invoke(Combo);
            OnScoreChanged?.Invoke();
        }

        public void BreakCombo()
        {
            if (Combo > 0)
            {
                Combo = 0;
                _comboTimer = 0f;
                OnComboChanged?.Invoke(0);
            }
        }
    }
}
