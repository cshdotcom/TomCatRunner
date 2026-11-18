using System;
using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using TomCatRunner.Core.Level;
using TomCatRunner.Core.Season;
using TomCatRunner.Gameplay.Core;
using TomCatRunner.Gameplay.UI;
using TomCatRunner.Gameplay.World;

namespace TomCatRunner.Gameplay.Core
{
    /// <summary>游戏状态</summary>
    public enum GameState
    {
        Boot,
        Menu,
        Countdown,
        Playing,
        Paused,
        Dying,
        GameOver,
    }

    /// <summary>
    /// 游戏流程控制器 - 状态机 + 难度曲线 + 碰撞/得分/道具反馈编排
    /// 由 GameplayDriver 每帧驱动
    /// </summary>
    public class GameFlowController
    {
        public GameState State { get; private set; } = GameState.Boot;

        // 依赖
        private readonly InputService _input;
        private readonly ScoreSystem _score;
        private readonly SaveService _save;
        private readonly ProceduralAudioService _audio;
        private readonly CatController _cat;
        private readonly CameraRig _cameraRig;
        private readonly LevelStreamer _streamer;
        private readonly SeasonManager _seasons;
        private readonly ThemeRuntime _theme;
        private readonly MenuScreen _menu;
        private readonly HudScreen _hud;
        private readonly PauseScreen _pauseScreen;
        private readonly GameOverScreen _gameOverScreen;
        private readonly CountdownScreen _countdown;
        private readonly ToastService _toast;

        // 难度参数
        private const float BaseSpeed = 8.5f;
        private const float MaxSpeed = 17f;
        private const float RampDistance = 2200f;    // 距离越远越快
        private const float MagnetDuration = 9f;
        private const float ShieldDuration = 20f;
        private const float BoostDuration = 3.2f;

        // 内部
        private float _countdownTimer;
        private int _countdownDigit;
        private float _dyingTimer;
        private float _hudRefreshTimer;
        private float _menuRefreshTimer;
        private bool _tutorialShownThisSession;

        public GameFlowController(
            InputService input, ScoreSystem score, SaveService save, ProceduralAudioService audio,
            CatController cat, CameraRig cameraRig, LevelStreamer streamer, SeasonManager seasons,
            ThemeRuntime theme, MenuScreen menu, HudScreen hud, PauseScreen pauseScreen,
            GameOverScreen gameOverScreen, CountdownScreen countdown, ToastService toast)
        {
            _input = input;
            _score = score;
            _save = save;
            _audio = audio;
            _cat = cat;
            _cameraRig = cameraRig;
            _streamer = streamer;
            _seasons = seasons;
            _theme = theme;
            _menu = menu;
            _hud = hud;
            _pauseScreen = pauseScreen;
            _gameOverScreen = gameOverScreen;
            _countdown = countdown;
            _toast = toast;

            // 输入 → 猫
            _input.MoveLeftRequested += _cat.MoveLeft;
            _input.MoveRightRequested += _cat.MoveRight;
            _input.JumpRequested += _cat.Jump;
            _input.SlideRequested += _cat.Slide;
            _input.PauseRequested += HandlePausePressed;

            // 猫 → 反馈
            _cat.OnCoinTriggered += HandleCoin;
            _cat.OnPowerupTriggered += HandlePowerup;
            _cat.OnObstacleTriggered += HandleObstacleHit;

            // 得分事件
            _score.OnComboChanged += combo =>
            {
                if (combo >= 2) _hud.PlayCombo(combo);
                else _hud.BreakCombo();
                if (combo is 5 or 10 or 15 or 20)
                    _toast.Show($"连击 ×{combo}！金币加成 {(1 + combo / 10 * 0.5f):F0} 倍", new Color(1f, 0.86f, 0.35f));
            };
            _score.OnCoinCollected += (_, _) => _hud.PunchCoin();
        }

        public void Begin()
        {
            State = GameState.Menu;
            _cameraRig.MenuOrbit = true;
            _cameraRig.SetTarget(_cat.transform);
            _streamer.SetPlayer(_cat.transform);
            _streamer.Refresh();
            _menu.Show();
            _audio.PlayMusic();
            _toast.Show("欢迎回来！按 ← → 跳跃滑铲，冲鸭！", new Color(1f, 0.9f, 0.7f));
        }

        // =====================================================================
        // 帧驱动
        // =====================================================================

        public void Tick(float deltaTime)
        {
            _input.Tick();

            // 手动驱动流式与赛季（保险：即使 VContainer PlayerLoop 未注入也能运转）
            try { _streamer.Tick(); } catch (Exception) { /* PlayerLoop 双驱动场景下安全 */ }
            if (_seasons != null)
            {
                try { _seasons.Tick(); } catch (Exception) { }
            }

            switch (State)
            {
                case GameState.Menu:
                case GameState.Countdown:
                    // 菜单/倒计时阶段周期性刷新流式加载，确保跑道背景就绪
                    _menuRefreshTimer -= deltaTime;
                    if (_menuRefreshTimer <= 0f)
                    {
                        _menuRefreshTimer = 0.6f;
                        try { _streamer.Refresh(); } catch (Exception) { }
                    }
                    if (State == GameState.Countdown) TickCountdown(deltaTime);
                    break;

                case GameState.Playing:
                    TickPlaying(deltaTime);
                    break;

                case GameState.Dying:
                    _dyingTimer -= Time.unscaledDeltaTime;
                    if (_dyingTimer <= 0f) FinishDeath();
                    break;
            }
        }

        private void TickCountdown(float dt)
        {
            _countdownTimer -= Time.unscaledDeltaTime;
            int digit = Mathf.CeilToInt(_countdownTimer);

            if (digit != _countdownDigit)
            {
                _countdownDigit = digit;
                if (digit > 0)
                {
                    _countdown.ShowDigit(digit.ToString(), Color.white);
                    _audio.PlayCountdown();
                }
                else
                {
                    _countdown.ShowDigit("出发！", new Color(1f, 0.6f, 0.2f));
                    _audio.PlayGo();
                    StartPlaying();
                }
            }
        }

        private void TickPlaying(float dt)
        {
            // 难度曲线：速度随距离提升
            float ramp = Mathf.Clamp01(_score.Distance / RampDistance);
            float seasonSpeed = 1f;
            var season = _seasons?.CurrentSeason;
            if (season?.DifficultyCurve?.SpeedMultiplier != null)
            {
                // 赛季曲线覆盖（归一化距离）
                seasonSpeed = season.DifficultyCurve.SpeedMultiplier.Evaluate(
                    Mathf.Clamp01(_score.Distance / Mathf.Max(1f, season.DifficultyCurve.MaxDistance)));
            }
            _cat.TargetSpeed = Mathf.Min(BaseSpeed + (MaxSpeed - BaseSpeed) * ramp, BaseSpeed * seasonSpeed);

            // 得分
            _score.AddDistance(_cat.DeltaZ);
            _score.Tick(dt);

            // HUD 节流刷新（10Hz）
            _hudRefreshTimer -= dt;
            if (_hudRefreshTimer <= 0f)
            {
                _hudRefreshTimer = 0.1f;
                float speed01 = Mathf.Clamp01((_cat.CurrentSpeed - BaseSpeed) / (MaxSpeed - BaseSpeed));
                _hud.Refresh(_score, _cat, speed01);
            }
        }

        // =====================================================================
        // 状态切换
        // =====================================================================

        public void StartRun()
        {
            if (State != GameState.Menu) return;
            _audio.PlayClick();
            _menu.Hide();

            // 重置
            _score.Reset();
            _cat.ResetState();
            _streamer.SetPlayer(_cat.transform);
            _streamer.Refresh();

            _cameraRig.MenuOrbit = false;
            _cameraRig.DeathView = false;
            _hud.Show();

            State = GameState.Countdown;
            _countdownTimer = 3.0f;
            _countdownDigit = int.MaxValue;

            if (!_save.TutorialShown && !_tutorialShownThisSession)
            {
                _tutorialShownThisSession = true;
                _save.MarkTutorialShown();
                _toast.Show("← → 切道 · ↑/空格 跳 · ↓ 滑铲", Color.white);
            }
        }

        private void StartPlaying()
        {
            State = GameState.Playing;
            _cat.SetRunning(true);
            _cat.TargetSpeed = BaseSpeed;

            // 0.9 秒后隐藏倒计时
            DelayHideCountdown().Forget();
        }

        private async UniTaskVoid DelayHideCountdown()
        {
            await UniTask.Delay(900, ignoreTimeScale: true);
            _countdown.Hide();
        }

        private void HandlePausePressed()
        {
            if (State == GameState.Playing) Pause();
            else if (State == GameState.Paused) Resume();
        }

        public void Pause()
        {
            if (State != GameState.Playing) return;
            State = GameState.Paused;
            Time.timeScale = 0f;
            _cat.SetRunning(false);
            _pauseScreen.Show();
            _audio.PlayClick();
        }

        public void Resume()
        {
            if (State != GameState.Paused) return;
            _pauseScreen.Hide();
            Time.timeScale = 1f;
            _cat.SetRunning(true);
            State = GameState.Playing;
            _audio.PlayClick();
        }

        public void RestartFromPause()
        {
            _pauseScreen.Hide();
            Time.timeScale = 1f;
            RestartRun();
        }

        public void GoToMenu()
        {
            _pauseScreen.Hide();
            _gameOverScreen.Hide();
            _hud.Hide();
            _countdown.Hide();
            Time.timeScale = 1f;
            DOTween.KillAll();

            _cat.ResetState();
            _streamer.SetPlayer(_cat.transform);
            _streamer.Refresh();
            _cameraRig.MenuOrbit = true;
            _cameraRig.DeathView = false;

            _menu.Refresh(_save, _theme);
            _menu.Show();
            _audio.PlayMusic();
            State = GameState.Menu;
        }

        /// <summary>从结算面板重开</summary>
        public void RestartRun()
        {
            _gameOverScreen.Hide();
            DOTween.KillAll();

            _score.Reset();
            _cat.ResetState();
            _streamer.SetPlayer(_cat.transform);
            _streamer.Refresh();
            _cameraRig.DeathView = false;
            _hud.Show();

            State = GameState.Countdown;
            _countdownTimer = 3.0f;
            _countdownDigit = int.MaxValue;
            _audio.PlayClick();
        }

        // =====================================================================
        // 碰撞 / 道具反馈
        // =====================================================================

        private void HandleCoin(CoinBehaviour coin)
        {
            coin.Collect();
            _score.AddCoin();
            _audio.PlayCoin();
        }

        private void HandlePowerup(PowerupBehaviour powerup)
        {
            powerup.Collect();
            _audio.PlayPowerup();

            switch (powerup.Kind)
            {
                case PowerupKind.Magnet:
                    _cat.ActivateMagnet(MagnetDuration);
                    _toast.Show("磁铁登场！金币自动飞来", new Color(0.45f, 0.7f, 1f));
                    break;
                case PowerupKind.Shield:
                    _cat.ActivateShield(ShieldDuration);
                    _cat.Visuals?.SetEmission(0.5f, new Color(1f, 0.9f, 0.4f));
                    _toast.Show("护盾展开！可抵挡一次碰撞", new Color(1f, 0.88f, 0.4f));
                    break;
                case PowerupKind.Boost:
                    _cat.ActivateBoost(BoostDuration);
                    _cat.Visuals?.SetEmission(0.7f, new Color(1f, 0.4f, 0.3f));
                    _cameraRig.Shake(0.35f);
                    _toast.Show("极速冲刺！撞碎一切！", new Color(1f, 0.55f, 0.4f));
                    _audio.PlayBoost();
                    break;
            }
        }

        private void HandleObstacleHit(ObstacleMarker obstacle)
        {
            // 冲刺状态撞碎障碍（CatController 已过滤，理论上不会到这）
            if (_cat.BoostActive) return;

            // 护盾抵挡
            if (_cat.ShieldActive)
            {
                _cat.TryConsumeShield();
                _cat.Visuals?.SetEmission(0f);
                _score.BreakCombo();
                _audio.PlayShieldBreak();
                _cameraRig.Shake(0.5f);
                _toast.Show("护盾碎裂！小心！", new Color(1f, 0.7f, 0.3f));
                return;
            }

            // 正式死亡
            _audio.PlayHit();
            _score.BreakCombo();
            _cameraRig.Shake(0.9f);
            _cat.Die();
            _hud.Hide();
            _cat.Visuals?.SetEmission(0.9f, new Color(1f, 0.25f, 0.2f));

            State = GameState.Dying;
            _dyingTimer = 1.25f;
            Time.timeScale = 0.28f;
            _cameraRig.DeathView = true;
        }

        private void FinishDeath()
        {
            Time.timeScale = 1f;
            State = GameState.GameOver;

            bool newBest = _save.SubmitRun(_score.Score, _score.Distance, _score.Coins);
            _gameOverScreen.Show(_score, _save, newBest);
            if (newBest)
                _toast.Show("打破纪录！干得漂亮，猫！", new Color(1f, 0.5f, 0.2f));
        }
    }
}
