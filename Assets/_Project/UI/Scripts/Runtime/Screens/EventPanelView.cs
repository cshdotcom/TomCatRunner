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
    /// 活动面板 - 活动列表、进度、奖励领取、倒计时
    /// </summary>
    public class EventPanelView : ViewBase<EventPanelViewModel>
    {
        [Header("标签页")]
        [SerializeField] private Transform _tabContainer;
        [SerializeField] private GameObject _tabButtonPrefab;
        [SerializeField] private Image _tabSelector;
        private readonly List<TabButton> _tabButtons = new List<TabButton>();

        [Header("活动列表")]
        [SerializeField] private Transform _eventListContainer;
        [SerializeField] private GameObject _eventCardPrefab;
        private readonly List<EventCard> _eventCards = new List<EventCard>();

        [Header("详情面板")]
        [SerializeField] private GameObject _detailPanel;
        [SerializeField] private Image _detailBanner;
        [SerializeField] private TMP_Text _detailTitle;
        [SerializeField] private TMP_Text _detailDescription;
        [SerializeField] private TMP_Text _detailTimeRemaining;
        [SerializeField] private Slider _detailProgressBar;
        [SerializeField] private TMP_Text _detailProgressText;
        [SerializeField] private Transform _detailRewardsContainer;
        [SerializeField] private GameObject _rewardIconPrefab;
        [SerializeField] private Button _detailActionButton;
        [SerializeField] private TMP_Text _detailActionButtonText;
        [SerializeField] private Button _detailCloseButton;

        [Header("空状态")]
        [SerializeField] private GameObject _emptyState;
        [SerializeField] private TMP_Text _emptyStateText;

        [Header("刷新")]
        [SerializeField] private Button _refreshButton;
        [SerializeField] private Image _refreshIcon;

        private EventPanelViewModel _viewModel;
        private int _selectedTabIndex = 0;
        private EventCard _selectedCard;
        private bool _isDetailVisible = false;

        protected override void Awake()
        {
            base.Awake();
            _detailPanel.SetActive(false);
            _emptyState.SetActive(false);
        }

        protected override void OnBind()
        {
            _viewModel = ViewModel;

            _refreshButton.onClick.AddListener(() =>
            {
                _refreshIcon.transform.DORotate(new Vector3(0, 0, 360), 0.5f, RotateMode.FastBeyond360)
                    .SetEase(Ease.OutCubic);
                _viewModel.RefreshCommand.Execute();
            });

            _detailCloseButton.onClick.AddListener(() => HideDetailPanel());
            _detailActionButton.onClick.AddListener(() => _viewModel.ActionCommand.Execute());

            BindDisposables.Add(_viewModel.AvailableTabs.Subscribe(OnTabsChanged));
            BindDisposables.Add(_viewModel.Events.Subscribe(OnEventsChanged));
            BindDisposables.Add(_viewModel.SelectedEvent.Subscribe(OnSelectedEventChanged));
            BindDisposables.Add(_viewModel.IsRefreshing.Subscribe(OnRefreshingChanged));
        }

        private void OnTabsChanged(List<EventTab> tabs)
        {
            // 清理旧标签
            foreach (var btn in _tabButtons)
            {
                Destroy(btn.gameObject);
            }
            _tabButtons.Clear();

            // 创建新标签
            for (int i = 0; i < tabs.Count; i++)
            {
                var tab = tabs[i];
                var btnObj = Instantiate(_tabButtonPrefab, _tabContainer);
                var btn = btnObj.GetComponent<TabButton>();
                btn.Initialize(tab, i, OnTabSelected);
                _tabButtons.Add(btn);
            }

            // 默认选中第一个
            if (_tabButtons.Count > 0)
            {
                SelectTab(0);
            }
        }

        private void OnTabSelected(int index)
        {
            SelectTab(index);
            _viewModel.SelectTabCommand.Execute(index);
        }

        private void SelectTab(int index)
        {
            _selectedTabIndex = index;

            // 更新选择器位置
            if (index < _tabButtons.Count && _tabSelector != null)
            {
                var targetRect = _tabButtons[index].GetComponent<RectTransform>();
                _tabSelector.transform.DOMoveX(targetRect.position.x, 0.3f).SetEase(Ease.OutCubic);
                _tabSelector.rectTransform.DOSizeDelta(new Vector2(targetRect.rect.width, _tabSelector.rectTransform.rect.height), 0.3f).SetEase(Ease.OutCubic);
            }

            // 更新按钮状态
            for (int i = 0; i < _tabButtons.Count; i++)
            {
                _tabButtons[i].SetSelected(i == index);
            }
        }

        private void OnEventsChanged(List<EventViewData> events)
        {
            // 回收多余卡片
            while (_eventCards.Count > events.Count)
            {
                var last = _eventCards[_eventCards.Count - 1];
                _eventCards.RemoveAt(_eventCards.Count - 1);
                last.Recycle();
            }

            // 更新或创建卡片
            for (int i = 0; i < events.Count; i++)
            {
                EventCard card;
                if (i < _eventCards.Count)
                {
                    card = _eventCards[i];
                }
                else
                {
                    var obj = Instantiate(_eventCardPrefab, _eventListContainer);
                    card = obj.GetComponent<EventCard>();
                    _eventCards.Add(card);
                }

                card.SetData(events[i], () => OnCardClicked(card, events[i]));
                card.gameObject.SetActive(true);

                // 入场动画延迟
                card.transform.localScale = Vector3.zero;
                card.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetDelay(i * 0.05f);
            }

            // 隐藏多余
            for (int i = events.Count; i < _eventCards.Count; i++)
            {
                _eventCards[i].gameObject.SetActive(false);
            }

            _emptyState.SetActive(events.Count == 0);
        }

        private void OnCardClicked(EventCard card, EventViewData data)
        {
            // 取消之前选中的高亮
            if (_selectedCard != null)
            {
                _selectedCard.SetHighlighted(false);
            }

            _selectedCard = card;
            card.SetHighlighted(true);
            _viewModel.SelectEventCommand.Execute(data);
        }

        private void OnSelectedEventChanged(EventViewData data)
        {
            if (data == null)
            {
                HideDetailPanel();
                return;
            }

            ShowDetailPanel(data);
        }

        private void ShowDetailPanel(EventViewData data)
        {
            if (!_isDetailVisible)
            {
                _detailPanel.SetActive(true);
                _detailPanel.transform.localScale = Vector3.zero;
                _detailPanel.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);
                _isDetailVisible = true;
            }

            // 设置内容
            _detailBanner.sprite = data.bannerSprite;
            _detailTitle.text = data.title;
            _detailDescription.text = data.description;
            _detailTimeRemaining.text = FormatTimeRemaining(data.timeRemainingSeconds);
            _detailProgressBar.value = data.progress;
            _detailProgressText.text = $"{data.currentProgress}/{data.maxProgress}";

            // 奖励图标
            foreach (Transform child in _detailRewardsContainer)
            {
                Destroy(child.gameObject);
            }

            foreach (var reward in data.rewards)
            {
                var iconObj = Instantiate(_rewardIconPrefab, _detailRewardsContainer);
                var icon = iconObj.GetComponent<Image>();
                var qtyText = iconObj.GetComponentInChildren<TMP_Text>();
                if (icon != null) icon.sprite = reward.icon;
                if (qtyText != null && reward.quantity > 1) qtyText.text = $"x{reward.quantity}";
            }

            // 按钮状态
            _detailActionButton.interactable = data.canClaim;
            _detailActionButtonText.text = data.canClaim ? "领取奖励" : (data.isCompleted ? "已完成" : "前往参与");

            // 启动倒计时
            StartCountdown(data);
        }

        private void HideDetailPanel()
        {
            if (!_isDetailVisible) return;

            _detailPanel.transform.DOScale(0f, 0.2f).SetEase(Ease.InBack)
                .OnComplete(() =>
                {
                    _detailPanel.SetActive(false);
                    _isDetailVisible = false;
                });

            if (_selectedCard != null)
            {
                _selectedCard.SetHighlighted(false);
                _selectedCard = null;
            }
        }

        private void StartCountdown(EventViewData data)
        {
            // 取消之前的倒计时
            DOTween.Kill(this, true);

            DOVirtual.Float(data.timeRemainingSeconds, 0f, data.timeRemainingSeconds, t =>
            {
                _detailTimeRemaining.text = FormatTimeRemaining((int)t);
            }).SetTarget(this).SetUpdate(true);
        }

        private string FormatTimeRemaining(int seconds)
        {
            if (seconds <= 0) return "已结束";

            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalDays >= 1)
                return $"{ts.Days}天 {ts.Hours}小时";
            if (ts.TotalHours >= 1)
                return $"{ts.Hours}小时 {ts.Minutes}分";
            return $"{ts.Minutes}分 {ts.Seconds}秒";
        }

        private void OnRefreshingChanged(bool isRefreshing)
        {
            _refreshButton.interactable = !isRefreshing;
            if (!isRefreshing)
            {
                DOTween.Kill(_refreshIcon.transform);
            }
        }

        protected override void OnDispose()
        {
            DOTween.Kill(this);
            base.OnDispose();
        }
    }

    // ===================== Tab Button =====================

    public class TabButton : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;
        [SerializeField] private Image _icon;
        [SerializeField] private Image _background;
        [SerializeField] private GameObject _notificationBadge;
        [SerializeField] private TMP_Text _badgeCount;

        private EventTab _tabData;
        private int _index;
        private Action<int> _onClick;

        public void Initialize(EventTab tab, int index, Action<int> onClick)
        {
            _tabData = tab;
            _index = index;
            _onClick = onClick;

            _label.text = tab.title;
            if (_icon != null && tab.icon != null) _icon.sprite = tab.icon;
            if (_notificationBadge != null) _notificationBadge.SetActive(tab.hasNotification);
            if (_badgeCount != null && tab.notificationCount > 0) _badgeCount.text = tab.notificationCount.ToString();

            var btn = GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(() => _onClick?.Invoke(_index));
        }

        public void SetSelected(bool selected)
        {
            _background.color = selected ? new Color(1, 1, 1, 0.2f) : Color.clear;
            _label.fontWeight = selected ? FontWeight.Bold : FontWeight.Normal;
        }
    }

    [Serializable]
    public class EventTab
    {
        public string id;
        public string title;
        public Sprite icon;
        public bool hasNotification;
        public int notificationCount;
    }

    // ===================== Event Card =====================

    public class EventCard : MonoBehaviour
    {
        [SerializeField] private Image _banner;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _timeRemaining;
        [SerializeField] private Slider _progressBar;
        [SerializeField] private TMP_Text _progressText;
        [SerializeField] private Image _rewardIcon;
        [SerializeField] private GameObject _completedBadge;
        [SerializeField] private GameObject _newBadge;
        [SerializeField] private Image _highlightFrame;

        private EventViewData _data;
        private Action<EventCard, EventViewData> _onClick;

        public void SetData(EventViewData data, Action<EventCard, EventViewData> onClick)
        {
            _data = data;
            _onClick = onClick;

            _banner.sprite = data.bannerSprite;
            _title.text = data.title;
            _timeRemaining.text = FormatTime(data.timeRemainingSeconds);
            _progressBar.value = data.progress;
            _progressText.text = $"{data.currentProgress}/{data.maxProgress}";

            if (data.rewards.Count > 0 && _rewardIcon != null)
            {
                _rewardIcon.sprite = data.rewards[0].icon;
            }

            _completedBadge?.SetActive(data.isCompleted);
            _newBadge?.SetActive(data.isNew);
            _highlightFrame.enabled = false;

            var btn = GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => _onClick?.Invoke(this, _data));
            }
        }

        public void SetHighlighted(bool highlighted)
        {
            _highlightFrame.enabled = highlighted;
            if (highlighted)
            {
                _highlightFrame.DOFade(1f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
            }
            else
            {
                DOTween.Kill(_highlightFrame);
                _highlightFrame.color = new Color(_highlightFrame.color.r, _highlightFrame.color.g, _highlightFrame.color.b, 0f);
            }
        }

        public void Recycle()
        {
            DOTween.Kill(this);
            _highlightFrame.enabled = false;
        }

        private string FormatTime(int seconds)
        {
            if (seconds <= 0) return "已结束";
            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalDays >= 1) return $"{ts.Days}d";
            if (ts.TotalHours >= 1) return $"{ts.Hours}h";
            return $"{ts.Minutes}m";
        }
    }

    // ===================== ViewModel =====================

    public class EventPanelViewModel : ViewModelBase
    {
        public ReactiveProperty<List<EventTab>> AvailableTabs { get; } = new ReactiveProperty<List<EventTab>>(new List<EventTab>());
        public ReactiveProperty<List<EventViewData>> Events { get; } = new ReactiveProperty<List<EventViewData>>(new List<EventViewData>());
        public ReactiveProperty<EventViewData> SelectedEvent { get; } = new ReactiveProperty<EventViewData>();
        public ReactiveProperty<bool> IsRefreshing { get; } = new ReactiveProperty<bool>(false);

        public ReactiveCommand<int> SelectTabCommand { get; }
        public ReactiveCommand<EventViewData> SelectEventCommand { get; }
        public ReactiveCommand RefreshCommand { get; }
        public ReactiveCommand ActionCommand { get; }

        public EventPanelViewModel()
        {
            SelectTabCommand = CreateCommand<int>(OnSelectTab);
            SelectEventCommand = CreateCommand<EventViewData>(OnSelectEvent);
            RefreshCommand = CreateCommand(OnRefresh);
            ActionCommand = CreateCommand(OnAction);
        }

        public void SetTabs(List<EventTab> tabs) => AvailableTabs.Value = tabs;
        public void SetEvents(List<EventViewData> events) => Events.Value = events;
        public void SelectEvent(EventViewData data) => SelectedEvent.Value = data;

        private async UniTask OnSelectTab(int index)
        {
            // 根据标签筛选活动
            await UniTask.Yield();
        }

        private async UniTask OnSelectEvent(EventViewData data)
        {
            SelectedEvent.Value = data;
        }

        private async UniTask OnRefresh()
        {
            IsRefreshing.Value = true;
            await UniTask.Delay(1000); // 模拟网络请求
            IsRefreshing.Value = false;
        }

        private async UniTask OnAction()
        {
            await UniTask.Yield();
        }
    }

    // ===================== 数据结构 =====================

    [Serializable]
    public class EventViewData
    {
        public string id;
        public string title;
        public string description;
        public Sprite bannerSprite;
        public float progress; // 0-1
        public int currentProgress;
        public int maxProgress;
        public int timeRemainingSeconds;
        public bool isCompleted;
        public bool canClaim;
        public bool isNew;
        public List<EventRewardViewData> rewards = new List<EventRewardViewData>();
    }

    [Serializable]
    public class EventRewardViewData
    {
        public Sprite icon;
        public int quantity;
        public RewardRarity rarity;
    }

    public enum RewardRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4,
        Mythic = 5
    }
}