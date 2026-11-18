using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TomCatRunner.Gameplay.Core;
using TomCatRunner.Gameplay.World;
using TomCatRunner.UI.Framework;

namespace TomCatRunner.Gameplay.UI
{
    /// <summary>
    /// 游戏 HUD - 分数、距离、金币、连击、道具状态、暂停按钮
    /// </summary>
    public class HudScreen
    {
        public GameObject Root { get; }

        private readonly Text _scoreText;
        private readonly Text _distanceText;
        private readonly Text _coinText;
        private readonly Text _comboText;
        private readonly RectTransform _comboRt;
        private readonly Image _magnetIcon, _shieldIcon, _boostIcon;
        private readonly Text _magnetTime, _shieldTime, _boostTime;
        private readonly Image _speedVignette;

        private Tweener _comboPunch;
        private Tweener _coinPunch;

        public HudScreen(CanvasManager canvas, ThemeRuntime theme, System.Action onPause)
        {
            var layer = canvas.GetLayer(UILayer.Overlay);
            var rt = UIFactory.Stretch(layer, "HudScreen");
            Root = rt.gameObject;

            // ---- 左上：分数 + 距离 ----
            var scoreBlock = UIFactory.Anchor(rt, "ScoreBlock", new Vector2(0f, 1f),
                new Vector2(40f, -30f), new Vector2(520f, 150f));
            _scoreText = UIFactory.CreateText(scoreBlock, "0", 62, Color.white, TextAnchor.UpperLeft, FontStyle.Bold, "Score");
            var scoreRt = _scoreText.rectTransform;
            scoreRt.anchorMin = new Vector2(0f, 1f);
            scoreRt.anchorMax = new Vector2(0f, 1f);
            scoreRt.pivot = new Vector2(0f, 1f);
            scoreRt.anchoredPosition = Vector2.zero;
            scoreRt.sizeDelta = new Vector2(520f, 80f);

            _distanceText = UIFactory.CreateText(scoreBlock, "0 m", 34, new Color(1f, 1f, 1f, 0.85f),
                TextAnchor.UpperLeft, FontStyle.Bold, "Distance");
            var distRt = _distanceText.rectTransform;
            distRt.anchorMin = new Vector2(0f, 1f);
            distRt.anchorMax = new Vector2(0f, 1f);
            distRt.pivot = new Vector2(0f, 1f);
            distRt.anchoredPosition = new Vector2(6f, -86f);
            distRt.sizeDelta = new Vector2(520f, 50f);

            // ---- 右上：金币 + 暂停按钮 ----
            _coinText = UIFactory.CreateBadge(rt, "0", new Vector2(1f, 1f), new Vector2(-200f, -52f),
                new Vector2(320f, 72f), new Color(0.1f, 0.09f, 0.04f, 0.7f), new Color(1f, 0.86f, 0.4f), 36);

            var pauseBtn = UIFactory.CreateButton(rt, "II", new Vector2(-440f, -52f), new Vector2(84f, 84f),
                new Color(0.1f, 0.12f, 0.18f, 0.7f), Color.white, () => onPause(), 40, "PauseBtn");
            var pauseBtnRt = (RectTransform)pauseBtn.transform;
            pauseBtnRt.anchorMin = new Vector2(1f, 1f);
            pauseBtnRt.anchorMax = new Vector2(1f, 1f);
            pauseBtnRt.pivot = new Vector2(0.5f, 0.5f);
            pauseBtnRt.anchoredPosition = new Vector2(-440f, -52f);

            // ---- 中央：连击 ----
            _comboRt = UIFactory.Anchor(rt, "Combo", new Vector2(0.5f, 0.5f),
                new Vector2(0f, 210f), new Vector2(700f, 90f));
            _comboText = UIFactory.CreateTitle(_comboRt, "", 52, new Color(1f, 0.85f, 0.3f));
            _comboRt.gameObject.SetActive(false);

            // ---- 左下：道具状态 ----
            var powerBlock = UIFactory.Anchor(rt, "Powerups", new Vector2(0f, 0f),
                new Vector2(30f, 40f), new Vector2(400f, 220f));
            (_magnetIcon, _magnetTime) = BuildPowerupBadge(powerBlock, 0, new Color(0.25f, 0.55f, 1f), "磁");
            (_shieldIcon, _shieldTime) = BuildPowerupBadge(powerBlock, 1, new Color(1f, 0.85f, 0.25f), "盾");
            (_boostIcon, _boostTime) = BuildPowerupBadge(powerBlock, 2, new Color(1f, 0.35f, 0.25f), "冲");

            // ---- 速度暗角（四周渐暗，速度越快越明显）----
            var vig = UIFactory.Stretch(rt, "Vignette");
            _speedVignette = vig.gameObject.AddComponent<Image>();
            _speedVignette.sprite = UIFactory.RoundedSprite;
            _speedVignette.type = Image.Type.Sliced;
            _speedVignette.color = new Color(0f, 0f, 0.05f, 0f);
            _speedVignette.raycastTarget = false;

            Root.SetActive(false);
        }

        private (Image icon, Text time) BuildPowerupBadge(RectTransform parent, int row, Color color, string label)
        {
            var rt = UIFactory.Anchor(parent, $"Power_{label}", new Vector2(0f, 1f),
                new Vector2(0f, -row * 96f), new Vector2(340f, 82f));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UIFactory.RoundedSprite;
            img.type = Image.Type.Sliced;
            img.color = new Color(color.r, color.g, color.b, 0.28f);

            var iconRt = UIFactory.Anchor(rt, "Icon", new Vector2(0f, 0.5f), new Vector2(52f, 0f), new Vector2(72f, 72f));
            var iconImg = iconRt.gameObject.AddComponent<Image>();
            iconImg.sprite = UIFactory.CircleSprite;
            iconImg.color = color;
            iconImg.raycastTarget = false;
            var iconTxt = UIFactory.CreateText(iconRt, label, 34, Color.white);

            var timeTxt = UIFactory.CreateText(rt, "", 32, new Color(1f, 1f, 1f, 0.9f), TextAnchor.MiddleLeft, FontStyle.Bold, "Time");
            var trt = timeTxt.rectTransform;
            trt.anchorMin = new Vector2(0f, 0.5f);
            trt.anchorMax = new Vector2(1f, 0.5f);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.anchoredPosition = new Vector2(30f, 0f);
            trt.sizeDelta = new Vector2(-90f, 70f);

            rt.gameObject.SetActive(false);
            return (iconImg, timeTxt);
        }

        public void Show() { Root.SetActive(true); }
        public void Hide() { Root.SetActive(false); }

        /// <summary>每帧/高频刷新（由控制器节流调用）</summary>
        public void Refresh(ScoreSystem score, CatController cat, float speed01)
        {
            _scoreText.text = score.Score.ToString("N0");
            _distanceText.text = $"{score.Distance:F0} m";
            _coinText.text = score.Coins.ToString("N0");
            _speedVignette.color = new Color(0f, 0f, 0.05f, Mathf.Lerp(0f, 0.32f, speed01));

            UpdatePowerup(_magnetIcon, _magnetTime, cat.MagnetTimer, "磁铁");
            UpdatePowerup(_shieldIcon, _shieldTime, cat.ShieldTimer, "护盾");
            UpdatePowerup(_boostIcon, _boostTime, cat.BoostTimer, "冲刺");
        }

        private static void UpdatePowerup(Image icon, Text time, float timer, string label)
        {
            bool active = timer > 0f;
            var badge = icon.transform.parent;
            if (badge != null && badge.gameObject.activeSelf != active)
                badge.gameObject.SetActive(active);
            if (active)
                time.text = $"{label} {timer:F1}s";
        }

        /// <summary>连击弹出动画</summary>
        public void PlayCombo(int combo)
        {
            if (combo < 2) { _comboRt.gameObject.SetActive(false); return; }
            _comboRt.gameObject.SetActive(true);
            _comboText.text = $"COMBO ×{combo}";
            _comboPunch?.Kill();
            _comboRt.localScale = Vector3.one * 1.35f;
            _comboPunch = _comboRt.DOScale(1f, 0.28f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        public void BreakCombo()
        {
            _comboRt.gameObject.SetActive(false);
        }

        public void PunchCoin()
        {
            var rt = _coinText.transform.parent.GetComponent<RectTransform>();
            if (rt == null) return;
            _coinPunch?.Kill();
            rt.localScale = Vector3.one * 1.18f;
            _coinPunch = rt.DOScale(1f, 0.2f).SetEase(Ease.OutQuad).SetUpdate(true);
        }
    }
}
