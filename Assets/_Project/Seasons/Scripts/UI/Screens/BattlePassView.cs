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
    /// 战斗通行证界面 - 双轨奖励、进度、购买、领取
    /// </summary>
    public class BattlePassView : ViewBase<BattlePassViewModel>
    {
        [Header("顶部栏")]
        [SerializeField] private TMP_Text _seasonTitleText;
        [SerializeField] private TMP_Text _tierText;
        [SerializeField] private TMP_Text _xpText;
        [SerializeField] private Slider _overallProgressBar;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _infoButton;

        [Header("高级通行证")]
        [SerializeField] private GameObject _premiumPanel;
        [SerializeField] private TMP_Text _premiumPriceText;
        [SerializeField] private Button _buyPremiumButton;
        [SerializeField] private Image _premiumOwnedBadge;

        [Header("奖励轨道")]
        [SerializeField] private Transform _freeTrackContainer;
        [SerializeField] private Transform _premiumTrackContainer;
        [SerializeField] private GameObject _tierItemPrefab;
        [SerializeField] private ScrollRect _trackScrollRect;
        [SerializeField] private Button _scrollToCurrentButton;

        [Header("里程碑")]
        [SerializeField] private Transform _milestoneContainer;
        [SerializeField] private GameObject _milestoneItemPrefab;

        [Header("周上限")]
        [SerializeField] private Slider _weeklyCapBar;
        [SerializeField] private TMP_Text _weeklyCapText;
        [SerializeField] private TMP_Text _weeklyResetText;

        [Header("动画")]
        [SerializeField] private float _tierAnimationDelay = 0.02f;
        [SerializeField] private float _scrollDuration = 0.5f;

        private BattlePassViewModel _viewModel;
        private readonly List<BattlePassTierItem> _freeTierItems = new List<BattlePassTierItem>();
        private readonly List<BattlePassTierItem> _premiumTierItems = new List<BattlePassTierItem>();
        private readonly List<GameObject> _milestoneItems = new List<GameObject>();
        private int _currentTierIndex = 0;

        protected override void OnBind()
        {
            _viewModel = ViewModel;

            _closeButton.onClick.AddListener(() => _viewModel.CloseCommand.Execute());
            _infoButton.onClick.AddListener(() => _viewModel.ShowInfoCommand.Execute());
            _buyPremiumButton.onClick.AddListener(() => _viewModel.BuyPremiumCommand.Execute());
            _scrollToCurrentButton.onClick.AddListener(ScrollToCurrentTier);

            BindDisposables.Add(_viewModel.ViewData.Subscribe(OnViewDataChanged));
            BindDisposables.Add(_viewModel.PremiumStatus.Subscribe(OnPremiumStatusChanged));
            BindDisposables.Add(_viewModel.WeeklyCapData.Subscribe(OnWeeklyCapChanged));
        }

        private void OnViewDataChanged(BattlePassViewData data)
        {
            if (data == null) return;

            _seasonTitleText.text = data.seasonName;
            _tierText.text = $"T{data.currentTier}";
            _xpText.text = $"{data.currentXP:N0} / {data.nextTierXP:N0} XP";
            _overallProgressBar.DOValue(data.overallProgress, 0.5f).SetEase(Ease.OutCubic);

            // 重建奖励列表
            RebuildTierList(data.tiers);
        }

        private void RebuildTierList(List<BattlePassTierViewData> tiers)
        {
            // 清理旧项
            foreach (var item in _freeTierItems) if (item != null) Destroy(item.gameObject);
            foreach (var item in _premiumTierItems) if (item != null) Destroy(item.gameObject);
            _freeTierItems.Clear();
            _premiumTierItems.Clear();

            _currentTierIndex = Math.Max(0, _viewModel.ViewData.Value.currentTier - 1);

            for (int i = 0; i < tiers.Count; i++)
            {
                var tierData = tiers[i];

                // 免费轨道
                var freeItem = CreateTierItem(_freeTrackContainer, tierData, false, i);
                _freeTierItems.Add(freeItem);

                // 高级轨道
                var premiumItem = CreateTierItem(_premiumTrackContainer, tierData, true, i);
                _premiumTierItems.Add(premiumItem);
            }

            // 里程碑
            RebuildMilestones(tiers);

            // 滚动到当前等级
            ScrollToCurrentTier();
        }

        private BattlePassTierItem CreateTierItem(Transform parent, BattlePassTierViewData data, bool isPremium, int index)
        {
            var item = Instantiate(_tierItemPrefab, parent).GetComponent<BattlePassTierItem>();
            item.Initialize(data, isPremium, _viewModel);
            item.transform.localScale = Vector3.zero;
            item.transform.DOScale(1f, 0.3f).SetDelay(index * _tierAnimationDelay).SetEase(Ease.OutBack);
            return item;
        }

        private void RebuildMilestones(List<BattlePassTierViewData> tiers)
        {
            foreach (var item in _milestoneItems) if (item != null) Destroy(item);
            _milestoneItems.Clear();

            var milestones = tiers.Where(t => t.isMilestone).ToList();
            for (int i = 0; i < milestones.Count; i++)
            {
                var milestone = milestones[i];
                var item = Instantiate(_milestoneItemPrefab, _milestoneContainer);
                _milestoneItems.Add(item);

                var nameText = item.transform.Find("Name")?.GetComponent<TMP_Text>();
                var icon = item.transform.Find("Icon")?.GetComponent<Image>();
                var unlockText = item.transform.Find("UnlockText")?.GetComponent<TMP_Text>();

                if (nameText != null) nameText.text = milestone.milestoneTitle;
                if (icon != null && milestone.milestoneReward != null) icon.sprite = milestone.milestoneReward.icon;
                if (unlockText != null) unlockText.text = milestone.unlockDescription;

                item.transform.localScale = Vector3.zero;
                item.transform.DOScale(1f, 0.3f).SetDelay(i * 0.1f).SetEase(Ease.OutBack);
            }
        }

        private void OnPremiumStatusChanged(bool isPremium)
        {
            _premiumOwnedBadge.enabled = isPremium;
            _buyPremiumButton.gameObject.SetActive(!isPremium);
            _premiumPanel.SetActive(!isPremium);

            // 更新高级轨道项的可领取状态
            foreach (var item in _premiumTierItems)
            {
                item.SetPremiumUnlocked(isPremium);
            }
        }

        private void OnWeeklyCapChanged(WeeklyCapData data)
        {
            _weeklyCapBar.DOValue(data.progress, 0.5f).SetEase(Ease.OutCubic);
            _weeklyCapText.text = $"{data.earned:N0} / {data.cap:N0}";
            _weeklyResetText.text = $"重置: {data.resetTime:MM/dd HH:mm}";
        }

        private void ScrollToCurrentTier()
        {
            if (_currentTierIndex < 0 || _currentTierIndex >= _freeTierItems.Count) return;

            var targetPos = _freeTierItems[_currentTierIndex].transform.localPosition;
            var contentRect = _trackScrollRect.content;
            var viewportHeight = _trackScrollRect.viewport.rect.height;
            var targetY = -targetPos.y + viewportHeight * 0.5f;
            targetY = Mathf.Clamp(targetY, 0, contentRect.rect.height - viewportHeight);

            contentRect.DOAnchorPosY(targetY, _scrollDuration).SetEase(Ease.OutCubic);
        }

        protected override void OnDispose()
        {
            foreach (var item in _freeTierItems) if (item != null) Destroy(item.gameObject);
            foreach (var item in _premiumTierItems) if (item != null) Destroy(item.gameObject);
            foreach (var item in _milestoneItems) if (item != null) Destroy(item);
            base.OnDispose();
        }
    }

    // ===================== Tier Item 组件 =====================

    public class BattlePassTierItem : MonoBehaviour
    {
        [SerializeField] private TMP_Text _tierNumberText;
        [SerializeField] private Image _freeRewardIcon;
        [SerializeField] private TMP_Text _freeRewardAmountText;
        [SerializeField] private GameObject _freeClaimedMark;
        [SerializeField] private Button _freeClaimButton;
        [SerializeField] private Image _premiumRewardIcon;
        [SerializeField] private TMP_Text _premiumRewardAmountText;
        [SerializeField] private GameObject _premiumClaimedMark;
        [SerializeField] private Button _premiumClaimButton;
        [SerializeField] private GameObject _premiumLockOverlay;
        [SerializeField] private Image _milestoneBadge;
        [SerializeField] private ParticleSystem _claimEffect;

        private BattlePassTierViewData _data;
        private bool _isPremium;
        private BattlePassViewModel _viewModel;

        public void Initialize(BattlePassTierViewData data, bool isPremium, BattlePassViewModel viewModel)
        {
            _data = data;
            _isPremium = isPremium;
            _viewModel = viewModel;

            _tierNumberText.text = data.tier.ToString();
            _milestoneBadge.enabled = data.isMilestone;

            // 免费奖励
            if (data.freeReward != null)
            {
                _freeRewardIcon.sprite = data.freeReward.icon;
                _freeRewardAmountText.text = data.freeReward.quantity > 1 ? $"x{data.freeReward.quantity}" : "";
                _freeClaimedMark.SetActive(data.freeClaimed);
                _freeClaimButton.gameObject.SetActive(data.canClaimFree && !data.freeClaimed);
                _freeClaimButton.onClick.AddListener(() => _viewModel.ClaimTierRewardCommand.Execute(new ClaimTierParam { tier = data.tier, isPremium = false }));
            }

            // 高级奖励
            if (data.premiumReward != null)
            {
                _premiumRewardIcon.sprite = data.premiumReward.icon;
                _premiumRewardAmountText.text = data.premiumReward.quantity > 1 ? $"x{data.premiumReward.quantity}" : "";
                _premiumClaimedMark.SetActive(data.premiumClaimed);
                _premiumClaimButton.gameObject.SetActive(data.canClaimPremium && !data.premiumClaimed);
                _premiumClaimButton.onClick.AddListener(() => _viewModel.ClaimTierRewardCommand.Execute(new ClaimTierParam { tier = data.tier, isPremium = true }));
            }

            SetPremiumUnlocked(_viewModel.ViewData.Value.isPremium);
        }

        public void SetPremiumUnlocked(bool unlocked)
        {
            _premiumLockOverlay.SetActive(!unlocked);
            _premiumClaimButton.interactable = unlocked;
        }
    }

    [Serializable]
    public class ClaimTierParam
    {
        public int tier;
        public bool isPremium;
    }

    // ===================== ViewModel =====================

    public class BattlePassViewModel : ViewModelBase
    {
        private readonly BattlePassSystem _battlePassSystem;
        private readonly SeasonManager _seasonManager;
        private readonly MockServer _mockServer;

        public ReactiveProperty<BattlePassViewData> ViewData { get; } = new ReactiveProperty<BattlePassViewData>();
        public ReactiveProperty<bool> PremiumStatus { get; } = new ReactiveProperty<bool>();
        public ReactiveProperty<WeeklyCapData> WeeklyCapData { get; } = new ReactiveProperty<WeeklyCapData>();

        public ReactiveCommand CloseCommand { get; }
        public ReactiveCommand ShowInfoCommand { get; }
        public ReactiveCommand BuyPremiumCommand { get; }
        public ReactiveCommand<ClaimTierParam> ClaimTierRewardCommand { get; }

        public BattlePassViewModel(BattlePassSystem battlePassSystem, SeasonManager seasonManager, MockServer mockServer)
        {
            _battlePassSystem = battlePassSystem;
            _seasonManager = seasonManager;
            _mockServer = mockServer;

            CloseCommand = CreateCommand(OnClose);
            ShowInfoCommand = CreateCommand(OnShowInfo);
            BuyPremiumCommand = CreateCommand(OnBuyPremium);
            ClaimTierRewardCommand = CreateCommand<ClaimTierParam>(OnClaimTierReward);
        }

        protected override async UniTask OnInitializeAsync()
        {
            await RefreshDataAsync();
        }

        public async UniTask RefreshDataAsync()
        {
            var bpData = _battlePassSystem.GetBattlePassViewData();
            var config = _seasonManager.CurrentConfig;

            var tiers = new List<BattlePassTierViewData>();
            if (_battlePassSystem.BattlePassTable != null && _battlePassSystem.BattlePassTable.tiers != null)
            {
                foreach (var tier in _battlePassSystem.BattlePassTable.tiers)
                {
                    tiers.Add(new BattlePassTierViewData
                    {
                        tier = tier.level,
                        xpRequired = tier.xpRequired,
                        freeReward = ConvertReward(tier.freeReward),
                        premiumReward = ConvertReward(tier.premiumReward),
                        isMilestone = tier.isMilestone,
                        milestoneTitle = tier.milestoneTitle,
                        milestoneReward = tier.isMilestone ? ConvertReward(tier.premiumReward) : null,
                        unlockDescription = tier.isMilestone ? tier.milestoneTitle : $"Tier {tier.level}",
                        freeClaimed = bpData.claimedFreeTiers.Contains(tier.level),
                        premiumClaimed = bpData.claimedPremiumTiers.Contains(tier.level),
                        canClaimFree = tier.level <= bpData.currentTier && !bpData.claimedFreeTiers.Contains(tier.level),
                        canClaimPremium = bpData.isPremiumUnlocked && tier.level <= bpData.currentTier && !bpData.claimedPremiumTiers.Contains(tier.level)
                    });
                }
            }

            ViewData.Value = new BattlePassViewData
            {
                seasonName = config?.seasonName ?? "Season",
                currentTier = bpData.currentTier,
                currentXP = bpData.currentXP,
                nextTierXP = bpData.nextTierXP,
                overallProgress = bpData.overallProgress,
                isPremium = bpData.isPremiumUnlocked,
                tiers = tiers
            };

            PremiumStatus.Value = bpData.isPremiumUnlocked;

            WeeklyCapData.Value = new WeeklyCapData
            {
                earned = bpData.weeklyXPEarned,
                cap = bpData.weeklyCap,
                progress = bpData.weeklyCap > 0 ? (float)bpData.weeklyXPEarned / bpData.weeklyCap : 0f,
                resetTime = DateTimeOffset.FromUnixTimeSeconds(bpData.lastWeeklyReset).AddDays(7).DateTime
            };
        }

        private RewardViewData ConvertReward(RewardBundle bundle)
        {
            if (bundle?.items == null || bundle.items.Length == 0) return null;
            // 一个奖励格展示首个代表性物品
            var item = bundle.items[0];
            return new RewardViewData
            {
                type = MapRewardType(item.type),
                itemId = item.itemId,
                quantity = item.quantity,
                rarity = MapRarity(item.rarity),
                icon = null // 运行时从资源加载
            };
        }

        private static RewardType MapRewardType(TomCatRunner.Seasons.RewardType type)
        {
            switch (type)
            {
                case TomCatRunner.Seasons.RewardType.Skin: return RewardType.Skin;
                case TomCatRunner.Seasons.RewardType.Mount: return RewardType.Character;
                case TomCatRunner.Seasons.RewardType.Pet: return RewardType.Character;
                case TomCatRunner.Seasons.RewardType.Emote: return RewardType.Custom;
                case TomCatRunner.Seasons.RewardType.Title: return RewardType.Custom;
                case TomCatRunner.Seasons.RewardType.ProfileFrame: return RewardType.Custom;
                case TomCatRunner.Seasons.RewardType.LootBox: return RewardType.Custom;
                case TomCatRunner.Seasons.RewardType.Experience: return RewardType.BattlePassXP;
                default: return RewardType.Currency;
            }
        }

        private static Rarity MapRarity(TomCatRunner.Seasons.Rarity rarity)
        {
            switch (rarity)
            {
                case TomCatRunner.Seasons.Rarity.Uncommon: return Rarity.Uncommon;
                case TomCatRunner.Seasons.Rarity.Rare: return Rarity.Rare;
                case TomCatRunner.Seasons.Rarity.Epic: return Rarity.Epic;
                case TomCatRunner.Seasons.Rarity.Legendary: return Rarity.Legendary;
                case TomCatRunner.Seasons.Rarity.Mythic: return Rarity.Mythic;
                default: return Rarity.Common;
            }
        }

        private async UniTask OnClose()
        {
            await UniTask.Yield();
        }

        private async UniTask OnShowInfo()
        {
            // 显示详细说明
            await UniTask.Yield();
        }

        private async UniTask OnBuyPremium()
        {
            // 购买高级通行证
            await UniTask.Yield();
        }

        private async UniTask OnClaimTierReward(ClaimTierParam param)
        {
            // 领取奖励
            await UniTask.Yield();
        }
    }

    // ===================== 数据结构 =====================

    [Serializable]
    public class BattlePassViewData
    {
        public string seasonName;
        public int currentTier;
        public int currentXP;
        public int nextTierXP;
        public float overallProgress;
        public bool isPremium;
        public List<BattlePassTierViewData> tiers;
    }

    [Serializable]
    public class BattlePassTierViewData
    {
        public int tier;
        public int xpRequired;
        public RewardViewData freeReward;
        public RewardViewData premiumReward;
        public bool isMilestone;
        public string milestoneTitle;
        public RewardViewData milestoneReward;
        public string unlockDescription;
        public bool freeClaimed;
        public bool premiumClaimed;
        public bool canClaimFree;
        public bool canClaimPremium;
    }

    [Serializable]
    public class RewardViewData
    {
        public RewardType type;
        public string itemId;
        public int quantity;
        public Rarity rarity;
        public Sprite icon;
    }

    [Serializable]
    public class WeeklyCapData
    {
        public int earned;
        public int cap;
        public float progress;
        public DateTime resetTime;
    }
}