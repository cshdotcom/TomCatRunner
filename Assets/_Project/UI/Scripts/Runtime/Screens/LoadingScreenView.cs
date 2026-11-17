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
    /// 加载界面 - 进度条、提示轮播、主题过渡动画、资源预加载
    /// </summary>
    public class LoadingScreenView : ViewBase<LoadingScreenViewModel>
    {
        [Header("主面板")]
        [SerializeField] private CanvasGroup _mainCanvasGroup;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private RawImage _backgroundVideo; // 可选视频背景

        [Header("进度条")]
        [SerializeField] private Slider _progressBar;
        [SerializeField] private Image _progressFill;
        [SerializeField] private Image _progressGlow;
        [SerializeField] private TMP_Text _progressText;
        [SerializeField] private TMP_Text _progressPercentText;
        [SerializeField] private RectTransform _progressHandle;

        [Header("提示轮播")]
        [SerializeField] private TMP_Text _tipText;
        [SerializeField] private Image _tipIcon;
        [SerializeField] private Button _tipPrevButton;
        [SerializeField] private Button _tipNextButton;
        [SerializeField] private RectTransform _tipContainer;

        [Header("主题过渡")]
        [SerializeField] private Image _transitionOverlay;
        [SerializeField] private float _transitionDuration = 1.5f;
        [SerializeField] private AnimationCurve _transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("版本/状态")]
        [SerializeField] private TMP_Text _versionText;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private GameObject _offlineIndicator;

        [Header("角色展示")]
        [SerializeField] private RawImage _characterRenderTexture;
        [SerializeField] private Animation _characterAnimation;

        private LoadingScreenViewModel _viewModel;
        private int _currentTipIndex = 0;
        private float _tipAutoRotateTimer = 0f;
        private Sequence _progressSequence;
        private Sequence _transitionSequence;
        private Material _transitionMaterial;

        protected override void Awake()
        {
            base.Awake();

            _mainCanvasGroup.alpha = 0f;
            _progressBar.value = 0f;
            _transitionOverlay.color = new Color(0, 0, 0, 0);

            // 创建过渡材质（用于 Shader 过渡效果）
            if (_transitionOverlay.material == null || _transitionOverlay.material.name.Contains("Default"))
            {
                _transitionMaterial = new Material(Shader.Find("UI/Transition/Dissolve"));
                _transitionOverlay.material = _transitionMaterial;
            }
            else
            {
                _transitionMaterial = _transitionOverlay.material;
            }
        }

        protected override void OnBind()
        {
            _viewModel = ViewModel;

            _tipPrevButton.onClick.AddListener(() => RotateTip(-1));
            _tipNextButton.onClick.AddListener(() => RotateTip(1));

            BindDisposables.Add(_viewModel.Progress.Subscribe(OnProgressChanged));
            BindDisposables.Add(_viewModel.StatusText.Subscribe(OnStatusChanged));
            BindDisposables.Add(_viewModel.Tips.Subscribe(OnTipsChanged));
            BindDisposables.Add(_viewModel.CurrentTheme.Subscribe(OnThemeChanged));
            BindDisposables.Add(_viewModel.IsTransitioning.Subscribe(OnTransitioningChanged));
            BindDisposables.Add(_viewModel.VersionText.Subscribe(v => _versionText.text = v));
            BindDisposables.Add(_viewModel.IsOfflineMode.Subscribe(OnOfflineModeChanged));
        }

        private void OnProgressChanged(float progress)
        {
            _progressBar.DOValue(progress, 0.3f).SetEase(Ease.OutCubic);
            _progressPercentText.text = $"{progress * 100:F0}%";

            // 进度条光晕跟随
            if (_progressHandle != null)
            {
                _progressHandle.anchoredPosition = new Vector2(progress * _progressBar.GetComponent<RectTransform>().rect.width, 0);
            }

            // 脉冲效果
            if (progress >= 1f)
            {
                _progressGlow.DOFade(1f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
            }
            else
            {
                DOTween.Kill(_progressGlow);
                _progressGlow.DOFade(0f, 0.3f);
            }
        }

        private void OnStatusChanged(string status)
        {
            _statusText.text = status;
            // 打字机效果
            AnimateTypewriter(_statusText, status);
        }

        private void OnTipsChanged(List<string> tips)
        {
            if (tips == null || tips.Count == 0) return;

            _currentTipIndex = 0;
            ShowTip(tips[0]);
        }

        private void ShowTip(string tip)
        {
            _tipText.DOFade(0f, 0.2f).OnComplete(() =>
            {
                _tipText.text = tip;
                _tipText.DOFade(1f, 0.3f);
            });
        }

        private void RotateTip(int direction)
        {
            var tips = _viewModel.Tips.Value;
            if (tips == null || tips.Count == 0) return;

            _currentTipIndex = (_currentTipIndex + direction + tips.Count) % tips.Count;
            ShowTip(tips[_currentTipIndex]);
        }

        private void OnThemeChanged(ThemeProfile theme)
        {
            if (theme == null) return;

            // 背景图切换
            if (_backgroundImage != null && theme.loadingBackground != null)
            {
                _backgroundImage.DOFade(0f, 0.3f).OnComplete(() =>
                {
                    _backgroundImage.sprite = theme.loadingBackground;
                    _backgroundImage.DOFade(1f, 0.5f);
                });
            }

            // 进度条颜色
            if (_progressFill != null)
            {
                _progressFill.color = theme.primaryColor;
            }
            if (_progressGlow != null)
            {
                _progressGlow.color = theme.accentColor;
            }

            // 提示图标
            if (_tipIcon != null && theme.tipIcon != null)
            {
                _tipIcon.sprite = theme.tipIcon;
            }
        }

        private void OnTransitioningChanged(bool isTransitioning)
        {
            if (isTransitioning)
            {
                PlayThemeTransition();
            }
        }

        private void OnOfflineModeChanged(bool isOffline)
        {
            _offlineIndicator.SetActive(isOffline);
        }

        private void AnimateTypewriter(TMP_Text textMesh, string targetText)
        {
            textMesh.maxVisibleCharacters = 0;
            DOTween.To(() => textMesh.maxVisibleCharacters,
                x => textMesh.maxVisibleCharacters = x,
                targetText.Length, 0.5f)
                .SetEase(Ease.Linear);
        }

        private async void PlayThemeTransition()
        {
            if (_transitionMaterial == null) return;

            _transitionSequence?.Kill();
            _transitionSequence = DOTween.Sequence();

            // 溶解进入
            _transitionMaterial.SetFloat("_Cutoff", 0f);
            _transitionOverlay.color = new Color(0, 0, 0, 1f);

            _transitionSequence.Append(DOTween.To(() => _transitionMaterial.GetFloat("_Cutoff"),
                x => _transitionMaterial.SetFloat("_Cutoff", x), 1f, _transitionDuration)
                .SetEase(_transitionCurve));

            await _transitionSequence.AsyncWaitForCompletion();

            // 通知完成
            _viewModel.OnTransitionComplete();
        }

        protected override async UniTask PlayEnterAnimationAsync()
        {
            _mainCanvasGroup.interactable = true;
            _mainCanvasGroup.blocksRaycasts = true;

            var seq = DOTween.Sequence();
            seq.Join(_mainCanvasGroup.DOFade(1f, 0.5f));
            seq.Join(_progressBar.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack).From(0f));
            await seq.AsyncWaitForCompletion();

            // 启动提示自动轮播
            StartTipAutoRotate();
        }

        protected override async UniTask PlayExitAnimationAsync()
        {
            _mainCanvasGroup.interactable = false;
            _mainCanvasGroup.blocksRaycasts = false;

            var seq = DOTween.Sequence();
            seq.Join(_mainCanvasGroup.DOFade(0f, 0.3f));
            await seq.AsyncWaitForCompletion();
        }

        private void StartTipAutoRotate()
        {
            DOTween.Kill(this, "TipAutoRotate");
            DOVirtual.DelayedCall(5f, () =>
            {
                RotateTip(1);
                StartTipAutoRotate();
            }).SetId(this, "TipAutoRotate").SetUpdate(true);
        }

        protected override void OnDispose()
        {
            _progressSequence?.Kill();
            _transitionSequence?.Kill();
            DOTween.Kill(this);
            if (_transitionMaterial != null)
            {
                Destroy(_transitionMaterial);
            }
            base.OnDispose();
        }

        private void Update()
        {
            // 角色呼吸动画
            if (_characterRenderTexture != null)
            {
                float breath = Mathf.Sin(Time.time * 2f) * 0.02f + 1f;
                _characterRenderTexture.transform.localScale = Vector3.one * breath;
            }
        }
    }

    // ===================== ViewModel =====================

    public class LoadingScreenViewModel : ViewModelBase
    {
        public ReactiveProperty<float> Progress { get; } = new ReactiveProperty<float>(0f);
        public ReactiveProperty<string> StatusText { get; } = new ReactiveProperty<string>("初始化中...");
        public ReactiveProperty<List<string>> Tips { get; } = new ReactiveProperty<List<string>>(new List<string>());
        public ReactiveProperty<ThemeProfile> CurrentTheme { get; } = new ReactiveProperty<ThemeProfile>();
        public ReactiveProperty<bool> IsTransitioning { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<string> VersionText { get; } = new ReactiveProperty<string>("v1.0.0");
        public ReactiveProperty<bool> IsOfflineMode { get; } = new ReactiveProperty<bool>(false);

        private Action _onTransitionComplete;

        public void SetProgress(float progress) => Progress.Value = Mathf.Clamp01(progress);
        public void SetStatus(string status) => StatusText.Value = status;
        public void SetTips(List<string> tips) => Tips.Value = tips;
        public void SetTheme(ThemeProfile theme) => CurrentTheme.Value = theme;
        public void SetVersion(string version) => VersionText.Value = version;
        public void SetOfflineMode(bool offline) => IsOfflineMode.Value = offline;

        public void TriggerThemeTransition(Action onComplete)
        {
            _onTransitionComplete = onComplete;
            IsTransitioning.Value = true;
        }

        public void OnTransitionComplete()
        {
            IsTransitioning.Value = false;
            _onTransitionComplete?.Invoke();
            _onTransitionComplete = null;
        }
    }
}