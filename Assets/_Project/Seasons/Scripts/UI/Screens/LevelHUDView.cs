using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TomCatRunner.Seasons;
using TomCatRunner.Common;

namespace TomCatRunner.UI
{
    /// <summary>
    /// 关卡跑酷 HUD - 距离、金币、倍率、道具、暂停
    /// </summary>
    public class LevelHUDView : ViewBase<LevelHUDViewModel>
    {
        [Header("距离")]
        [SerializeField] private TMP_Text _distanceText;
        [SerializeField] private TMP_Text _bestDistanceText;
        [SerializeField] private Image _distanceIcon;

        [Header("金币")]
        [SerializeField] private TMP_Text _coinText;
        [SerializeField] private Image _coinIcon;
        [SerializeField] private ParticleSystem _coinCollectEffect;

        [Header("倍率")]
        [SerializeField] private TMP_Text _multiplierText;
        [SerializeField] private Image _multiplierGlow;
        [SerializeField] private Slider _multiplierBar;

        [Header("道具栏")]
        [SerializeField] private Transform _powerupSlotsContainer;
        [SerializeField] private GameObject _powerupSlotPrefab;
        [SerializeField] private int _maxPowerupSlots = 3;

        [Header("车道指示器")]
        [SerializeField] private Transform _laneIndicatorContainer;
        [SerializeField] private GameObject _laneIndicatorPrefab;
        [SerializeField] private int _laneCount = 3;

        [Header("暂停/菜单")]
        [SerializeField] private Button _pauseButton;
        [SerializeField] private GameObject _pausePanel;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _quitButton;

        [Header("检查点")]
        [SerializeField] private GameObject _checkpointToastPrefab;
        [SerializeField] private Transform _toastContainer;

        [Header("Boss 血条")]
        [SerializeField] private GameObject _bossHealthPanel;
        [SerializeField] private Slider _bossHealthBar;
        [SerializeField] private TMP_Text _bossNameText;
        [SerializeField] private Image _bossPortrait;

        private LevelHUDViewModel _viewModel;
        private readonly List<PowerupSlot> _powerupSlots = new List<PowerupSlot>();
        private readonly List<Image> _laneIndicators = new List<Image>();
        private Sequence _coinPulseSequence;

        protected override void Awake()
        {
            base.Awake();

            // 初始化道具槽
            for (int i = 0; i < _maxPowerupSlots; i++)
            {
                var slot = Instantiate(_powerupSlotPrefab, _powerupSlotsContainer).GetComponent<PowerupSlot>();
                slot.Initialize(i);
                slot.gameObject.SetActive(false);
                _powerupSlots.Add(slot);
            }

            // 初始化车道指示器
            for (int i = 0; i < _laneCount; i++)
            {
                var indicator = Instantiate(_laneIndicatorPrefab, _laneIndicatorContainer).GetComponent<Image>();
                _laneIndicators.Add(indicator);
            }
            UpdateLaneIndicator(1); // 默认中间车道

            _pausePanel.SetActive(false);
            _bossHealthPanel.SetActive(false);
        }

        protected override void OnBind()
        {
            _viewModel = ViewModel;

            _pauseButton.onClick.AddListener(() => _viewModel.PauseCommand.Execute());
            _resumeButton.onClick.AddListener(() => _viewModel.ResumeCommand.Execute());
            _restartButton.onClick.AddListener(() => _viewModel.RestartCommand.Execute());
            _quitButton.onClick.AddListener(() => _viewModel.QuitCommand.Execute());

            BindDisposables.Add(_viewModel.Distance.Subscribe(OnDistanceChanged));
            BindDisposables.Add(_viewModel.Coins.Subscribe(OnCoinsChanged));
            BindDisposables.Add(_viewModel.Multiplier.Subscribe(OnMultiplierChanged));
            BindDisposables.Add(_viewModel.ActivePowerups.Subscribe(OnPowerupsChanged));
            BindDisposables.Add(_viewModel.CurrentLane.Subscribe(OnLaneChanged));
            BindDisposables.Add(_viewModel.IsPaused.Subscribe(OnPauseChanged));
            BindDisposables.Add(_viewModel.BossHealth.Subscribe(OnBossHealthChanged));
            BindDisposables.Add(_viewModel.CheckpointReached.Subscribe(OnCheckpointReached));
        }

        private void OnDistanceChanged(float distance)
        {
            _distanceText.text = FormatDistance(distance);
        }

        private void OnCoinsChanged(int coins)
        {
            _coinText.text = coins.ToString("N0");

            // 金币脉冲动画
            _coinPulseSequence?.Kill();
            _coinPulseSequence = DOTween.Sequence();
            _coinPulseSequence.Append(_coinIcon.transform.DOScale(1.3f, 0.1f).SetEase(Ease.OutQuad));
            _coinPulseSequence.Append(_coinIcon.transform.DOScale(1f, 0.2f).SetEase(Ease.OutBack));

            if (_coinCollectEffect != null)
            {
                _coinCollectEffect.Play();
            }
        }

        private void OnMultiplierChanged(float multiplier)
        {
            _multiplierText.text = $"x{multiplier:F1}";
            _multiplierBar.DOValue(Mathf.InverseLerp(1f, 5f, multiplier), 0.3f).SetEase(Ease.OutCubic);

            // 倍率变色
            Color color = multiplier switch
            {
                >= 4f => new Color(1f, 0f, 1f),      // 紫
                >= 3f => new Color(1f, 0.5f, 0f),    // 橙
                >= 2f => new Color(1f, 1f, 0f),      // 金
                _ => new Color(1f, 1f, 1f)           // 白
            };
            _multiplierText.color = color;
            _multiplierGlow.color = color;
        }

        private void OnPowerupsChanged(List<ActivePowerup> powerups)
        {
            for (int i = 0; i < _powerupSlots.Count; i++)
            {
                if (i < powerups.Count)
                {
                    _powerupSlots[i].SetPowerup(powerups[i]);
                    _powerupSlots[i].gameObject.SetActive(true);
                }
                else
                {
                    _powerupSlots[i].gameObject.SetActive(false);
                }
            }
        }

        private void OnLaneChanged(int lane)
        {
            UpdateLaneIndicator(lane);
        }

        private void UpdateLaneIndicator(int activeLane)
        {
            for (int i = 0; i < _laneIndicators.Count; i++)
            {
                var indicator = _laneIndicators[i];
                bool isActive = i == activeLane;
                indicator.color = isActive ? Color.cyan : new Color(1, 1, 1, 0.3f);
                indicator.transform.DOScale(isActive ? 1.3f : 1f, 0.15f).SetEase(Ease.OutQuad);
            }
        }

        private void OnPauseChanged(bool isPaused)
        {
            _pausePanel.SetActive(isPaused);
            Time.timeScale = isPaused ? 0f : 1f;

            if (isPaused)
            {
                _pausePanel.transform.localScale = Vector3.zero;
                _pausePanel.transform.DOScale(1f, 0.2f).SetEase(Ease.OutBack).SetUpdate(true);
            }
        }

        private void OnBossHealthChanged(BossHealthData data)
        {
            if (data == null || data.maxHealth <= 0)
            {
                _bossHealthPanel.SetActive(false);
                return;
            }

            _bossHealthPanel.SetActive(true);
            _bossNameText.text = data.bossName;
            _bossHealthBar.DOValue((float)data.currentHealth / data.maxHealth, 0.3f).SetEase(Ease.OutCubic);

            if (data.phaseChanged)
            {
                // 阶段变化特效
                _bossHealthBar.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 5, 0.5f);
            }
        }

        private void OnCheckpointReached(CheckpointData data)
        {
            if (data == null) return;

            var toast = Instantiate(_checkpointToastPrefab, _toastContainer);
            var text = toast.GetComponentInChildren<TMP_Text>();
            if (text != null) text.text = $"检查点 {data.index + 1}";

            // 动画
            toast.transform.localPosition = Vector3.up * 100f;
            toast.transform.localScale = Vector3.zero;
            var seq = DOTween.Sequence();
            seq.Append(toast.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack));
            seq.Join(toast.transform.DOLocalMoveY(0f, 0.3f).SetEase(Ease.OutCubic));
            seq.AppendInterval(2f);
            seq.Append(toast.transform.DOScale(0f, 0.2f).SetEase(Ease.InBack));
            seq.OnComplete(() => Destroy(toast));
        }

        private string FormatDistance(float distance)
        {
            if (distance >= 1000f)
                return $"{distance / 1000f:F1}km";
            return $"{distance:F0}m";
        }

        protected override void OnDispose()
        {
            _coinPulseSequence?.Kill();
            Time.timeScale = 1f;
            base.OnDispose();
        }
    }

    // ===================== Powerup Slot 组件 =====================

    public class PowerupSlot : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Image _cooldownOverlay;
        [SerializeField] private TMP_Text _durationText;
        [SerializeField] private ParticleSystem _activeEffect;

        private int _slotIndex;
        private float _endTime;

        public void Initialize(int index)
        {
            _slotIndex = index;
        }

        public void SetPowerup(ActivePowerup powerup)
        {
            _icon.sprite = powerup.icon;
            _endTime = Time.time + powerup.remainingDuration;
            _cooldownOverlay.fillAmount = 1f;

            if (_activeEffect != null) _activeEffect.Play();

            // 启动倒计时协程
            StartCoroutine(CooldownRoutine());
        }

        private System.Collections.IEnumerator CooldownRoutine()
        {
            while (Time.time < _endTime)
            {
                float remaining = _endTime - Time.time;
                _cooldownOverlay.fillAmount = remaining / 10f; // 假设最大 10 秒
                if (_durationText != null) _durationText.text = $"{remaining:F1}s";
                yield return null;
            }
            gameObject.SetActive(false);
        }
    }

    // ===================== ViewModel =====================

    public class LevelHUDViewModel : ViewModelBase
    {
        public ReactiveProperty<float> Distance { get; } = new ReactiveProperty<float>(0f);
        public ReactiveProperty<int> Coins { get; } = new ReactiveProperty<int>(0);
        public ReactiveProperty<float> Multiplier { get; } = new ReactiveProperty<float>(1f);
        public ReactiveProperty<List<ActivePowerup>> ActivePowerups { get; } = new ReactiveProperty<List<ActivePowerup>>(new List<ActivePowerup>());
        public ReactiveProperty<int> CurrentLane { get; } = new ReactiveProperty<int>(1);
        public ReactiveProperty<bool> IsPaused { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<BossHealthData> BossHealth { get; } = new ReactiveProperty<BossHealthData>();
        public ReactiveProperty<CheckpointData> CheckpointReached { get; } = new ReactiveProperty<CheckpointData>();

        public ReactiveCommand PauseCommand { get; }
        public ReactiveCommand ResumeCommand { get; }
        public ReactiveCommand RestartCommand { get; }
        public ReactiveCommand QuitCommand { get; }

        public LevelHUDViewModel()
        {
            PauseCommand = CreateCommand(OnPause);
            ResumeCommand = CreateCommand(OnResume);
            RestartCommand = CreateCommand(OnRestart);
            QuitCommand = CreateCommand(OnQuit);
        }

        public void UpdateDistance(float distance) => Distance.Value = distance;
        public void AddCoins(int amount) => Coins.Value += amount;
        public void SetMultiplier(float multiplier) => Multiplier.Value = multiplier;
        public void SetPowerups(List<ActivePowerup> powerups) => ActivePowerups.Value = powerups;
        public void SetLane(int lane) => CurrentLane.Value = lane;
        public void ShowBossHealth(string name, int current, int max, bool phaseChanged = false)
            => BossHealth.Value = new BossHealthData { bossName = name, currentHealth = current, maxHealth = max, phaseChanged = phaseChanged };
        public void HideBossHealth() => BossHealth.Value = null;
        public void TriggerCheckpoint(int index, Vector3 position)
            => CheckpointReached.Value = new CheckpointData { index = index, position = position };

        private async UniTask OnPause() => IsPaused.Value = true;
        private async UniTask OnResume() => IsPaused.Value = false;
        private async UniTask OnRestart() { await UniTask.Yield(); }
        private async UniTask OnQuit() { await UniTask.Yield(); }
    }

    // ===================== 数据结构 =====================

    [Serializable]
    public class ActivePowerup
    {
        public PowerupType type;
        public Sprite icon;
        public float remainingDuration;
        public float totalDuration;
    }

    /// <summary>
    /// 道具类型
    /// </summary>
    public enum PowerupType
    {
        None,
        Shield,       // 护盾
        Magnet,       // 磁铁
        SpeedBoost,   // 加速
        DoubleCoins,  // 双倍金币
        SlowMotion,   // 减速时间
        ExtraLife     // 额外生命
    }

    [Serializable]
    public class BossHealthData
    {
        public string bossName;
        public int currentHealth;
        public int maxHealth;
        public bool phaseChanged;
    }

    [Serializable]
    public class CheckpointData
    {
        public int index;
        public Vector3 position;
    }
}