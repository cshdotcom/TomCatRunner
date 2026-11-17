using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;

namespace TomCatRunner.Common
{
    public static class Extensions
    {
        // Transform
        public static void ResetLocal(this Transform t)
        {
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }

        public static void SetLayerRecursively(this GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                child.gameObject.SetLayerRecursively(layer);
            }
        }

        // Component
        public static T GetOrAddComponent<T>(this GameObject obj) where T : Component
        {
            return obj.TryGetComponent(out T comp) ? comp : obj.AddComponent<T>();
        }

        public static T GetOrAddComponent<T>(this Component comp) where T : Component
        {
            return comp.gameObject.GetOrAddComponent<T>();
        }

        // Addressables
        public static async UniTask<T> LoadAssetAsync<T>(this AssetReference reference) where T : UnityEngine.Object
        {
            var handle = reference.LoadAssetAsync<T>();
            await handle.Task;
            if (handle.Status == AsyncOperationStatus.Succeeded)
                return handle.Result;
            throw new Exception($"Failed to load asset: {reference.RuntimeKey}");
        }

        public static async UniTask<GameObject> InstantiateAsync(this AssetReferenceGameObject reference, Transform parent = null)
        {
            var handle = reference.InstantiateAsync(parent);
            await handle.Task;
            if (handle.Status == AsyncOperationStatus.Succeeded)
                return handle.Result;
            throw new Exception($"Failed to instantiate: {reference.RuntimeKey}");
        }

        // Coroutine
        public static Coroutine StartCoroutineSafe(this MonoBehaviour mb, IEnumerator routine)
        {
            if (mb == null || !mb.isActiveAndEnabled) return null;
            return mb.StartCoroutine(routine);
        }

        public static void StopCoroutineSafe(this MonoBehaviour mb, Coroutine routine)
        {
            if (mb != null && routine != null)
                mb.StopCoroutine(routine);
        }

        // Color
        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);
        public static Color32 WithAlpha(this Color32 c, byte a) => new Color32(c.r, c.g, c.b, a);

        // Vector
        public static Vector3 WithX(this Vector3 v, float x) => new Vector3(x, v.y, v.z);
        public static Vector3 WithY(this Vector3 v, float y) => new Vector3(v.x, y, v.z);
        public static Vector3 WithZ(this Vector3 v, float z) => new Vector3(v.x, v.y, z);

        public static Vector2 WithX(this Vector2 v, float x) => new Vector2(x, v.y);
        public static Vector2 WithY(this Vector2 v, float y) => new Vector2(v.x, y);

        // List
        public static T GetRandom<T>(this IList<T> list)
        {
            if (list == null || list.Count == 0) return default;
            return list[UnityEngine.Random.Range(0, list.Count)];
        }

        public static void Shuffle<T>(this IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // String
        public static string ToTimeString(this float seconds, bool showMilliseconds = false)
        {
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            return showMilliseconds ? t.ToString(@"mm\:ss\.ff") : t.ToString(@"mm\:ss");
        }

        // Async
        public static async UniTask WaitUntil(this MonoBehaviour mb, Func<bool> condition, float timeout = 30f)
        {
            float start = Time.time;
            while (!condition())
            {
                if (Time.time - start > timeout)
                    throw new TimeoutException("WaitUntil timed out");
                await UniTask.Yield();
            }
        }

        public static async UniTask WaitForFrames(this MonoBehaviour mb, int frames)
        {
            for (int i = 0; i < frames; i++)
                await UniTask.Yield();
        }
    }
}