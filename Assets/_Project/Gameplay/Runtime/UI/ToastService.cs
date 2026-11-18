using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TomCatRunner.UI.Framework;

namespace TomCatRunner.Gameplay.UI
{
    /// <summary>
    /// Toast 提示服务 - 屏幕中上方的浮动消息（道具、连击里程碑、护盾抵挡等）
    /// </summary>
    public class ToastService
    {
        private readonly RectTransform _layer;
        private readonly Queue<(string text, Color color)> _queue = new Queue<(string, Color)>();
        private bool _showing;

        public ToastService(CanvasManager canvas)
        {
            _layer = UIFactory.Stretch(canvas.GetLayer(UILayer.Toast), "Toasts");
            var cg = UIFactory.GetOrAddCanvasGroup(_layer);
            cg.blocksRaycasts = false;
        }

        public void Show(string text, Color? color = null)
        {
            _queue.Enqueue((text, color ?? Color.white));
            if (!_showing) ShowNext();
        }

        private void ShowNext()
        {
            if (_queue.Count == 0)
            {
                _showing = false;
                return;
            }
            _showing = true;
            var (text, color) = _queue.Dequeue();

            var rt = UIFactory.Anchor(_layer, "Toast", new Vector2(0.5f, 1f),
                new Vector2(0f, 120f), new Vector2(900f, 84f));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UIFactory.RoundedSprite;
            img.type = Image.Type.Sliced;
            img.color = new Color(0.08f, 0.1f, 0.16f, 0.85f);
            img.raycastTarget = false;

            var txt = UIFactory.CreateText(rt, text, 38, color);
            txt.rectTransform.offsetMin = new Vector2(16f, 0f);
            txt.rectTransform.offsetMax = new Vector2(-16f, 0f);

            rt.anchoredPosition = new Vector2(0f, 140f);
            var seq = DOTween.Sequence().SetUpdate(true);
            seq.Append(rt.DOAnchorPosY(60f, 0.3f).SetEase(Ease.OutCubic));
            seq.AppendInterval(1.1f);
            seq.Append(rt.DOAnchorPosY(150f, 0.35f).SetEase(Ease.InCubic));
            seq.AppendCallback(() =>
            {
                Object.Destroy(rt.gameObject);
                ShowNext();
            });
        }
    }
}
