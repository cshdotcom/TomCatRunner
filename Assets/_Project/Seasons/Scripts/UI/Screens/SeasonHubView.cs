using System;
using System.Collections.Generic;
using System.Linq;
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
    /// 赛季主界面 - 展示当前赛季、阶段、主题、进度、入口
    /// </summary>
    public class SeasonHubView : ViewBase<SeasonHubViewModel>
    {
        [Header("主视觉")]
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Image _themeDecorationImage;
        [SerializeField] private RawImage _themeVideoPlayer;
        [SerializeField] private ParticleSystem _ambientParticles;

        [Header("赛季信息")]
        [SerializeField] private TMP_Text _seasonTitleText;
        [SerializeField] private TMP_Text _seasonSubtitleText;
        [SerializeField] private TMP_Text _phaseNameText;
        [SerializeField] private TMP_Text _phaseDescriptionText;
        [SerializeField] private Image _phaseIcon;
        [SerializeField] private Slider _phaseProgressBar;
        [SerializeField] private TMP_Text _phaseProgressText;
        [SerializeField] private TMP_Text _timeRemainingText;

        [Header("主题预览")]
        [SerializeField] private Transform _themePreviewContainer;
        [SerializeField] private Button _prevThemeButton;
        [SerializeField] private Button _nextThemeButton;
        [SerializeField] private TMP_Text _currentThemeNameText;

        [Header("功能入口")]
        [SerializeField] private Button _startRunButton;
        [SerializeField] private Button _battlePassButton;
        [SerializeField] private Button _eventsButton;
        [SerializeField] private Button _shopButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _profileButton;

        [Header("通行证预览")]
        [SerializeField] private TMP_Text _bpTierText;
        [SerializeField] private TMP_Text _bpXPText;
        [SerializeField] private Slider _bpProgressBar;
        [SerializeField] private Image _bpPremiumBadge;
        [SerializeField] private Button _bpUpgradeButton;

        [Header("活动预览")]
        [SerializeField] private Transform _eventPreviewContainer;
        [SerializeField] private GameObject _eventPreviewItemPrefab;
        [SerializeField] private Button _viewAllEventsButton;

        [Header("每日奖励")]
        [SerializeField] private GameObject _dailyRewardPanel;
        [SerializeField] private Button _claimDailyButton;
        [SerializeField] private TMP_Text _dailyRewardText;
        [SerializeField] private Image _dailyRewardIcon;

        [Header("动画")]
        [SerializeField] private float _staggerDelay = 0.05f;
        [SerializeField] private Ease _enterEase = Ease.OutBack;

        private SeasonHubViewModel _viewModel;
        private readonly List<GameObject> _eventPreviewItems = new List<GameObject>();
        private Sequence _enterSequence;
        private IDisposable _timeUpdateSubscription;

        protected override void OnBind()
        {
            _viewModel = ViewModel;

            // 按钮绑定
            _startRunButton.onClick.AddListener(() => _viewModel.StartRunCommand.Execute());
            _battlePassButton.onClick.AddListener(() => _viewModel.OpenBattlePassCommand.Execute());
            _eventsButton.onClick.AddListener(() => _viewModel.OpenEventsCommand.Execute());
            _shopButton.onClick.AddListener(() => _viewModel.OpenShopCommand.Execute());
            _settingsButton.onClick.AddListener(() => _viewModel.OpenSettingsCommand.Execute());
            _profileButton.onClick.AddListener(() => _viewModel.OpenProfileCommand.Execute());
            _bpUpgradeButton.onClick.AddListener(() => _viewModel.UpgradeBattlePassCommand.Execute());
            _claimDailyButton.onClick.AddListener(() => _viewModel.ClaimDailyRewardCommand.Execute());
            _viewAllEventsButton.onClick.AddListener(() => _viewModel.OpenEventsCommand.Execute());
            _prevThemeButton.onClick.AddListener(() => _viewModel.PrevThemeCommand.Execute());
            _nextThemeButton.onClick.AddListener(() => _viewModel.NextThemeCommand.Execute());

            // 订阅数据变化
            BindDisposables.Add(_viewModel.SeasonInfo.Subscribe(OnSeasonInfoChanged));
            BindDisposables.Add(_viewModel.PhaseInfo.Subscribe(OnPhaseInfoChanged));
            BindDisposables.Add(_viewModel.BattlePassPreview.Subscribe(OnBattlePassPreviewChanged));
            BindDisposables.Add(_viewModel.EventPreviews.Subscribe(OnEventPreviewsChanged));
            BindDisposables.Add(_viewModel.DailyReward.Subscribe(OnDailyRewardChanged));
            BindDisposables.Add(_viewModel.CurrentTheme.Subscribe(OnThemeChanged));

            // 时间更新
            _timeUpdateSubscription = Observable.Interval(TimeSpan.FromSeconds(1))
                .Subscribe(_ => _viewModel.UpdateTimeRemaining());
        }

        private void OnSeasonInfoChanged(SeasonInfo info)
        {
            if (info == null) return;

            _seasonTitleText.text = info.displayName;
            _seasonSubtitleText.text = info.description;

            // 背景过渡
            if (info.backgroundSprite != null)
            {
                _backgroundImage.DOFade(0f, 0.3f).OnComplete(() =>
                {
                    _backgroundImage.sprite = info.backgroundSprite;
                    _backgroundImage.DOFade(1f, 0.5f);
                });
            }

            // 主题装饰
            if (info.themeDecoration != null)
            {
                _themeDecorationImage.sprite = info.themeDecoration;
            }
        }

        private void OnPhaseInfoChanged(PhaseInfo info)
        {
            if (info == null) return;

            _phaseNameText.text = info.name;
            _phaseDescriptionText.text = info.description;
            _phaseIcon.sprite = info.icon;

            _phaseProgressBar.DOValue(info.progress, 0.5f).SetEase(Ease.OutCubic);
            _phaseProgressText.text = $"{info.progress * 100:F0}%";

            _viewModel.UpdateTimeRemaining();
        }

        private void OnBattlePassPreviewChanged(BattlePassPreview preview)
        {
            if (preview == null) return;

            _bpTierText.text = $"T{preview.currentTier}";
            _bpXPText.text = $"{preview.currentXP:N0} / {preview.nextTierXP:N0} XP";
            _bpProgressBar.DOValue(preview.progress, 0.5f).SetEase(Ease.OutCubic);
            _bpPremiumBadge.enabled = preview.isPremium;
            _bpUpgradeButton.gameObject.SetActive(!preview.isPremium);
        }

        private void OnEventPreviewsChanged(List<EventPreview> events)
        {
            // 清理旧项
            foreach (var item in _eventPreviewItems)
            {
                if (item != null) Destroy(item);
            }
            _eventPreviewItems.Clear();

            // 创建新项
            for (int i = 0; i < Math.Min(events.Count, 3); i++)
            {
                var evt = events[i];
                var item = Instantiate(_eventPreviewItemPrefab, _eventPreviewContainer);
                _eventPreviewItems.Add(item);

                var icon = item.transform.Find("Icon")?.GetComponent<Image>();
                var nameText = item.transform.Find("Name")?.GetComponent<TMP_Text>();
                var progressBar = item.transform.Find("ProgressBar")?.GetComponent<Slider>();
                var progressText = item.transform.Find("ProgressText")?.GetComponent<TMP_Text>();
                var timeText = item.transform.Find("TimeText")?.GetComponent<TMP_Text>();
                var claimButton = item.transform.Find("ClaimButton")?.GetComponent<Button>();

                if (icon != null) icon.sprite = evt.icon;
                if (nameText != null) nameText.text = evt.displayName;
                if (progressBar != null) progressBar.value = evt.progress;
                if (progressText != null) progressText.text = $"{evt.currentProgress}/{evt.targetProgress}";
                if (timeText != null) timeText.text = FormatTimeRemaining(evt.timeRemaining);

                if (claimButton != null && evt.canClaim)
                {
                    claimButton.gameObject.SetActive(true);
                    claimButton.onClick.AddListener(() => _viewModel.ClaimEventRewardCommand.Execute(evt.eventId));
                }
                else if (claimButton != null)
                {
                    claimButton.gameObject.SetActive(false);
                }

                // 入场动画
                item.transform.localScale = Vector3.zero;
                item.transform.DOScale(1f, 0.3f).SetDelay(i * _staggerDelay).SetEase(_enterEase);
            }
        }

        private void OnDailyRewardChanged(DailyRewardInfo reward)
        {
            if (reward == null || !reward.isAvailable)
            {
                _dailyRewardPanel.SetActive(false);
                return;
            }

            _dailyRewardPanel.SetActive(true);
            _dailyRewardText.text = reward.description;
            _dailyRewardIcon.sprite = reward.icon;
            _claimDailyButton.interactable = !reward.isClaimed;
            _claimDailyButton.GetComponentInChildren<TMP_Text>().text = reward.isClaimed ? "已领取" : "领取";
        }

        private void OnThemeChanged(ThemeProfile theme)
        {
            if (theme == null) return;

            _currentThemeNameText.text = theme.displayName;

            // 粒子系统颜色更新（取自主题色板）
            if (_ambientParticles != null)
            {
                var palette = theme.colorPalette;
                var particleColor = palette != null ? palette.particlePrimary : theme.themeColor;
                var glowColor = palette != null ? palette.glowPrimary : theme.themeColor;
                var main = _ambientParticles.main;
                main.startColor = new ParticleSystem.MinMaxGradient(particleColor, glowColor);
            }
        }

        protected override async UniTask PlayEnterAnimationAsync()
        {
            _enterSequence?.Kill();
            _enterSequence = DOTween.Sequence();

            // 背景先显示
            _backgroundImage.color = new Color(1, 1, 1, 0);
            _enterSequence.Append(_backgroundImage.DOFade(1f, 0.5f));

            // 标题滑入
            _seasonTitleText.transform.localPosition += Vector3.up * 100f;
            _seasonTitleText.alpha = 0;
            _enterSequence.Join(_seasonTitleText.transform.DOLocalMoveY(_seasonTitleText.transform.localPosition.y - 100f, 0.6f).SetEase(_enterEase));
            _enterSequence.Join(_seasonTitleText.DOFade(1f, 0.4f));

            // 副标题
            _seasonSubtitleText.transform.localPosition += Vector3.up * 50f;
            _seasonSubtitleText.alpha = 0;
            _enterSequence.Join(_seasonSubtitleText.transform.DOLocalMoveY(_seasonSubtitleText.transform.localPosition.y - 50f, 0.5f).SetDelay(0.1f).SetEase(_enterEase));
            _enterSequence.Join(_seasonSubtitleText.DOFade(1f, 0.3f).SetDelay(0.1f));

            // 阶段信息
            var phaseGroup = _phaseNameText.transform.parent;
            phaseGroup.localPosition += Vector3.down * 50f;
            phaseGroup.GetComponent<CanvasGroup>().alpha = 0;
            _enterSequence.Join(phaseGroup.DOLocalMoveY(phaseGroup.localPosition.y + 50f, 0.5f).SetDelay(0.2f).SetEase(_enterEase));
            _enterSequence.Join(phaseGroup.GetComponent<CanvasGroup>().DOFade(1f, 0.3f).SetDelay(0.2f));

            // 功能按钮交错入场
            var buttons = new[] { _startRunButton, _battlePassButton, _eventsButton, _shopButton, _settingsButton, _profileButton };
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                var btnTransform = buttons[i].transform;
                btnTransform.localScale = Vector3.zero;
                _enterSequence.Join(btnTransform.DOScale(1f, 0.4f).SetDelay(0.3f + i * _staggerDelay).SetEase(Ease.OutBack));
            }

            // 通行证预览
            var bpGroup = _bpTierText.transform.parent;
            bpGroup.localPosition += Vector3.right * 100f;
            bpGroup.GetComponent<CanvasGroup>().alpha = 0;
            _enterSequence.Join(bpGroup.DOLocalMoveX(bpGroup.localPosition.x - 100f, 0.5f).SetDelay(0.4f).SetEase(_enterEase));
            _enterSequence.Join(bpGroup.GetComponent<CanvasGroup>().DOFade(1f, 0.3f).SetDelay(0.4f));

            await _enterSequence.AsyncWaitForCompletion();
        }

        private string FormatTimeRemaining(TimeSpan time)
        {
            if (time.TotalDays >= 1)
                return $"{time.Days}天{time.Hours}小时";
            if (time.TotalHours >= 1)
                return $"{time.Hours}小时{time.Minutes}分";
            return $"{time.Minutes}分{time.Seconds}秒";
        }

        protected override void OnDispose()
        {
            _enterSequence?.Kill();
            _timeUpdateSubscription?.Dispose();
            foreach (var item in _eventPreviewItems)
            {
                if (item != null) Destroy(item);
            }
            base.OnDispose();
        }
    }

    // ===================== ViewModel =====================

    public class SeasonHubViewModel : ViewModelBase
    {
        private readonly SeasonManager _seasonManager;
        private readonly BattlePassSystem _battlePassSystem;
        private readonly EventScheduler _eventScheduler;
        private readonly MockServer _mockServer;

        public ReactiveProperty<SeasonInfo> SeasonInfo { get; } = new ReactiveProperty<SeasonInfo>();
        public ReactiveProperty<PhaseInfo> PhaseInfo { get; } = new ReactiveProperty<PhaseInfo>();
        public ReactiveProperty<BattlePassPreview> BattlePassPreview { get; } = new ReactiveProperty<BattlePassPreview>();
        public ReactiveProperty<List<EventPreview>> EventPreviews { get; } = new ReactiveProperty<List<EventPreview>>(new List<EventPreview>());
        public ReactiveProperty<DailyRewardInfo> DailyReward { get; } = new ReactiveProperty<DailyRewardInfo>();
        public ReactiveProperty<ThemeProfile> CurrentTheme { get; } = new ReactiveProperty<ThemeProfile>();

        public ReactiveCommand StartRunCommand { get; }
        public ReactiveCommand OpenBattlePassCommand { get; }
        public ReactiveCommand OpenEventsCommand { get; }
        public ReactiveCommand OpenShopCommand { get; }
        public ReactiveCommand OpenSettingsCommand { get; }
        public ReactiveCommand OpenProfileCommand { get; }
        public ReactiveCommand UpgradeBattlePassCommand { get; }
        public ReactiveCommand ClaimDailyRewardCommand { get; }
        public ReactiveCommand<string> ClaimEventRewardCommand { get; }
        public ReactiveCommand PrevThemeCommand { get; }
        public ReactiveCommand NextThemeCommand { get; }

        public SeasonHubViewModel(
            SeasonManager seasonManager,
            BattlePassSystem battlePassSystem,
            EventScheduler eventScheduler,
            MockServer mockServer)
        {
            _seasonManager = seasonManager;
            _battlePassSystem = battlePassSystem;
            _eventScheduler = eventScheduler;
            _mockServer = mockServer;

            StartRunCommand = CreateCommand(OnStartRun);
            OpenBattlePassCommand = CreateCommand(OnOpenBattlePass);
            OpenEventsCommand = CreateCommand(OnOpenEvents);
            OpenShopCommand = CreateCommand(OnOpenShop);
            OpenSettingsCommand = CreateCommand(OnOpenSettings);
            OpenProfileCommand = CreateCommand(OnOpenProfile);
            UpgradeBattlePassCommand = CreateCommand(OnUpgradeBattlePass);
            ClaimDailyRewardCommand = CreateCommand(OnClaimDailyReward);
            ClaimEventRewardCommand = CreateCommand<string>(OnClaimEventReward);
            PrevThemeCommand = CreateCommand(OnPrevTheme);
            NextThemeCommand = CreateCommand(OnNextTheme);
        }

        protected override async UniTask OnInitializeAsync()
        {
            await RefreshAllDataAsync();
        }

        public async UniTask RefreshAllDataAsync()
        {
            // 赛季信息
            var config = _seasonManager.CurrentConfig;
            if (config != null)
            {
                SeasonInfo.Value = new SeasonInfo
                {
                    seasonId = config.seasonId.ToString(),
                    displayName = config.seasonName,
                    description = $"{config.shortCode} 赛季 · {config.seasonName}",
                    backgroundSprite = null, // 从 ThemeProfile 加载
                    themeDecoration = null
                };

                CurrentTheme.Value = _seasonManager.GetCurrentThemeProfile();
            }

            // 阶段信息
            var runtimeData = _seasonManager.RuntimeData;
            if (runtimeData != null && config != null && config.phases != null && config.phases.Length > 0)
            {
                int phaseIndex = _seasonManager.CurrentPhase == SeasonPhase.Preheat ? 0
                    : _seasonManager.CurrentPhase == SeasonPhase.Active
                        ? Mathf.Clamp((int)((DateTimeOffset.UtcNow.ToUnixTimeSeconds() - config.seasonStartTimestamp) / 86400L) / 14, 0, config.phases.Length - 1)
                        : config.phases.Length - 1;
                var phase = config.phases[phaseIndex];
                PhaseInfo.Value = new PhaseInfo
                {
                    name = phase.phaseName,
                    description = $"阶段 {phaseIndex + 1}/{config.phases.Length}",
                    icon = null,
                    progress = _seasonManager.GetTimeUntilSeasonEnd().Ticks > 0
                        ? 1f - Mathf.Clamp01((float)_seasonManager.GetTimeUntilSeasonEnd().Ticks / (float)TimeSpan.FromDays(60).Ticks)
                        : 0f
                };
            }

            // 通行证预览
            if (_battlePassSystem != null)
            {
                var bpData = _battlePassSystem.GetBattlePassViewData();
                BattlePassPreview.Value = new BattlePassPreview
                {
                    currentTier = bpData.currentTier,
                    currentXP = bpData.currentXP,
                    nextTierXP = bpData.nextTierXP,
                    progress = bpData.tierProgress,
                    isPremium = bpData.isPremiumUnlocked
                };
            }

            // 活动预览
            if (_eventScheduler != null)
            {
                var events = _eventScheduler.GetAllEventsViewData();
                EventPreviews.Value = events.Take(3).Select(e => new EventPreview
                {
                    eventId = e.eventId,
                    displayName = e.displayName,
                    icon = e.icon,
                    currentProgress = e.currentProgress,
                    targetProgress = e.targetProgress,
                    progress = e.progressNormalized,
                    timeRemaining = e.timeRemaining,
                    canClaim = e.canClaim
                }).ToList();
            }

            // 每日奖励
            DailyReward.Value = new DailyRewardInfo
            {
                isAvailable = true,
                isClaimed = false,
                description = "每日登录奖励：500 金币 + 10 宝石",
                icon = null
            };
        }

        public void UpdateTimeRemaining()
        {
            if (PhaseInfo.Value != null)
            {
                var config = _seasonManager.CurrentConfig;
                if (config != null)
                {
                    long seasonEnd = config.seasonEndTimestamp;
                    long now = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();
                    long remaining = Math.Max(0, seasonEnd - now);
                    // 更新 UI...
                }
            }
        }

        private async UniTask OnStartRun()
        {
            // 打开关卡选择或直接开始
            await UniTask.Yield();
        }

        private async UniTask OnOpenBattlePass()
        {
            // 打开通行证界面
            await UniTask.Yield();
        }

        private async UniTask OnOpenEvents()
        {
            // 打开活动界面
            await UniTask.Yield();
        }

        private async UniTask OnOpenShop()
        {
            // 打开商店
            await UniTask.Yield();
        }

        private async UniTask OnOpenSettings()
        {
            // 打开设置
            await UniTask.Yield();
        }

        private async UniTask OnOpenProfile()
        {
            // 打开个人资料
            await UniTask.Yield();
        }

        private async UniTask OnUpgradeBattlePass()
        {
            // 购买高级通行证
            await UniTask.Yield();
        }

        private async UniTask OnClaimDailyReward()
        {
            // 领取每日奖励
            DailyReward.Value.isClaimed = true;
            await UniTask.Yield();
        }

        private async UniTask OnClaimEventReward(string eventId)
        {
            // 领取活动奖励
            await UniTask.Yield();
        }

        private async UniTask OnPrevTheme()
        {
            // 切换上一主题
            await UniTask.Yield();
        }

        private async UniTask OnNextTheme()
        {
            // 切换下一主题
            await UniTask.Yield();
        }
    }

    // ===================== 数据结构 =====================

    [Serializable]
    public class SeasonInfo
    {
        public string seasonId;
        public string displayName;
        public string description;
        public Sprite backgroundSprite;
        public Sprite themeDecoration;
    }

    [Serializable]
    public class PhaseInfo
    {
        public string name;
        public string description;
        public Sprite icon;
        public float progress;
    }

    [Serializable]
    public class BattlePassPreview
    {
        public int currentTier;
        public int currentXP;
        public int nextTierXP;
        public float progress;
        public bool isPremium;
    }

    [Serializable]
    public class EventPreview
    {
        public string eventId;
        public string displayName;
        public Sprite icon;
        public int currentProgress;
        public int targetProgress;
        public float progress;
        public TimeSpan timeRemaining;
        public bool canClaim;
    }

    [Serializable]
    public class DailyRewardInfo
    {
        public bool isAvailable;
        public bool isClaimed;
        public string description;
        public Sprite icon;
    }
}