using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TomCatRunner.Gameplay.Core;
using TomCatRunner.UI.Framework;

namespace TomCatRunner.Gameplay.UI
{
    /// <summary>倒计时覆盖层：3 → 2 → 1 → 出发！</summary>
    public class CountdownScreen
    {
        public GameObject Root { get; }
        private readonly Text _bigText;
        private readonly RectTransform _rt;

        public CountdownScreen(CanvasManager canvas)
        {
            var layer = canvas.GetLayer(UILayer.Loading);
            _rt = UIFactory.Stretch(layer, "Countdown");
            Root = _rt.gameObject;
            _bigText = UIFactory.CreateTitle(_rt, "", 220, Color.white);
            Root.SetActive(false);
        }

        public void ShowDigit(string text, Color color)
        {
            Root.SetActive(true);
            _bigText.text = text;
            _bigText.color = color;
            _rt.localScale = Vector3.one * 1.6f;
            _rt.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        public void Hide() => Root.SetActive(false);
    }

    /// <summary>暂停面板</summary>
    public class PauseScreen
    {
        public GameObject Root { get; }
        private readonly RectTransform _rt;

        public PauseScreen(CanvasManager canvas, System.Action onResume, System.Action onRestart, System.Action onMenu)
        {
            var layer = canvas.GetLayer(UILayer.Popup);
            _rt = UIFactory.Stretch(layer, "PauseScreen");
            Root = _rt.gameObject;

            var dim = UIFactory.CreatePanel(_rt, new Color(0f, 0f, 0f, 0.55f), "Dim");

            var box = UIFactory.Anchor(_rt, "Box", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680f, 620f));
            var boxImg = box.gameObject.AddComponent<Image>();
            boxImg.sprite = UIFactory.RoundedSprite;
            boxImg.type = Image.Type.Sliced;
            boxImg.color = new Color(0.09f, 0.12f, 0.19f, 0.97f);

            var title = UIFactory.CreateText(box, "暂 停", 64, Color.white, TextAnchor.UpperCenter, FontStyle.Bold, "Title");
            var titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -36f);
            titleRt.sizeDelta = new Vector2(600f, 100f);

            UIFactory.CreateButton(box, "继 续", new Vector2(0f, -60f), new Vector2(420f, 104f),
                new Color(0.22f, 0.62f, 0.38f), Color.white, () => onResume(), 44);
            UIFactory.CreateButton(box, "重新开始", new Vector2(0f, -196f), new Vector2(420f, 104f),
                new Color(0.96f, 0.5f, 0.16f), Color.white, () => onRestart(), 44);
            UIFactory.CreateButton(box, "返回主页", new Vector2(0f, -332f), new Vector2(420f, 104f),
                new Color(0.24f, 0.28f, 0.38f), Color.white, () => onMenu(), 44);

            var tip = UIFactory.CreateText(box, "按 ESC / P 也可继续", 26, new Color(1f, 1f, 1f, 0.45f),
                TextAnchor.MiddleCenter, FontStyle.Normal, "Tip");
            var tipRt = tip.rectTransform;
            tipRt.anchorMin = new Vector2(0.5f, 0f);
            tipRt.anchorMax = new Vector2(0.5f, 0f);
            tipRt.pivot = new Vector2(0.5f, 0f);
            tipRt.anchoredPosition = new Vector2(0f, 22f);
            tipRt.sizeDelta = new Vector2(500f, 40f);

            Root.SetActive(false);
        }

        public void Show()
        {
            Root.SetActive(true);
            var box = _rt.Find("Box");
            if (box != null)
            {
                box.localScale = Vector3.one * 0.85f;
                box.DOScale(1f, 0.24f).SetEase(Ease.OutBack).SetUpdate(true);
            }
        }

        public void Hide() => Root.SetActive(false);
    }

    /// <summary>结算面板：成绩统计 + 新纪录</summary>
    public class GameOverScreen
    {
        public GameObject Root { get; }
        private readonly RectTransform _rt;
        private readonly Text _scoreText, _distText, _coinText, _bestText, _newBestText;
        private Tweener _newBestLoop;

        public GameOverScreen(CanvasManager canvas, System.Action onRetry, System.Action onMenu)
        {
            var layer = canvas.GetLayer(UILayer.Popup);
            _rt = UIFactory.Stretch(layer, "GameOverScreen");
            Root = _rt.gameObject;

            var dim = UIFactory.CreatePanel(_rt, new Color(0f, 0f, 0f, 0.6f), "Dim");

            var box = UIFactory.Anchor(_rt, "Box", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 900f));
            var boxImg = box.gameObject.AddComponent<Image>();
            boxImg.sprite = UIFactory.RoundedSprite;
            boxImg.type = Image.Type.Sliced;
            boxImg.color = new Color(0.1f, 0.12f, 0.2f, 0.98f);

            var title = UIFactory.CreateTitle(box, "奔 跑 结 束", 76, Color.white);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -34f);
            titleRt.sizeDelta = new Vector2(700f, 110f);

            // 新纪录飘带
            _newBestText = UIFactory.CreateText(box, "★ 新纪录 ★", 48, new Color(1f, 0.4f, 0.2f),
                TextAnchor.MiddleCenter, FontStyle.Bold, "NewBest");
            var nbRt = _newBestText.rectTransform;
            nbRt.anchorMin = new Vector2(0.5f, 1f);
            nbRt.anchorMax = new Vector2(0.5f, 1f);
            nbRt.pivot = new Vector2(0.5f, 1f);
            nbRt.anchoredPosition = new Vector2(0f, -140f);
            nbRt.sizeDelta = new Vector2(500f, 66f);
            _newBestText.gameObject.SetActive(false);

            // 分数大字
            _scoreText = UIFactory.CreateTitle(box, "0", 110, new Color(1f, 0.86f, 0.35f));
            var scRt = _scoreText.rectTransform;
            scRt.anchorMin = new Vector2(0.5f, 1f);
            scRt.anchorMax = new Vector2(0.5f, 1f);
            scRt.pivot = new Vector2(0.5f, 1f);
            scRt.anchoredPosition = new Vector2(0f, -200f);
            scRt.sizeDelta = new Vector2(700f, 150f);

            // 统计行
            _distText = BuildStat(box, -370f, "奔跑距离");
            _coinText = BuildStat(box, -450f, "拾取金币");
            _bestText = BuildStat(box, -530f, "历史纪录");

            UIFactory.CreateButton(box, "再 跑 一 次", new Vector2(0f, -680f), new Vector2(480f, 112f),
                new Color(0.96f, 0.5f, 0.16f), Color.white, () => onRetry(), 46);
            UIFactory.CreateButton(box, "返回主页", new Vector2(0f, -800f), new Vector2(480f, 104f),
                new Color(0.24f, 0.28f, 0.38f), Color.white, () => onMenu(), 44);
            Root.SetActive(false);
        }

        private Text BuildStat(RectTransform box, float y, string label)
        {
            var rt = UIFactory.Anchor(box, $"Stat_{label}", new Vector2(0.5f, 1f),
                new Vector2(0f, y), new Vector2(560f, 64f));

            var lbl = UIFactory.CreateText(rt, label, 32, new Color(1f, 1f, 1f, 0.6f),
                TextAnchor.MiddleLeft, FontStyle.Bold, "Label");
            var lrt = lbl.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0.5f);
            lrt.anchorMax = new Vector2(0f, 0.5f);
            lrt.pivot = new Vector2(0f, 0.5f);
            lrt.anchoredPosition = new Vector2(20f, 0f);
            lrt.sizeDelta = new Vector2(280f, 60f);

            var val = UIFactory.CreateText(rt, "0", 40, Color.white, TextAnchor.MiddleRight, FontStyle.Bold, "Value");
            var vrt = val.rectTransform;
            vrt.anchorMin = new Vector2(1f, 0.5f);
            vrt.anchorMax = new Vector2(1f, 0.5f);
            vrt.pivot = new Vector2(1f, 0.5f);
            vrt.anchoredPosition = new Vector2(-20f, 0f);
            vrt.sizeDelta = new Vector2(280f, 60f);
            return val;
        }

        public void Show(ScoreSystem score, SaveService save, bool isNewBest)
        {
            Root.SetActive(true);
            _scoreText.text = score.Score.ToString("N0");
            _distText.text = $"{score.Distance:F0} m";
            _coinText.text = $"{score.Coins}";
            _bestText.text = save.BestScore.ToString("N0");
            _newBestText.gameObject.SetActive(isNewBest);

            if (isNewBest)
            {
                _newBestLoop?.Kill();
                _newBestText.rectTransform.localScale = Vector3.one;
                _newBestLoop = _newBestText.rectTransform.DOScale(1.15f, 0.5f).SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine).SetUpdate(true);
            }

            var box = _rt.Find("Box");
            if (box != null)
            {
                box.localScale = Vector3.one * 0.8f;
                box.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);
            }

            // 分数滚动动画
            int target = score.Score;
            var captured = _scoreText;
            DOTween.To(() => 0, v => captured.text = v.ToString("N0"), target, 0.8f)
                .SetEase(Ease.OutQuad).SetUpdate(true);
        }

        public void Hide()
        {
            _newBestLoop?.Kill();
            _newBestLoop = null;
            Root.SetActive(false);
        }
    }
}
