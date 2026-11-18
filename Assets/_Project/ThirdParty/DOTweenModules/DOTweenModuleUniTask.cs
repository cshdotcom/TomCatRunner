// DOTween + UniTask 桥接扩展
// 提供 AsyncWaitForCompletion 扩展方法（DOTween 官方 DOTweenModuleUniTask.cs 的精简实现）
#if UNITASK_2_OR_NEWER || UNITASK
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;

namespace DG.Tweening
{
    public static class DOTweenModuleUniTask
    {
        /// <summary>
        /// 等待 Tween 异步完成（基于 UniTask，零分配）
        /// </summary>
        public static UniTask AsyncWaitForCompletion(this Tween t)
        {
            if (!t.active) return UniTask.CompletedTask;
            return AsyncWaitForCompletionInternal(t);
        }

        /// <summary>
        /// 等待 Sequence 异步完成
        /// </summary>
        public static UniTask AsyncWaitForCompletion(this Sequence s)
        {
            if (!s.active) return UniTask.CompletedTask;
            return AsyncWaitForCompletionInternal(s);
        }

        private static async UniTask AsyncWaitForCompletionInternal(Tween t)
        {
            var tcs = new UniTaskCompletionSource();
            t.OnComplete(() =>
            {
                tcs.TrySetResult();
            });
            t.OnKill(() =>
            {
                tcs.TrySetCanceled();
            });
            await tcs.Task;
        }

        /// <summary>
        /// 等待 Tween 异步完成（带 CancellationToken）
        /// </summary>
        public static async UniTask AsyncWaitForCompletion(this Tween t, CancellationToken cancellationToken)
        {
            if (!t.active) return;
            var tcs = new UniTaskCompletionSource();
            t.OnComplete(() => tcs.TrySetResult());
            t.OnKill(() => tcs.TrySetCanceled());
            using (cancellationToken.Register(() =>
            {
                if (t != null && t.active) t.Kill();
            }))
            {
                await tcs.Task;
            }
        }
    }
}
#endif
