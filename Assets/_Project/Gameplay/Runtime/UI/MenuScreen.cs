using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TomCatRunner.Gameplay.Core;
using TomCatRunner.Gameplay.World;
using TomCatRunner.UI.Framework;

namespace TomCatRunner.Gameplay.UI
{
    /// <summary>
    /// 主菜单 - 标题、最佳纪录、开始按钮、操作说明、设置
    /// </summary>
    public class MenuScreen
    {
        public GameObject Root { get; }

        private readonly Text _seasonLabel;
        private readonly Text _bestLabel;
        private readonly Text _coinLabel;
        private readonly Text _titleText;
        private readonly RectTransform _rootRt;
        private readonly RectTransform _helpPanel;
        private readonly RectTransform _settingsPanel;
        private Button _musicBtn, _sfxBtn;

        private Tweener _titleTween;

        public MenuScreen(CanvasManager canvas, ThemeRuntime theme, SaveService save,
            System.Action onStart, System.Action<bool> onMusicToggle, System.Action<bool> onSfxToggle)
        {
            var layer = canvas.GetLayer(UILayer.Screen);
            _rootRt = UIFactory.Stretch(layer, "MenuScreen");
            Root = _rootRt.gameObject;

            // 半透明渐变底（上面透明下面暗）
            var dim = UIFactory.CreatePanel(_rootRt, new Color(0.05f, 0.07f, 0.12f, 0.42f), "Dim");
            dim.raycastTarget = false;

            // ---- 标题 ----
            var titleRt = UIFactory.Anchor(_rootRt, "TitleBlock", new Vector2(0.5f, 1f),
                new Vector2(0f, -170f), new Vector2(1400f, 380f));
            _titleText = UIFactory.CreateTitle(titleRt, "TOMCAT RUNNER", 130, Color.white);
            _seasonLabel = UIFactory.CreateText(titleRt, "", 44, new Color(1f, 1f, 1f, 0.92f),
                TextAnchor.MiddleCenter, FontStyle.Bold, "SeasonLabel");

            var seasonRt = _seasonLabel.rectTransform;
            seasonRt.anchorMin = new Vector2(0.5f, 0f);
            seasonRt.anchorMax = new Vector2(0.5f, 0f);
            seasonRt.pivot = new Vector2(0.5f, 0f);
            seasonRt.anchoredPosition = new Vector2(0f, 26f);
            seasonRt.sizeDelta = new Vector2(1200f, 70f);

            // 标题悬浮动画
            _titleTween = titleRt.DOAnchorPosY(-160f, 2.4f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);

            // ---- 纪录条 ----
            var statsRt = UIFactory.Anchor(_rootRt, "Stats", new Vector2(0.5f, 1f),
                new Vector2(0f, -540f), new Vector2(900f, 74f));
            _bestLabel = UIFactory.CreateBadge(statsRt, "", new Vector2(0f, 0.5f), new Vector2(-230f, 0f),
                new Vector2(420f, 74f), new Color(0.08f, 0.1f, 0.16f, 0.75f), Color.white, 32);
            _coinLabel = UIFactory.CreateBadge(statsRt, "", new Vector2(1f, 0.5f), new Vector2(230f, 0f),
                new Vector2(420f, 74f), new Color(0.08f, 0.1f, 0.16f, 0.75f), Color.white, 32);

            // ---- 主按钮 ----
            var accent = theme.Current.Accent;
            UIFactory.CreateButton(_rootRt, "开 始 奔 跑", new Vector2(0f, -110f), new Vector2(520f, 128f),
                accent, Color.white, () => onStart(), 52, "StartButton");
            UIFactory.CreateButton(_rootRt, "操作说明", new Vector2(-150f, -268f), new Vector2(240f, 86f),
                new Color(0.14f, 0.17f, 0.24f, 0.92f), Color.white, () => ToggleHelp(), 32);
            UIFactory.CreateButton(_rootRt, "设 置", new Vector2(150f, -268f), new Vector2(240f, 86f),
                new Color(0.14f, 0.17f, 0.24f, 0.92f), Color.white, () => ToggleSettings(), 32);

            // ---- 版本信息 ----
            var versionRt = UIFactory.Anchor(_rootRt, "Version", new Vector2(0.5f, 0f),
                new Vector2(0f, 24f), new Vector2(800f, 40f));
            var vt = UIFactory.CreateText(versionRt, $"v{Application.version} · Unity {Application.unityVersion}",
                24, new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleCenter, FontStyle.Normal);

            // ---- 操作说明面板 ----
            _helpPanel = BuildHelpPanel();

            // ---- 设置面板 ----
            _settingsPanel = BuildSettingsPanel(save, onMusicToggle, onSfxToggle);

            Refresh(save, theme);
            HideAll();
        }

        private RectTransform BuildHelpPanel()
        {
            var rt = UIFactory.Stretch(_rootRt, "HelpPanel");
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.6f);
            img.raycastTarget = true;

            var box = UIFactory.Anchor(rt, "Box", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 720f));
            var boxImg = box.gameObject.AddComponent<Image>();
            boxImg.sprite = UIFactory.RoundedSprite;
            boxImg.type = Image.Type.Sliced;
            boxImg.color = new Color(0.1f, 0.13f, 0.2f, 0.97f);

            UIFactory.CreateText(box, "操 作 说 明", 54, Color.white, TextAnchor.UpperCenter).rectTransform
                .anchoredPosition = new Vector2(0f, -60f);

            string help = "键盘：\n" +
                          "    ← → 或 A D —— 切换车道\n" +
                          "    ↑ / W / 空格 —— 跳跃\n" +
                          "    ↓ / S —— 滑铲\n" +
                          "    ESC / P —— 暂停\n\n" +
                          "触屏：\n" +
                          "    左右滑动 —— 切换车道\n" +
                          "    上滑或点按 —— 跳跃\n" +
                          "    下滑 —— 滑铲\n\n" +
                          "道具：\n" +
                          "    蓝色胶囊 —— 磁铁：金币自动飞来\n" +
                          "    黄色胶囊 —— 护盾：抵挡一次碰撞\n" +
                          "    红色胶囊 —— 冲刺：加速并撞碎障碍";
            var body = UIFactory.CreateText(box, help, 30, new Color(0.92f, 0.95f, 1f),
                TextAnchor.UpperLeft, FontStyle.Normal);
            var bodyRt = body.rectTransform;
            bodyRt.anchorMin = new Vector2(0.5f, 1f);
            bodyRt.anchorMax = new Vector2(0.5f, 1f);
            bodyRt.pivot = new Vector2(0.5f, 1f);
            bodyRt.anchoredPosition = new Vector2(0f, -130f);
            bodyRt.sizeDelta = new Vector2(740f, 480f);
            body.alignment = TextAnchor.UpperLeft;
            body.lineSpacing = 1.35f;

            UIFactory.CreateButton(box, "知道了", new Vector2(0f, -318f), new Vector2(280f, 90f),
                new Color(0.98f, 0.45f, 0.2f), Color.white, () => ToggleHelp(), 38);

            rt.gameObject.SetActive(false);
            return rt;
        }

        private RectTransform BuildSettingsPanel(SaveService save,
            System.Action<bool> onMusicToggle, System.Action<bool> onSfxToggle)
        {
            var rt = UIFactory.Stretch(_rootRt, "SettingsPanel");
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.6f);

            var box = UIFactory.Anchor(rt, "Box", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 460f));
            var boxImg = box.gameObject.AddComponent<Image>();
            boxImg.sprite = UIFactory.RoundedSprite;
            boxImg.type = Image.Type.Sliced;
            boxImg.color = new Color(0.1f, 0.13f, 0.2f, 0.97f);

            UIFactory.CreateText(box, "设 置", 54, Color.white, TextAnchor.UpperCenter).rectTransform
                .anchoredPosition = new Vector2(0f, -56f);

            _musicBtn = UIFactory.CreateButton(box, "", new Vector2(0f, -60f), new Vector2(420f, 92f),
                Color.white, Color.white,
                () =>
                {
                    bool on = !save.MusicOn;
                    save.SetMusicOn(on);
                    onMusicToggle(on);
                    UIFactory.SetButtonState(_musicBtn, on, "背景音乐：开", "背景音乐：关");
                }, 36, "MusicBtn");

            _sfxBtn = UIFactory.CreateButton(box, "", new Vector2(0f, -180f), new Vector2(420f, 92f),
                Color.white, Color.white,
                () =>
                {
                    bool on = !save.SfxOn;
                    save.SetSfxOn(on);
                    onSfxToggle(on);
                    UIFactory.SetButtonState(_sfxBtn, on, "音效：开", "音效：关");
                }, 36, "SfxBtn");

            UIFactory.SetButtonState(_musicBtn, save.MusicOn, "背景音乐：开", "背景音乐：关");
            UIFactory.SetButtonState(_sfxBtn, save.SfxOn, "音效：开", "音效：关");

            UIFactory.CreateButton(box, "返 回", new Vector2(0f, -300f), new Vector2(280f, 90f),
                new Color(0.2f, 0.24f, 0.34f), Color.white, () => ToggleSettings(), 38);

            rt.gameObject.SetActive(false);
            return rt;
        }

        private void ToggleHelp()
        {
            _helpPanel.gameObject.SetActive(!_helpPanel.gameObject.activeSelf);
            _settingsPanel.gameObject.SetActive(false);
        }

        private void ToggleSettings()
        {
            _settingsPanel.gameObject.SetActive(!_settingsPanel.gameObject.activeSelf);
            _helpPanel.gameObject.SetActive(false);
        }

        public void Refresh(SaveService save, ThemeRuntime theme)
        {
            _bestLabel.text = $"最高纪录  {save.BestScore:N0}";
            _coinLabel.text = $"累计金币  {save.TotalCoins:N0}";
            _seasonLabel.text = theme.Current.DisplayName;
        }

        private void HideAll()
        {
            _helpPanel.gameObject.SetActive(false);
            _settingsPanel.gameObject.SetActive(false);
        }

        public void Show()
        {
            HideAll();
            Root.SetActive(true);
            var cg = UIFactory.GetOrAddCanvasGroup(_rootRt);
            cg.alpha = 0f;
            cg.DOFade(1f, 0.4f).SetUpdate(true);
            if (_titleTween != null && !_titleTween.IsActive())
                _titleTween = null; // 已销毁则不重建（仅在构造时创建一次）
        }

        public void Hide()
        {
            var cg = UIFactory.GetOrAddCanvasGroup(_rootRt);
            cg.DOFade(0f, 0.25f).SetUpdate(true)
                .OnComplete(() => Root.SetActive(false));
        }
    }
}
