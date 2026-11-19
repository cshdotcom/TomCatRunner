using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;
using TomCatRunner.Common;
using TomCatRunner.Common.Reactive;

namespace TomCatRunner.UI.Framework
{
    /// <summary>
    /// UI 管理器 - 视图生命周期编排：注册、实例化、显示/隐藏、层级与卸载
    /// 由 GameLifetimeScope 注册为单例并驱动 Tick
    /// </summary>
    public class UIManager : IInitializable, ITickable, IDisposable
    {
        private readonly CanvasManager _canvasManager;
        private readonly ViewRegistry _registry;
        private readonly Scheduler _scheduler;
        private readonly Dictionary<string, Component> _activeViews = new Dictionary<string, Component>();

        private CancellationTokenSource _cts;

        public CanvasManager Canvas => _canvasManager;
        public ViewRegistry Registry => _registry;

        /// <summary>视图被打开（进入层级）时触发</summary>
        public event Action<Component> OnViewOpened;
        /// <summary>视图被关闭并回收时触发</summary>
        public event Action<Component> OnViewClosed;

        public UIManager(CanvasManager canvasManager, ViewRegistry registry, Scheduler scheduler)
        {
            _canvasManager = canvasManager ?? new CanvasManager();
            _registry = registry ?? new ViewRegistry();
            _scheduler = scheduler;
            _cts = new CancellationTokenSource();
        }

        public void Initialize()
        {
            Debug.Log($"[UIManager] Initialized. Registered views: {_registry.Count}");
        }

        /// <summary>
        /// 每帧驱动（由 VContainer ITickable / GameLifetimeScope 调用）
        /// </summary>
        public void Tick()
        {
            // 当前视图更新由各自 MonoBehaviour 驱动，这里保留给未来的
            // 全局 UI 动效 / 过渡队列。Scheduler 由 GameLifetimeScope 独立驱动。
        }

        /// <summary>
        /// 打开指定类型的视图（需要先注册预制体）
        /// </summary>
        public async UniTask<TView> OpenViewAsync<TView>(Transform parent = null)
            where TView : Component
        {
            var reg = _registry.Get<TView>();
            if (reg?.Prefab == null)
            {
                Debug.LogWarning($"[UIManager] View not registered: {typeof(TView).Name}");
                return null;
            }

            string key = reg.ViewId;
            if (_activeViews.TryGetValue(key, out var existing) && existing != null)
            {
                Debug.Log($"[UIManager] View already open: {key}");
                return existing as TView;
            }

            var go = UnityEngine.Object.Instantiate(reg.Prefab, parent ?? _canvasManager.GetLayer(reg.Layer));
            go.name = key;
            var view = go.GetComponent<TView>();

            _activeViews[key] = view;
            OnViewOpened?.Invoke(view);

            Debug.Log($"[UIManager] Opened view: {key}");
            await UniTask.Yield(_cts.Token);
            return view;
        }

        /// <summary>
        /// 关闭指定类型的视图
        /// </summary>
        public void CloseView<TView>() where TView : Component
        {
            var reg = _registry.Get<TView>();
            if (reg == null) return;

            string key = reg.ViewId;
            if (!_activeViews.TryGetValue(key, out var view) || view == null)
            {
                _activeViews.Remove(key);
                return;
            }

            OnViewClosed?.Invoke(view);
            _activeViews.Remove(key);
            UnityEngine.Object.Destroy(view.gameObject);

            Debug.Log($"[UIManager] Closed view: {key}");
        }

        /// <summary>
        /// 判断视图是否处于打开状态
        /// </summary>
        public bool IsOpen<TView>() where TView : Component
        {
            var reg = _registry.Get<TView>();
            return reg != null && _activeViews.ContainsKey(reg.ViewId);
        }

        /// <summary>
        /// 关闭全部视图（场景切换/回主菜单时）
        /// </summary>
        public void CloseAll()
        {
            foreach (var kvp in _activeViews)
            {
                if (kvp.Value != null)
                {
                    OnViewClosed?.Invoke(kvp.Value);
                    UnityEngine.Object.Destroy(kvp.Value.gameObject);
                }
            }
            _activeViews.Clear();
            Debug.Log("[UIManager] All views closed");
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            CloseAll();
        }
    }
}
