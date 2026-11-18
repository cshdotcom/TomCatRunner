using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using TomCatRunner.Common;
using TMPro;

namespace TomCatRunner.UI
{
    /// <summary>
    /// 动效库 - 统一管理所有 UI 动画预设
    /// 提供：弹性缓动、跟随延迟、磁吸对齐、磨砂背景模糊、页面转场
    /// </summary>
    public static class MotionLibrary
    {
        // ===================== 缓动曲线预设 =====================

        public static readonly Ease SpringOut = Ease.OutElastic;
        public static readonly Ease SpringIn = Ease.InElastic;
        public static readonly Ease SpringInOut = Ease.InOutElastic;
        public static readonly Ease BounceOut = Ease.OutBounce;
        public static readonly Ease BounceIn = Ease.InBounce;
        public static readonly Ease BackOut = Ease.OutBack;
        public static readonly Ease BackIn = Ease.InBack;
        public static readonly Ease SmoothOut = Ease.OutCubic;
        public static readonly Ease SmoothIn = Ease.InCubic;
        public static readonly Ease SmoothInOut = Ease.InOutCubic;
        public static readonly Ease SharpOut = Ease.OutQuint;
        public static readonly Ease SharpIn = Ease.InQuint;
        public static readonly Ease ExpoOut = Ease.OutExpo;
        public static readonly Ease ExpoIn = Ease.InExpo;
        public static readonly Ease CircOut = Ease.OutCirc;
        public static readonly Ease CircIn = Ease.InCirc;

        // 自定义曲线
        public static readonly AnimationCurve OvershootCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public static readonly AnimationCurve AnticipateCurve = new AnimationCurve(
            new Keyframe(0, 0, 0, 0),
            new Keyframe(0.3f, -0.2f, 0, 0),
            new Keyframe(1, 1, 0, 0)
        );
        public static readonly AnimationCurve SettleCurve = new AnimationCurve(
            new Keyframe(0, 0, 2, 2),
            new Keyframe(0.5f, 1.1f, 0, 0),
            new Keyframe(0.7f, 0.95f, 0, 0),
            new Keyframe(1, 1, 0, 0)
        );

        static MotionLibrary()
        {
            OvershootCurve.postWrapMode = WrapMode.Clamp;
            AnticipateCurve.postWrapMode = WrapMode.Clamp;
            SettleCurve.postWrapMode = WrapMode.Clamp;
        }

        // ===================== 基础动画 =====================

        /// <summary>
        /// 淡入
        /// </summary>
        public static Tween FadeIn(CanvasGroup canvasGroup, float duration = 0.3f, Ease ease = Ease.OutQuad, float delay = 0f)
        {
            canvasGroup.alpha = 0f;
            return canvasGroup.DOFade(1f, duration).SetEase(ease).SetDelay(delay).SetUpdate(true);
        }

        /// <summary>
        /// 淡出
        /// </summary>
        public static Tween FadeOut(CanvasGroup canvasGroup, float duration = 0.2f, Ease ease = Ease.InQuad, float delay = 0f)
        {
            return canvasGroup.DOFade(0f, duration).SetEase(ease).SetDelay(delay).SetUpdate(true);
        }

        /// <summary>
        /// 缩放入场（弹性）
        /// </summary>
        public static Tween ScaleIn(RectTransform rect, float duration = 0.4f, Ease ease = Ease.OutBack, float delay = 0f, Vector3? fromScale = null)
        {
            var start = fromScale ?? Vector3.zero;
            rect.localScale = start;
            return rect.DOScale(1f, duration).SetEase(ease).SetDelay(delay).SetUpdate(true);
        }

        /// <summary>
        /// 缩放退场
        /// </summary>
        public static Tween ScaleOut(RectTransform rect, float duration = 0.2f, Ease ease = Ease.InBack, float delay = 0f, Vector3? toScale = null)
        {
            var end = toScale ?? Vector3.zero;
            return rect.DOScale(end, duration).SetEase(ease).SetDelay(delay).SetUpdate(true);
        }

        /// <summary>
        /// 滑入（从指定方向）
        /// </summary>
        public static Tween SlideIn(RectTransform rect, Vector2 fromOffset, float duration = 0.4f, Ease ease = Ease.OutCubic, float delay = 0f)
        {
            var original = rect.anchoredPosition;
            rect.anchoredPosition = original + fromOffset;
            return rect.DOAnchorPos(original, duration).SetEase(ease).SetDelay(delay).SetUpdate(true);
        }

        /// <summary>
        /// 滑出
        /// </summary>
        public static Tween SlideOut(RectTransform rect, Vector2 toOffset, float duration = 0.3f, Ease ease = Ease.InCubic, float delay = 0f)
        {
            var original = rect.anchoredPosition;
            return rect.DOAnchorPos(original + toOffset, duration).SetEase(ease).SetDelay(delay).SetUpdate(true);
        }

        /// <summary>
        /// 旋转入场
        /// </summary>
        public static Tween RotateIn(RectTransform rect, float fromAngle = -180f, float duration = 0.5f, Ease ease = Ease.OutBack, float delay = 0f)
        {
            rect.localRotation = Quaternion.Euler(0, 0, fromAngle);
            return rect.DORotate(Vector3.zero, duration, RotateMode.FastBeyond360).SetEase(ease).SetDelay(delay).SetUpdate(true);
        }

        // ===================== 组合动画 =====================

        /// <summary>
        /// 经典弹窗入场：缩放 + 淡入 + 滑入
        /// </summary>
        public static Sequence PopupEnter(RectTransform rect, CanvasGroup canvasGroup, Vector2 slideFrom = default, float duration = 0.4f, float delay = 0f)
        {
            var seq = DOTween.Sequence();

            if (slideFrom != default)
            {
                var original = rect.anchoredPosition;
                rect.anchoredPosition = original + slideFrom;
                seq.Join(rect.DOAnchorPos(original, duration).SetEase(Ease.OutCubic));
            }

            seq.Join(ScaleIn(rect, duration, Ease.OutBack));
            if (canvasGroup != null) seq.Join(FadeIn(canvasGroup, duration * 0.8f));

            seq.SetDelay(delay).SetUpdate(true);
            return seq;
        }

        /// <summary>
        /// 经典弹窗退场
        /// </summary>
        public static Sequence PopupExit(RectTransform rect, CanvasGroup canvasGroup, Vector2 slideTo = default, float duration = 0.25f, float delay = 0f)
        {
            var seq = DOTween.Sequence();

            if (slideTo != default)
            {
                seq.Join(rect.DOAnchorPos(rect.anchoredPosition + slideTo, duration).SetEase(Ease.InCubic));
            }

            seq.Join(ScaleOut(rect, duration, Ease.InBack));
            if (canvasGroup != null) seq.Join(FadeOut(canvasGroup, duration));

            seq.SetDelay(delay).SetUpdate(true);
            return seq;
        }

        /// <summary>
        /// 页面转场：推出旧页，拉入新页
        /// </summary>
        public static async UniTask PageTransition(RectTransform oldPage, RectTransform newPage, CanvasGroup oldCanvas, CanvasGroup newCanvas,
            PageTransitionDirection direction = PageTransitionDirection.Left, float duration = 0.4f)
        {
            var seq = DOTween.Sequence();

            float width = oldPage.rect.width;
            Vector2 oldOffset = direction switch
            {
                PageTransitionDirection.Left => Vector2.left * width,
                PageTransitionDirection.Right => Vector2.right * width,
                PageTransitionDirection.Up => Vector2.up * width,
                PageTransitionDirection.Down => Vector2.down * width,
                _ => Vector2.left * width
            };
            Vector2 newOffset = -oldOffset;

            // 旧页推出
            seq.Join(oldPage.DOAnchorPos(oldPage.anchoredPosition + oldOffset, duration).SetEase(Ease.InOutCubic));
            if (oldCanvas != null) seq.Join(FadeOut(oldCanvas, duration * 0.5f));

            // 新页拉入
            newPage.anchoredPosition += newOffset;
            newPage.gameObject.SetActive(true);
            seq.Join(newPage.DOAnchorPos(newPage.anchoredPosition - newOffset, duration).SetEase(Ease.OutCubic));
            if (newCanvas != null) seq.Join(FadeIn(newCanvas, duration * 0.8f).SetDelay(duration * 0.1f));

            await seq.AsyncWaitForCompletion();

            if (oldCanvas != null) oldCanvas.gameObject.SetActive(false);
        }

        public enum PageTransitionDirection { Left, Right, Up, Down }

        // ===================== 磁吸对齐 =====================

        /// <summary>
        /// 磁吸到目标位置
        /// </summary>
        public static Tween MagneticSnap(RectTransform rect, Vector2 targetPos, float maxDistance = 100f,
            float duration = 0.3f, Ease ease = Ease.OutCubic, float snapThreshold = 10f)
        {
            float dist = Vector2.Distance(rect.anchoredPosition, targetPos);
            if (dist > maxDistance) return null;

            if (dist < snapThreshold)
            {
                return rect.DOAnchorPos(targetPos, duration * 0.5f).SetEase(Ease.OutQuad).SetUpdate(true);
            }

            return rect.DOAnchorPos(targetPos, duration).SetEase(ease).SetUpdate(true);
        }

        /// <summary>
        /// 磁吸到最近的网格点
        /// </summary>
        public static Tween MagneticGridSnap(RectTransform rect, float gridSize, float maxDistance = 50f,
            float duration = 0.2f, Ease ease = Ease.OutQuad)
        {
            Vector2 pos = rect.anchoredPosition;
            Vector2 target = new Vector2(
                Mathf.Round(pos.x / gridSize) * gridSize,
                Mathf.Round(pos.y / gridSize) * gridSize
            );

            float dist = Vector2.Distance(pos, target);
            if (dist > maxDistance) return null;

            return rect.DOAnchorPos(target, duration).SetEase(ease).SetUpdate(true);
        }

        // ===================== 跟随延迟 =====================

        /// <summary>
        /// 创建跟随延迟效果（如拖拽时的跟随）
        /// </summary>
        public static IDisposable CreateFollowLag(RectTransform follower, RectTransform leader,
            float lagTime = 0.1f, float maxDistance = 500f, bool useUnscaledTime = true)
        {
            return new FollowLagBehaviour(follower, leader, lagTime, maxDistance, useUnscaledTime);
        }

        private class FollowLagBehaviour : IDisposable
        {
            private readonly RectTransform _follower;
            private readonly RectTransform _leader;
            private readonly float _lagTime;
            private readonly float _maxDistance;
            private readonly bool _useUnscaledTime;
            private Vector2 _currentPos;
            private bool _disposed = false;

            public FollowLagBehaviour(RectTransform follower, RectTransform leader, float lagTime, float maxDistance, bool useUnscaledTime)
            {
                _follower = follower;
                _leader = leader;
                _lagTime = lagTime;
                _maxDistance = maxDistance;
                _useUnscaledTime = useUnscaledTime;
                _currentPos = follower.anchoredPosition;
            }

            public void Update()
            {
                if (_disposed) return;

                float dt = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                Vector2 targetPos = _leader.anchoredPosition;

                float dist = Vector2.Distance(_currentPos, targetPos);
                if (dist > _maxDistance)
                {
                    _currentPos = targetPos;
                }
                else
                {
                    _currentPos = Vector2.Lerp(_currentPos, targetPos, 1f - Mathf.Exp(-dt / _lagTime));
                }

                _follower.anchoredPosition = _currentPos;
            }

            public void Dispose()
            {
                _disposed = true;
            }
        }

        // ===================== 列表动画 =====================

        /// <summary>
        /// 交错入场动画
        /// </summary>
        public static Sequence StaggeredEnter(List<RectTransform> items, float itemDuration = 0.3f,
            float staggerDelay = 0.05f, Vector2? slideFrom = null, Ease ease = Ease.OutBack)
        {
            var seq = DOTween.Sequence();

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                float delay = i * staggerDelay;

                if (slideFrom.HasValue)
                {
                    var original = item.anchoredPosition;
                    item.anchoredPosition = original + slideFrom.Value;
                    seq.Join(item.DOAnchorPos(original, itemDuration).SetEase(ease).SetDelay(delay));
                }

                seq.Join(ScaleIn(item, itemDuration, ease, delay));
            }

            seq.SetUpdate(true);
            return seq;
        }

        /// <summary>
        /// 交错退场动画
        /// </summary>
        public static Sequence StaggeredExit(List<RectTransform> items, float itemDuration = 0.2f,
            float staggerDelay = 0.03f, Vector2? slideTo = null, Ease ease = Ease.InBack, bool reverseOrder = false)
        {
            var seq = DOTween.Sequence();

            int count = items.Count;
            for (int i = 0; i < count; i++)
            {
                int index = reverseOrder ? count - 1 - i : i;
                var item = items[index];
                float delay = i * staggerDelay;

                if (slideTo.HasValue)
                {
                    seq.Join(item.DOAnchorPos(item.anchoredPosition + slideTo.Value, itemDuration).SetEase(ease).SetDelay(delay));
                }

                seq.Join(ScaleOut(item, itemDuration, ease, delay));
            }

            seq.SetUpdate(true);
            return seq;
        }

        // ===================== 数字滚动 =====================

        /// <summary>
        /// 数字滚动动画
        /// </summary>
        public static Tween AnimateNumber(TMP_Text text, int from, int to, float duration = 0.8f,
            Ease ease = Ease.OutCubic, string format = "N0", Action<int> onUpdate = null)
        {
            return DOTween.To(() => from, x =>
            {
                from = x;
                text.text = x.ToString(format);
                onUpdate?.Invoke(x);
            }, to, duration).SetEase(ease).SetUpdate(true);
        }

        /// <summary>
        /// 浮点数滚动
        /// </summary>
        public static Tween AnimateFloat(TMP_Text text, float from, float to, float duration = 0.8f,
            Ease ease = Ease.OutCubic, string format = "F1", Action<float> onUpdate = null)
        {
            return DOTween.To(() => from, x =>
            {
                from = x;
                text.text = x.ToString(format);
                onUpdate?.Invoke(x);
            }, to, duration).SetEase(ease).SetUpdate(true);
        }

        // ===================== 进度条动画 =====================

        /// <summary>
        /// 进度条平滑填充
        /// </summary>
        public static Tween FillProgress(Slider slider, float targetValue, float duration = 0.5f,
            Ease ease = Ease.OutCubic, Action onComplete = null)
        {
            return slider.DOValue(targetValue, duration).SetEase(ease).SetUpdate(true).OnComplete(() => onComplete?.Invoke());
        }

        /// <summary>
        /// 进度条脉冲
        /// </summary>
        public static Sequence PulseProgress(Slider slider, float minValue = 0f, float maxValue = 1f,
            float duration = 1f, int loops = -1)
        {
            var seq = DOTween.Sequence();
            seq.Append(slider.DOValue(maxValue, duration * 0.5f).SetEase(Ease.OutCubic));
            seq.Append(slider.DOValue(minValue, duration * 0.5f).SetEase(Ease.InCubic));
            seq.SetLoops(loops).SetUpdate(true);
            return seq;
        }

        // ===================== 按钮反馈 =====================

        /// <summary>
        /// 按钮点击反馈
        /// </summary>
        public static Sequence ButtonPress(RectTransform button, float pressScale = 0.9f,
            float pressDuration = 0.1f, float releaseDuration = 0.2f, Ease pressEase = Ease.OutQuad, Ease releaseEase = Ease.OutBack)
        {
            var seq = DOTween.Sequence();
            seq.Append(button.DOScale(pressScale, pressDuration).SetEase(pressEase));
            seq.Append(button.DOScale(1f, releaseDuration).SetEase(releaseEase));
            seq.SetUpdate(true);
            return seq;
        }

        /// <summary>
        /// 按钮悬停反馈
        /// </summary>
        public static Tween ButtonHover(RectTransform button, float hoverScale = 1.05f,
            float duration = 0.15f, Ease ease = Ease.OutQuad)
        {
            return button.DOScale(hoverScale, duration).SetEase(ease).SetUpdate(true);
        }

        /// <summary>
        /// 按钮取消悬停
        /// </summary>
        public static Tween ButtonUnhover(RectTransform button, float duration = 0.2f, Ease ease = Ease.OutBack)
        {
            return button.DOScale(1f, duration).SetEase(ease).SetUpdate(true);
        }

        // ===================== 抖动/震动 =====================

        /// <summary>
        /// 位置抖动
        /// </summary>
        public static Sequence ShakePosition(RectTransform rect, float strength = 10f,
            float duration = 0.5f, int vibrato = 10, float randomness = 90f, bool fadeOut = true)
        {
            var seq = DOTween.Sequence();
            seq.Append(rect.DOShakeAnchorPos(duration, strength, vibrato, randomness, false, fadeOut).SetUpdate(true));
            return seq;
        }

        /// <summary>
        /// 缩放抖动
        /// </summary>
        public static Sequence ShakeScale(RectTransform rect, float strength = 0.2f,
            float duration = 0.3f, int vibrato = 5, float randomness = 90f)
        {
            var seq = DOTween.Sequence();
            seq.Append(rect.DOShakeScale(duration, strength, vibrato, randomness, fadeOut: true).SetUpdate(true));
            return seq;
        }

        /// <summary>
        /// 旋转抖动
        /// </summary>
        public static Sequence ShakeRotation(RectTransform rect, float strength = 15f,
            float duration = 0.3f, int vibrato = 5, float randomness = 90f)
        {
            var seq = DOTween.Sequence();
            seq.Append(rect.DOShakeRotation(duration, strength, vibrato, randomness, fadeOut: true).SetUpdate(true));
            return seq;
        }

        // ===================== 高光扫过 =====================

        /// <summary>
        /// 高光扫过效果（用于按钮、卡片）
        /// </summary>
        public static Sequence ShineSweep(Image target, RectTransform shineRect, float duration = 0.6f,
            float delay = 0f, Color? shineColor = null)
        {
            var seq = DOTween.Sequence();

            var originalColor = target.color;
            var color = shineColor ?? new Color(1, 1, 1, 0.5f);

            // 创建高光遮罩
            // 这里简化为颜色闪烁
            seq.Append(target.DOColor(color, duration * 0.3f).SetEase(Ease.OutQuad));
            seq.Append(target.DOColor(originalColor, duration * 0.7f).SetEase(Ease.InQuad));
            seq.SetDelay(delay).SetUpdate(true);

            return seq;
        }

        // ===================== 磨砂背景模糊 =====================

        /// <summary>
        /// 模糊背景动画
        /// </summary>
        public static async UniTask BlurBackground(CanvasGroup background, float targetBlur = 1f,
            float duration = 0.3f, Ease ease = Ease.OutCubic)
        {
            // 需要配合 URP 的 Blur 效果或自定义 Shader
            // 这里仅做透明度过渡作为占位
            await background.DOFade(targetBlur, duration).SetEase(ease).SetUpdate(true).AsyncWaitForCompletion();
        }

        // ===================== 循环动画 =====================

        /// <summary>
        /// 呼吸动画（缩放循环）
        /// </summary>
        public static Sequence Breathing(RectTransform rect, float minScale = 0.95f, float maxScale = 1.05f,
            float duration = 2f, int loops = -1, Ease ease = Ease.InOutSine)
        {
            var seq = DOTween.Sequence();
            seq.Append(rect.DOScale(maxScale, duration * 0.5f).SetEase(ease));
            seq.Append(rect.DOScale(minScale, duration * 0.5f).SetEase(ease));
            seq.SetLoops(loops).SetUpdate(true);
            return seq;
        }

        /// <summary>
        /// 浮动动画（上下浮动）
        /// </summary>
        public static Sequence Floating(RectTransform rect, float amplitude = 10f,
            float duration = 2f, int loops = -1, Ease ease = Ease.InOutSine)
        {
            var seq = DOTween.Sequence();
            var original = rect.anchoredPosition;
            seq.Append(rect.DOAnchorPosY(original.y + amplitude, duration * 0.5f).SetEase(ease));
            seq.Append(rect.DOAnchorPosY(original.y - amplitude, duration * 0.5f).SetEase(ease));
            seq.SetLoops(loops).SetUpdate(true);
            return seq;
        }

        /// <summary>
        /// 旋转循环
        /// </summary>
        public static Tween RotateLoop(RectTransform rect, float degrees = 360f,
            float duration = 10f, int loops = -1, RotateMode mode = RotateMode.FastBeyond360)
        {
            return rect.DORotate(new Vector3(0, 0, degrees), duration, mode)
                .SetLoops(loops)
                .SetEase(Ease.Linear)
                .SetUpdate(true);
        }

        // ===================== 文本动画 =====================

        /// <summary>
        /// 逐字显示
        /// </summary>
        public static Tween Typewriter(TMP_Text text, float duration = 1f, Ease ease = Ease.Linear)
        {
            text.maxVisibleCharacters = 0;
            int totalChars = text.textInfo.characterCount;
            return DOTween.To(() => text.maxVisibleCharacters, x => text.maxVisibleCharacters = x, totalChars, duration)
                .SetEase(ease).SetUpdate(true);
        }

        /// <summary>
        /// 逐行显示
        /// </summary>
        public static Sequence TypewriterLines(TMP_Text text, float lineDelay = 0.3f, float charDuration = 0.5f)
        {
            var seq = DOTween.Sequence();
            int lineCount = text.textInfo.lineCount;

            for (int i = 0; i < lineCount; i++)
            {
                int lineIndex = i;
                seq.AppendCallback(() =>
                {
                    // 这里需要更复杂的实现来逐行显示
                    // 简化版：整体淡入
                });
                seq.AppendInterval(lineDelay);
            }

            return seq;
        }

        // ===================== 工具方法 =====================

        /// <summary>
        /// 并行执行多个 Tween
        /// </summary>
        public static Sequence Parallel(params Tween[] tweens)
        {
            var seq = DOTween.Sequence();
            foreach (var t in tweens)
            {
                seq.Join(t);
            }
            return seq;
        }

        /// <summary>
        /// 顺序执行多个 Tween
        /// </summary>
        public static Sequence Sequence(params Tween[] tweens)
        {
            var seq = DOTween.Sequence();
            foreach (var t in tweens)
            {
                seq.Append(t);
            }
            return seq;
        }

        /// <summary>
        /// 等待所有 Tween 完成
        /// </summary>
        public static async UniTask WaitAll(params Tween[] tweens)
        {
            foreach (var t in tweens)
            {
                if (t != null && t.active)
                {
                    await t.AsyncWaitForCompletion();
                }
            }
        }

        /// <summary>
        /// 安全停止并清理
        /// </summary>
        public static void SafeKill(ref Tween tween)
        {
            if (tween != null && tween.active)
            {
                tween.Kill();
                tween = null;
            }
        }

        public static void SafeKill(ref Sequence sequence)
        {
            if (sequence != null && sequence.active)
            {
                sequence.Kill();
                sequence = null;
            }
        }
    }

    /// <summary>
    /// UI 动画助手组件 - 挂载在需要动画的 GameObject 上
    /// </summary>
    public class UIAnimator : MonoBehaviour
    {
        [Header("入场动画")]
        [SerializeField] private EnterAnimationType _enterType = EnterAnimationType.Popup;
        [SerializeField] private float _enterDuration = 0.4f;
        [SerializeField] private Ease _enterEase = Ease.OutBack;
        [SerializeField] private Vector2 _slideFrom = Vector2.up * 100f;

        [Header("退场动画")]
        [SerializeField] private ExitAnimationType _exitType = ExitAnimationType.Popup;
        [SerializeField] private float _exitDuration = 0.25f;
        [SerializeField] private Ease _exitEase = Ease.InBack;
        [SerializeField] private Vector2 _slideTo = Vector2.down * 100f;

        [Header("循环动画")]
        [SerializeField] private bool _loopAnimation = false;
        [SerializeField] private LoopAnimationType _loopType = LoopAnimationType.Breathing;
        [SerializeField] private float _loopDuration = 2f;

        private RectTransform _rect;
        private CanvasGroup _canvasGroup;
        private Tween _currentSequence;
        private Tween _loopSequence;

        public enum EnterAnimationType { None, Fade, Scale, Slide, Popup, Rotate }
        public enum ExitAnimationType { None, Fade, Scale, Slide, Popup }
        public enum LoopAnimationType { None, Breathing, Floating, Rotate, Pulse }

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            PlayEnter();
            if (_loopAnimation) PlayLoop();
        }

        private void OnDisable()
        {
            StopLoop();
            _currentSequence?.Kill();
        }

        public void PlayEnter()
        {
            _currentSequence?.Kill();

            switch (_enterType)
            {
                case EnterAnimationType.Fade:
                    _currentSequence = MotionLibrary.FadeIn(_canvasGroup, _enterDuration, _enterEase);
                    break;
                case EnterAnimationType.Scale:
                    _currentSequence = MotionLibrary.ScaleIn(_rect, _enterDuration, _enterEase);
                    break;
                case EnterAnimationType.Slide:
                    _currentSequence = MotionLibrary.SlideIn(_rect, _slideFrom, _enterDuration, _enterEase);
                    break;
                case EnterAnimationType.Popup:
                    _currentSequence = MotionLibrary.PopupEnter(_rect, _canvasGroup, _slideFrom, _enterDuration);
                    break;
                case EnterAnimationType.Rotate:
                    _currentSequence = MotionLibrary.RotateIn(_rect, -180f, _enterDuration, _enterEase);
                    break;
            }
        }

        public async UniTask PlayExitAsync()
        {
            if (_loopSequence != null) StopLoop();

            _currentSequence?.Kill();

            switch (_exitType)
            {
                case ExitAnimationType.Fade:
                    _currentSequence = MotionLibrary.FadeOut(_canvasGroup, _exitDuration, _exitEase);
                    break;
                case ExitAnimationType.Scale:
                    _currentSequence = MotionLibrary.ScaleOut(_rect, _exitDuration, _exitEase);
                    break;
                case ExitAnimationType.Slide:
                    _currentSequence = MotionLibrary.SlideOut(_rect, _slideTo, _exitDuration, _exitEase);
                    break;
                case ExitAnimationType.Popup:
                    _currentSequence = MotionLibrary.PopupExit(_rect, _canvasGroup, _slideTo, _exitDuration);
                    break;
            }

            if (_currentSequence != null)
            {
                await _currentSequence.AsyncWaitForCompletion();
            }
        }

        public void PlayLoop()
        {
            StopLoop();

            switch (_loopType)
            {
                case LoopAnimationType.Breathing:
                    _loopSequence = MotionLibrary.Breathing(_rect, 0.95f, 1.05f, _loopDuration);
                    break;
                case LoopAnimationType.Floating:
                    _loopSequence = MotionLibrary.Floating(_rect, 10f, _loopDuration);
                    break;
                case LoopAnimationType.Rotate:
                    _loopSequence = MotionLibrary.RotateLoop(_rect, 360f, _loopDuration * 5);
                    break;
                case LoopAnimationType.Pulse:
                    _loopSequence = MotionLibrary.PulseProgress(GetComponent<Slider>(), 0f, 1f, _loopDuration);
                    break;
            }
        }

        public void StopLoop()
        {
            if (_loopSequence != null)
            {
                _loopSequence.Kill();
                _loopSequence = null;
            }
        }

        // 快捷方法
        public void PunchScale(float punch = 0.2f, float duration = 0.3f)
        {
            _rect.DOPunchScale(Vector3.one * punch, duration, 5, 0.5f).SetUpdate(true);
        }

        public void Shake(float strength = 10f, float duration = 0.3f)
        {
            MotionLibrary.ShakePosition(_rect, strength, duration).SetUpdate(true);
        }
    }
}