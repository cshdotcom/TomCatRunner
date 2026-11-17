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
    /// 奖励弹窗 - 稀有度展示、粒子特效、连续领取队列
    /// </summary>
    public class RewardPopupView : ViewBase<RewardPopupViewModel>
    {
        [Header("主面板")]
        [SerializeField] private RectTransform _panelRect;
        [SerializeField] private CanvasGroup _panelCanvasGroup;
        [SerializeField] private Image _panelBackground;
        [SerializeField] private Image _rarityGlow;

        [Header("奖励图标")]
        [SerializeField] private Image _rewardIcon;
        [SerializeField] private Image _rewardIconGlow;
        [SerializeField] private ParticleSystem _rewardParticles;
        [SerializeField] private ParticleSystem _burstParticles;

        [Header("文本")]
        [SerializeField] private TMP_Text _rewardNameText;
        [SerializeField] private TMP_Text _rewardDescriptionText;
        [SerializeField] private TMP_Text _quantityText;
        [SerializeField] private TMP_Text _rarityLabelText;

        [Header("按钮")]
        [SerializeField] private Button _claimButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TMP_Text _claimButtonText;

        [Header("队列指示器")]
        [SerializeField] private GameObject _queueIndicatorPrefab;
        [SerializeField] private Transform _queueContainer;
        [SerializeField] private int _maxQueueVisible = 5;

        [Header("稀有度配色")]
        [SerializeField] private RewardRarityColors _rarityColors;

        private RewardPopupViewModel _viewModel;
        private readonly Queue<RewardData> _rewardQueue = new Queue<RewardData>();
        private bool _isShowing = false;
        private Sequence _currentSequence;
        private readonly List<GameObject> _queueIndicators = new List<GameObject>();

        protected override void Awake()
        {
            base.Awake();
            _panelCanvasGroup.alpha = 0f;
            _panelCanvasGroup.interactable = false;
            _panelCanvasGroup.blocksRaycasts = false;
            _panelRect.localScale = Vector3.zero;
            gameObject.SetActive(false);
        }

        protected override void OnBind()
        {
            _viewModel = ViewModel;

            _claimButton.onClick.AddListener(() => _viewModel.ClaimCommand.Execute());
            _closeButton.onClick.AddListener(() => _viewModel.CloseCommand.Execute());

            BindDisposables.Add(_viewModel.ShowReward.Subscribe(OnShowReward));
            BindDisposables.Add(_viewModel.QueueChanged.Subscribe(OnQueueChanged));
            BindDisposables.Add(_viewModel.IsClaiming.Subscribe(OnClaimingChanged));
        }

        private void OnShowReward(RewardData reward)
        {
            if (reward == null) return;

            if (_isShowing)
            {
                _rewardQueue.Enqueue(reward);
                UpdateQueueIndicators();
                return;
            }

            ShowRewardInternal(reward);
        }

        private async void ShowRewardInternal(RewardData reward)
        {
            _isShowing = true;
            gameObject.SetActive(true);

            // 应用稀有度样式
            ApplyRarityStyle(reward.rarity);

            // 设置内容
            _rewardIcon.sprite = reward.icon;
            _rewardNameText.text = reward.name;
            _rewardDescriptionText.text = reward.description;
            _quantityText.text = reward.quantity > 1 ? $"x{reward.quantity}" : "";
            _rarityLabelText.text = GetRarityLabel(reward.rarity);

            // 入场动画
            await PlayEnterAnimationAsync(reward.rarity);

            // 等待点击领取
            await WaitForClaimAsync();

            // 退场动画
            await PlayExitAnimationAsync();

            _isShowing = false;

            // 处理队列中下一个
            if (_rewardQueue.Count > 0)
            {
                var next = _rewardQueue.Dequeue();
                UpdateQueueIndicators();
                ShowRewardInternal(next);
            }
            else
            {
                gameObject.SetActive(false);
                _viewModel.OnRewardSequenceComplete();
            }
        }

        private void ApplyRarityStyle(RewardRarity rarity)
        {
            var colors = _rarityColors.GetColors(rarity);

            // 背景渐变
            if (_panelBackground != null)
            {
                _panelBackground.color = colors.panelBackground;
            }

            // 稀有度光晕
            if (_rarityGlow != null)
            {
                _rarityGlow.color = colors.glowColor;
                _rarityGlow.DOFade(1f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
            }

            // 图标光晕
            if (_rewardIconGlow != null)
            {
                _rewardIconGlow.color = colors.glowColor;
                _rewardIconGlow.transform.DORotate(new Vector3(0, 0, 360), 10f, RotateMode.FastBeyond360)
                    .SetLoops(-1, LoopType.Restart).SetEase(Ease.Linear);
            }

            // 文本颜色
            _rewardNameText.color = colors.nameColor;
            _rarityLabelText.color = colors.nameColor;

            // 粒子颜色
            if (_rewardParticles != null)
            {
                var main = _rewardParticles.main;
                main.startColor = new ParticleSystem.MinMaxGradient(colors.particleColorStart, colors.particleColorEnd);
            }
            if (_burstParticles != null)
            {
                var main = _burstParticles.main;
                main.startColor = new ParticleSystem.MinMaxGradient(colors.particleColorStart, colors.particleColorEnd);
            }

            // 领取按钮
            var claimColors = _claimButton.colors;
            claimColors.normalColor = colors.buttonColor;
            claimColors.highlightedColor = colors.buttonColor * 1.2f;
            claimColors.pressedColor = colors.buttonColor * 0.8f;
            _claimButton.colors = claimColors;
        }

        private string GetRarityLabel(RewardRarity rarity)
        {
            return rarity switch
            {
                RewardRarity.Common => "普通",
                RewardRarity.Uncommon => "优秀",
                RewardRarity.Rare => "稀有",
                RewardRarity.Epic => "史诗",
                RewardRarity.Legendary => "传说",
                RewardRarity.Mythic => "神话",
                _ => ""
            };
        }

        protected override async UniTask PlayEnterAnimationAsync()
        {
            _panelCanvasGroup.interactable = true;
            _panelCanvasGroup.blocksRaycasts = true;

            _currentSequence?.Kill();
            _currentSequence = DOTween.Sequence();

            // 缩放 + 淡入 + 弹性
            _panelRect.localScale = Vector3.zero;
            _panelCanvasGroup.alpha = 0f;

            _currentSequence.Append(_panelRect.DOScale(1f, 0.5f).SetEase(Ease.OutElastic, 1.2f));
            _currentSequence.Join(_panelCanvasGroup.DOFade(1f, 0.3f));

            // 图标旋入
            _rewardIcon.transform.localRotation = Quaternion.Euler(0, 0, -180f);
            _currentSequence.Join(_rewardIcon.transform.DORotate(Vector3.zero, 0.6f, RotateMode.FastBeyond360)
                .SetEase(Ease.OutBack).SetDelay(0.1f));

            // 粒子爆发
            _currentSequence.AppendCallback(() =>
            {
                _rewardParticles?.Play();
                _burstParticles?.Play();
            });

            // 文本逐字显示
            _currentSequence.AppendCallback(() => AnimateTextReveal());

            await _currentSequence.AsyncWaitForCompletion();
        }

        private void AnimateTextReveal()
        {
            // 名称逐字显示
            _rewardNameText.maxVisibleCharacters = 0;
            DOTween.To(() => _rewardNameText.maxVisibleCharacters,
                x => _rewardNameText.maxVisibleCharacters = x,
                _rewardNameText.textInfo.characterCount, 0.5f)
                .SetEase(Ease.OutQuad);

            // 描述延迟显示
            _rewardDescriptionText.alpha = 0f;
            _rewardDescriptionText.DOFade(1f, 0.4f).SetDelay(0.3f);
        }

        private async UniTask WaitForClaimAsync()
        {
            var tcs = new UniTaskCompletionSource<bool>();
            IDisposable sub = null;
            sub = _viewModel.ClaimCommand.Executed.Subscribe(_ =>
            {
                sub?.Dispose();
                tcs.TrySetResult(true);
            });
            await tcs.Task;
        }

        protected override async UniTask PlayExitAnimationAsync()
        {
            _panelCanvasGroup.interactable = false;
            _panelCanvasGroup.blocksRaycasts = false;

            _currentSequence?.Kill();
            _currentSequence = DOTween.Sequence();

            _currentSequence.Append(_panelRect.DOScale(0.8f, 0.2f).SetEase(Ease.InBack));
            _currentSequence.Join(_panelCanvasGroup.DOFade(0f, 0.2f));
            _currentSequence.Join(_rewardIcon.transform.DORotate(new Vector3(0, 0, 180f), 0.2f, RotateMode.FastBeyond360)
                .SetEase(Ease.InBack));

            await _currentSequence.AsyncWaitForCompletion();
        }

        private void OnQueueChanged(int count)
        {
            UpdateQueueIndicators();
        }

        private void UpdateQueueIndicators()
        {
            // 清理多余
            while (_queueIndicators.Count > _rewardQueue.Count)
            {
                var last = _queueIndicators[_queueIndicators.Count - 1];
                _queueIndicators.RemoveAt(_queueIndicators.Count - 1);
                Destroy(last);
            }

            // 更新或创建
            int index = 0;
            foreach (var reward in _rewardQueue)
            {
                GameObject indicator;
                if (index < _queueIndicators.Count)
                {
                    indicator = _queueIndicators[index];
                }
                else
                {
                    indicator = Instantiate(_queueIndicatorPrefab, _queueContainer);
                    _queueIndicators.Add(indicator);
                }

                var icon = indicator.GetComponentInChildren<Image>();
                var rarityFrame = indicator.transform.Find("RarityFrame")?.GetComponent<Image>();

                if (icon != null) icon.sprite = reward.icon;
                if (rarityFrame != null)
                {
                    var colors = _rarityColors.GetColors(reward.rarity);
                    rarityFrame.color = colors.glowColor;
                }

                indicator.SetActive(true);
                index++;
            }

            // 隐藏多余
            for (int i = index; i < _queueIndicators.Count; i++)
            {
                _queueIndicators[i].SetActive(false);
            }
        }

        private void OnClaimingChanged(bool isClaiming)
        {
            _claimButton.interactable = !isClaiming;
            _claimButtonText.text = isClaiming ? "领取中..." : "领取";
        }

        protected override void OnDispose()
        {
            _currentSequence?.Kill();
            _rewardParticles?.Stop();
            _burstParticles?.Stop();
            base.OnDispose();
        }
    }

    // ===================== ViewModel =====================

    public class RewardPopupViewModel : ViewModelBase
    {
        public ReactiveProperty<RewardData> ShowReward { get; } = new ReactiveProperty<RewardData>();
        public ReactiveProperty<int> QueueChanged { get; } = new ReactiveProperty<int>(0);
        public ReactiveProperty<bool> IsClaiming { get; } = new ReactiveProperty<bool>(false);

        public ReactiveCommand ClaimCommand { get; }
        public ReactiveCommand CloseCommand { get; }

        private readonly Queue<RewardData> _queue = new Queue<RewardData>();
        private Action _onSequenceComplete;

        public RewardPopupViewModel()
        {
            ClaimCommand = CreateCommand(OnClaim);
            CloseCommand = CreateCommand(OnClose);
        }

        public void EnqueueReward(RewardData reward)
        {
            _queue.Enqueue(reward);
            QueueChanged.Value = _queue.Count;

            if (_queue.Count == 1)
            {
                ShowReward.Value = reward;
            }
        }

        public void EnqueueRewards(IEnumerable<RewardData> rewards)
        {
            foreach (var r in rewards) _queue.Enqueue(r);
            QueueChanged.Value = _queue.Count;

            if (_queue.Count == rewards.Count()) // First batch
            {
                ShowReward.Value = _queue.Dequeue();
            }
        }

        public void SetCompletionCallback(Action callback) => _onSequenceComplete = callback;
        public void OnRewardSequenceComplete() => _onSequenceComplete?.Invoke();

        private async UniTask OnClaim()
        {
            IsClaiming.Value = true;
            await UniTask.Delay(500); // 模拟领取动画
            IsClaiming.Value = false;
        }

        private async UniTask OnClose()
        {
            // 跳过当前，直接关闭
            await UniTask.Yield();
        }
    }

    // ===================== 数据结构 =====================

    [Serializable]
    public class RewardData
    {
        public string id;
        public string name;
        public string description;
        public Sprite icon;
        public int quantity;
        public RewardRarity rarity;
        public RewardType type;
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

    public enum RewardType
    {
        Currency,
        Character,
        Skin,
        Powerup,
        BattlePassXP,
        SeasonToken,
        Custom
    }

    [Serializable]
    public class RewardRarityColors
    {
        public RarityColorSet common;
        public RarityColorSet uncommon;
        public RarityColorSet rare;
        public RarityColorSet epic;
        public RarityColorSet legendary;
        public RarityColorSet mythic;

        public RarityColorSet GetColors(RewardRarity rarity)
        {
            return rarity switch
            {
                RewardRarity.Common => common,
                RewardRarity.Uncommon => uncommon,
                RewardRarity.Rare => rare,
                RewardRarity.Epic => epic,
                RewardRarity.Legendary => legendary,
                RewardRarity.Mythic => mythic,
                _ => common
            };
        }
    }

    [Serializable]
    public class RarityColorSet
    {
        public Color panelBackground = Color.white;
        public Color glowColor = Color.white;
        public Color nameColor = Color.white;
        public Color particleColorStart = Color.white;
        public Color particleColorEnd = Color.white;
        public Color buttonColor = Color.white;
    }
}