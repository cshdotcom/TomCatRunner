using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using DG.Tweening;

namespace TomCatRunner.Gameplay.UI
{
    /// <summary>
    /// uGUI 代码构建工具 - 字体获取、圆角贴图、文本/按钮/面板工厂
    /// </summary>
    public static class UIFactory
    {
        private static Font _font;
        private static Sprite _roundedSprite;
        private static Sprite _circleSprite;

        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                    catch { }
                    if (_font == null)
                    {
                        try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                        catch { }
                    }
                    if (_font == null)
                        _font = Font.CreateDynamicFontFromOSFont("Arial", 16);
                }
                return _font;
            }
        }

        /// <summary>9 宫格圆角矩形（白色，可任意染色）</summary>
        public static Sprite RoundedSprite
        {
            get
            {
                if (_roundedSprite != null) return _roundedSprite;
                const int size = 64, r = 18;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = Mathf.Max(Mathf.Abs(x - size / 2f) - (size / 2f - r), 0f);
                        float dy = Mathf.Max(Mathf.Abs(y - size / 2f) - (size / 2f - r), 0f);
                        bool inside = dx * dx + dy * dy <= r * r + 0.5f;
                        px[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                    }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                _roundedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f,
                    100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
                return _roundedSprite;
            }
        }

        /// <summary>圆形贴图（金币图标/头像用）</summary>
        public static Sprite CircleSprite
        {
            get
            {
                if (_circleSprite != null) return _circleSprite;
                const int size = 64, r = 30;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x - size / 2f + 0.5f, dy = y - size / 2f + 0.5f;
                        bool inside = dx * dx + dy * dy <= r * r;
                        px[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                    }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
                return _circleSprite;
            }
        }

        // =====================================================================
        // 节点
        // =====================================================================

        public static RectTransform Node(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.localPosition = Vector3.zero;
            rt.localScale = Vector3.one;
            return rt;
        }

        /// <summary>拉伸铺满父级的节点</summary>
        public static RectTransform Stretch(RectTransform parent, string name)
        {
            var rt = Node(parent, name);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>锚点定位节点</summary>
        public static RectTransform Anchor(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = Node(parent, name);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        // =====================================================================
        // 控件
        // =====================================================================

        public static Text CreateText(RectTransform parent, string text, int size, Color color,
            TextAnchor alignment = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold,
            string name = "Text")
        {
            var rt = Node(parent, name);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = alignment;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>带描边+阴影的标题文本</summary>
        public static Text CreateTitle(RectTransform parent, string text, int size, Color color)
        {
            var t = CreateText(parent, text, size, color, TextAnchor.MiddleCenter, FontStyle.Bold, "Title");
            var outline = t.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.65f);
            outline.effectDistance = new Vector2(3f, -3f);
            var shadow = t.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
            shadow.effectDistance = new Vector2(5f, -6f);
            return t;
        }

        public static Image CreatePanel(RectTransform parent, Color color, string name = "Panel")
        {
            var rt = Stretch(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = RoundedSprite;
            img.type = Image.Type.Sliced;
            img.color = color;
            return img;
        }

        /// <summary>按钮：圆角背景 + 标签 + 点击反馈</summary>
        public static Button CreateButton(RectTransform parent, string label, Vector2 anchoredPos, Vector2 size,
            Color bg, Color textColor, UnityAction onClick, int fontSize = 40, string name = "Button")
        {
            var rt = Anchor(parent, name, new Vector2(0.5f, 0.5f), anchoredPos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = RoundedSprite;
            img.type = Image.Type.Sliced;
            img.color = bg;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            var labelRt = Node(rt, "Label");
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var txt = labelRt.gameObject.AddComponent<Text>();
            txt.font = Font;
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.color = textColor;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.text = label;
            txt.raycastTarget = false;

            // 点击缩放反馈（DOTween，不受 timeScale 影响）
            var captured = rt;
            btn.onClick.AddListener(() =>
            {
                captured.localScale = Vector3.one * 0.92f;
                DOTween.To(() => captured.localScale,
                    v => captured.localScale = v, Vector3.one, 0.22f)
                    .SetEase(Ease.OutBack).SetUpdate(true);
                onClick?.Invoke();
            });

            return btn;
        }

        /// <summary>顶部信息胶囊（图标 + 文本）</summary>
        public static Text CreateBadge(RectTransform parent, string initialText, Vector2 anchor, Vector2 pos,
            Vector2 size, Color bg, Color textColor, int fontSize = 34)
        {
            var rt = Anchor(parent, "Badge", anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = RoundedSprite;
            img.type = Image.Type.Sliced;
            img.color = bg;

            var labelRt = Node(rt, "Label");
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(14f, 0f);
            labelRt.offsetMax = new Vector2(-14f, 0f);
            var txt = labelRt.gameObject.AddComponent<Text>();
            txt.font = Font;
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.color = textColor;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.text = initialText;
            txt.raycastTarget = false;
            return txt;
        }

        /// <summary>获取或添加 CanvasGroup</summary>
        public static CanvasGroup GetOrAddCanvasGroup(RectTransform rt)
        {
            var cg = rt.GetComponent<CanvasGroup>();
            if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();
            return cg;
        }

        /// <summary>设置按钮背景色与标签</summary>
        public static void SetButtonState(Button btn, bool on, string labelOn, string labelOff)
        {
            var img = btn.GetComponent<Image>();
            if (img != null)
                img.color = on ? new Color(0.2f, 0.6f, 0.35f) : new Color(0.35f, 0.22f, 0.24f);
            var txt = btn.GetComponentInChildren<Text>();
            if (txt != null)
                txt.text = on ? labelOn : labelOff;
        }
    }
}
