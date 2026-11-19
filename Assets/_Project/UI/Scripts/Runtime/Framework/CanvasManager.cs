using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TomCatRunner.Common;

namespace TomCatRunner.UI.Framework
{
    /// <summary>
    /// 画布管理器 - 负责创建与维护 UI 层级（根 Canvas、常驻层、弹窗层、加载层、特效层）
    /// </summary>
    public class CanvasManager
    {
        private readonly Dictionary<UILayer, RectTransform> _layers = new Dictionary<UILayer, RectTransform>();
        private Canvas _rootCanvas;
        private CanvasScaler _scaler;

        /// <summary>根 Canvas</summary>
        public Canvas RootCanvas => _rootCanvas;
        /// <summary>根 RectTransform</summary>
        public RectTransform Root => _rootCanvas != null ? _rootCanvas.transform as RectTransform : null;

        public CanvasManager()
        {
            var host = new GameObject("[UI] CanvasRoot", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            _rootCanvas = host.GetComponent<Canvas>();
            _rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _rootCanvas.sortingOrder = 0;

            _scaler = host.GetComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(1920f, 1080f);
            _scaler.matchWidthOrHeight = 0.5f;

            foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
            {
                var layerGo = new GameObject($"Layer_{layer}", typeof(RectTransform));
                var rt = layerGo.GetComponent<RectTransform>();
                rt.SetParent(host.transform, false);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                _layers[layer] = rt;
            }

            UnityEngine.Object.DontDestroyOnLoad(host);
        }

        /// <summary>
        /// 获取指定层级的 RectTransform
        /// </summary>
        public RectTransform GetLayer(UILayer layer)
        {
            return _layers.TryGetValue(layer, out var rt) ? rt : Root;
        }

        /// <summary>
        /// 将视图挂载到指定层级
        /// </summary>
        public void AttachToLayer(RectTransform view, UILayer layer)
        {
            if (view == null) return;
            view.SetParent(GetLayer(layer), false);
        }

        /// <summary>
        /// 设置全屏遮挡（暂停/加载时屏蔽下层输入）
        /// </summary>
        public void SetLayerInteractable(UILayer layer, bool interactable)
        {
            var rt = GetLayer(layer);
            var group = rt.GetComponent<CanvasGroup>() ?? rt.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = interactable;
        }
    }

    /// <summary>
    /// UI 层级（数值越大越靠前）
    /// </summary>
    public enum UILayer
    {
        Background = 0,   // 背景装饰
        Screen = 100,     // 常规界面
        Popup = 200,      // 弹窗
        Overlay = 300,    // 悬浮 HUD
        Loading = 400,    // 加载/过场
        Toast = 500,      // 提示条
    }
}
