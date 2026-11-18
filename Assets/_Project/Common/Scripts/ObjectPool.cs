using System;
using System.Collections.Generic;
using UnityEngine;

namespace TomCatRunner.Common
{
    /// <summary>
    /// 通用对象池
    /// </summary>
    public class ObjectPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Queue<T> _pool = new Queue<T>();
        private readonly Action<T> _onGet;
        private readonly Action<T> _onRelease;
        private readonly Action<T> _onCreate;

        public int CountAll => _pool.Count + CountActive;
        public int CountInactive => _pool.Count;
        public int CountActive { get; private set; }

        public ObjectPool(T prefab, Transform parent = null, int initialCapacity = 0,
            Action<T> onGet = null, Action<T> onRelease = null, Action<T> onCreate = null)
        {
            _prefab = prefab;
            _parent = parent;
            _onGet = onGet;
            _onRelease = onRelease;
            _onCreate = onCreate;

            for (int i = 0; i < initialCapacity; i++)
            {
                var obj = CreateInstance();
                _pool.Enqueue(obj);
            }
        }

        private T CreateInstance()
        {
            var obj = UnityEngine.Object.Instantiate(_prefab, _parent);
            obj.gameObject.SetActive(false);
            _onCreate?.Invoke(obj);
            return obj;
        }

        public T Get()
        {
            T obj;
            if (_pool.Count > 0)
            {
                obj = _pool.Dequeue();
            }
            else
            {
                obj = CreateInstance();
            }

            obj.gameObject.SetActive(true);
            CountActive++;
            _onGet?.Invoke(obj);
            return obj;
        }

        public void Release(T obj)
        {
            if (obj == null) return;

            _onRelease?.Invoke(obj);
            obj.gameObject.SetActive(false);
            _pool.Enqueue(obj);
            CountActive--;
        }

        public void Clear()
        {
            while (_pool.Count > 0)
            {
                var obj = _pool.Dequeue();
                if (obj != null)
                {
                    UnityEngine.Object.Destroy(obj.gameObject);
                }
            }
            CountActive = 0;
        }

        public void WarmUp(int count)
        {
            var temp = new List<T>();
            for (int i = 0; i < count; i++)
            {
                temp.Add(Get());
            }
            foreach (var obj in temp)
            {
                Release(obj);
            }
        }
    }

    /// <summary>
    /// GameObject 对象池（非组件版本）
    /// </summary>
    public class GameObjectPool
    {
        private readonly GameObject _prefab;
        private readonly Transform _parent;
        private readonly Queue<GameObject> _pool = new Queue<GameObject>();
        private readonly Action<GameObject> _onGet;
        private readonly Action<GameObject> _onRelease;

        public int CountInactive => _pool.Count;
        public int CountActive { get; private set; }

        public GameObjectPool(GameObject prefab, Transform parent = null, int initialCapacity = 0,
            Action<GameObject> onGet = null, Action<GameObject> onRelease = null)
        {
            _prefab = prefab;
            _parent = parent;
            _onGet = onGet;
            _onRelease = onRelease;

            for (int i = 0; i < initialCapacity; i++)
            {
                var obj = CreateInstance();
                _pool.Enqueue(obj);
            }
        }

        private GameObject CreateInstance()
        {
            var obj = UnityEngine.Object.Instantiate(_prefab, _parent);
            obj.SetActive(false);
            return obj;
        }

        public GameObject Get()
        {
            GameObject obj;
            if (_pool.Count > 0)
            {
                obj = _pool.Dequeue();
            }
            else
            {
                obj = CreateInstance();
            }

            obj.SetActive(true);
            CountActive++;
            _onGet?.Invoke(obj);
            return obj;
        }

        public void Release(GameObject obj)
        {
            if (obj == null) return;

            _onRelease?.Invoke(obj);
            obj.SetActive(false);
            _pool.Enqueue(obj);
            CountActive--;
        }

        public void Clear()
        {
            while (_pool.Count > 0)
            {
                var obj = _pool.Dequeue();
                if (obj != null)
                {
                    UnityEngine.Object.Destroy(obj);
                }
            }
            CountActive = 0;
        }
    }
}