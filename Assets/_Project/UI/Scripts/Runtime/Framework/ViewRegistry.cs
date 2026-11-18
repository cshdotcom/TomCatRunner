using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace TomCatRunner.UI.Framework
{
    /// <summary>
    /// 视图注册表 - 维护视图预制体与类型的映射，供 UIManager 按需实例化
    /// </summary>
    public class ViewRegistry
    {
        private readonly Dictionary<Type, ViewRegistration> _viewsByType = new Dictionary<Type, ViewRegistration>();
        private readonly Dictionary<string, ViewRegistration> _viewsById = new Dictionary<string, ViewRegistration>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 视图注册信息
        /// </summary>
        public class ViewRegistration
        {
            public string ViewId;
            public GameObject Prefab;
            public Type ViewType;
            public UILayer Layer;
        }

        /// <summary>
        /// 注册视图预制体
        /// </summary>
        public void Register<TView>(GameObject prefab, string viewId = null, UILayer layer = UILayer.Screen) where TView : Component
        {
            var reg = new ViewRegistration
            {
                ViewId = viewId ?? typeof(TView).Name,
                Prefab = prefab,
                ViewType = typeof(TView),
                Layer = layer,
            };

            _viewsByType[typeof(TView)] = reg;
            if (!string.IsNullOrEmpty(reg.ViewId))
            {
                _viewsById[reg.ViewId] = reg;
            }
        }

        /// <summary>
        /// 按类型获取注册信息
        /// </summary>
        public ViewRegistration Get<TView>() where TView : Component
        {
            return _viewsByType.TryGetValue(typeof(TView), out var reg) ? reg : null;
        }

        /// <summary>
        /// 按视图 ID 获取注册信息
        /// </summary>
        public ViewRegistration Get(string viewId)
        {
            return !string.IsNullOrEmpty(viewId) && _viewsById.TryGetValue(viewId, out var reg) ? reg : null;
        }

        /// <summary>
        /// 视图预制体是否已注册
        /// </summary>
        public bool IsRegistered<TView>() where TView : Component
        {
            return _viewsByType.ContainsKey(typeof(TView));
        }

        /// <summary>
        /// 注销视图
        /// </summary>
        public void Unregister<TView>() where TView : Component
        {
            if (_viewsByType.TryGetValue(typeof(TView), out var reg))
            {
                _viewsByType.Remove(typeof(TView));
                if (!string.IsNullOrEmpty(reg.ViewId))
                {
                    _viewsById.Remove(reg.ViewId);
                }
            }
        }

        /// <summary>
        /// 已注册视图数量
        /// </summary>
        public int Count => _viewsByType.Count;
    }
}
