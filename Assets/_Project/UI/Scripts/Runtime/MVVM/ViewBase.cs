using System;
using UnityEngine;
using UniRx;
using Cysharp.Threading.Tasks;
using DG.Tweening;

namespace TomCatRunner.UI
{
    /// <summary>
    /// View 基类 - 负责绑定 ViewModel、动画、生命周期
    /// </summary>
    public abstract class ViewBase<TViewModel> : MonoBehaviour where TViewModel : ViewModelBase
    {
        [Header("动画设置")]
        [SerializeField] private float _enterAnimationDuration = 0.3f;
        [SerializeField] private float _exitAnimationDuration = 0.2f;
        [SerializeField] private DG.Tweening.Ease _enterEase = DG.Tweening.Ease.OutBack;
        [SerializeField] private DG.Tweening.Ease _exitEase = DG.Tweening.Ease.InQuad;

        protected TViewModel ViewModel { get; private set; }
        protected CompositeDisposable BindDisposables { get; } = new CompositeDisposable();
        private CanvasGroup _canvasGroup;
        private RectTransform _rectTransform;
        private bool _isVisible = false;

        public bool IsVisible => _isVisible;
        public RectTransform RectTransform => _rectTransform;

        protected virtual void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            _rectTransform = GetComponent<RectTransform>();

            // 初始隐藏
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        /// <summary>
        /// 绑定 ViewModel
        /// </summary>
        public virtual async UniTask BindAsync(TViewModel viewModel)
        {
            ViewModel = viewModel;

            if (!ViewModel.IsInitialized)
            {
                await ViewModel.InitializeAsync();
            }

            OnBind();
            BindProperties();
            BindCommands();
            BindEvents();

            _isVisible = true;
            await PlayEnterAnimationAsync();
        }

        protected virtual void OnBind() { }

        protected virtual void BindProperties() { }
        protected virtual void BindCommands() { }
        protected virtual void BindEvents() { }

        /// <summary>
        /// 解绑并隐藏
        /// </summary>
        public virtual async UniTask UnbindAsync()
        {
            if (!_isVisible) return;

            await PlayExitAnimationAsync();

            BindDisposables.Clear();
            ViewModel = null;
            _isVisible = false;
        }

        /// <summary>
        /// 显示动画
        /// </summary>
        protected virtual async UniTask PlayEnterAnimationAsync()
        {
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            var sequence = DG.Tweening.DOTween.Sequence();

            // 从下方滑入 + 淡入
            _rectTransform.anchoredPosition += Vector2.up * 50f;
            sequence.Join(_rectTransform.DOAnchorPosY(_rectTransform.anchoredPosition.y - 50f, _enterAnimationDuration).SetEase(_enterEase));
            sequence.Join(_canvasGroup.DOFade(1f, _enterAnimationDuration * 0.8f));

            await sequence.AsyncWaitForCompletion();
        }

        /// <summary>
        /// 隐藏动画
        /// </summary>
        protected virtual async UniTask PlayExitAnimationAsync()
        {
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            var sequence = DG.Tweening.DOTween.Sequence();

            sequence.Join(_rectTransform.DOAnchorPosY(_rectTransform.anchoredPosition.y + 50f, _exitAnimationDuration).SetEase(_exitEase));
            sequence.Join(_canvasGroup.DOFade(0f, _exitAnimationDuration));

            await sequence.AsyncWaitForCompletion();
        }

        /// <summary>
        /// 视图销毁前的清理钩子
        /// </summary>
        protected virtual void OnDispose() { }

        protected virtual void OnDestroy()
        {
            OnDispose();
            BindDisposables.Dispose();
        }
    }

    /// <summary>
    /// 无 ViewModel 的简单 View 基类
    /// </summary>
    public abstract class ViewBase : MonoBehaviour
    {
        [Header("动画设置")]
        [SerializeField] private float _enterAnimationDuration = 0.3f;
        [SerializeField] private float _exitAnimationDuration = 0.2f;

        protected CanvasGroup CanvasGroup { get; private set; }
        protected RectTransform RectTransform { get; private set; }
        private bool _isVisible = false;

        public bool IsVisible => _isVisible;

        protected virtual void Awake()
        {
            CanvasGroup = GetComponent<CanvasGroup>();
            if (CanvasGroup == null) CanvasGroup = gameObject.AddComponent<CanvasGroup>();

            RectTransform = GetComponent<RectTransform>();

            CanvasGroup.alpha = 0f;
            CanvasGroup.interactable = false;
            CanvasGroup.blocksRaycasts = false;
        }

        public virtual async UniTask ShowAsync()
        {
            if (_isVisible) return;
            _isVisible = true;

            CanvasGroup.interactable = true;
            CanvasGroup.blocksRaycasts = true;

            await PlayEnterAnimationAsync();
            OnShown();
        }

        public virtual async UniTask HideAsync()
        {
            if (!_isVisible) return;

            await PlayExitAnimationAsync();

            CanvasGroup.interactable = false;
            CanvasGroup.blocksRaycasts = false;
            _isVisible = false;

            OnHidden();
        }

        protected virtual async UniTask PlayEnterAnimationAsync()
        {
            RectTransform.anchoredPosition += Vector2.up * 30f;
            var seq = DG.Tweening.DOTween.Sequence();
            seq.Join(RectTransform.DOAnchorPosY(RectTransform.anchoredPosition.y - 30f, _enterAnimationDuration).SetEase(DG.Tweening.Ease.OutBack));
            seq.Join(CanvasGroup.DOFade(1f, _enterAnimationDuration * 0.8f));
            await seq.AsyncWaitForCompletion();
        }

        protected virtual async UniTask PlayExitAnimationAsync()
        {
            var seq = DG.Tweening.DOTween.Sequence();
            seq.Join(RectTransform.DOAnchorPosY(RectTransform.anchoredPosition.y + 30f, _exitAnimationDuration).SetEase(DG.Tweening.Ease.InQuad));
            seq.Join(CanvasGroup.DOFade(0f, _exitAnimationDuration));
            await seq.AsyncWaitForCompletion();
        }

        protected virtual void OnShown() { }
        protected virtual void OnHidden() { }
    }
}