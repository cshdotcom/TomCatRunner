using System;
using UnityEngine;

namespace TomCatRunner.Gameplay.Core
{
    /// <summary>
    /// 输入服务 - 统一键盘与触屏输入，转换为游戏意图
    /// 键盘：A/D 或 ←/→ 切道；W/↑/空格 跳跃；S/↓ 滑铲；ESC/P 暂停
    /// 触屏：左右滑动切道；上滑/点按跳跃；下滑滑铲
    /// </summary>
    public class InputService
    {
        public event Action MoveLeftRequested;
        public event Action MoveRightRequested;
        public event Action JumpRequested;
        public event Action SlideRequested;
        public event Action PauseRequested;

        // 触屏滑动跟踪
        private bool _touchActive;
        private Vector2 _touchStart;
        private float _touchStartTime;
        private const float SwipeThresholdPx = 60f;   // 滑动判定距离
        private const float TapMaxDuration = 0.22f;   // 点按最长时间
        private const float TapMaxDrift = 24f;        // 点按最大漂移

        // 鼠标模拟滑动（编辑器调试用）
        private bool _mouseActive;
        private Vector2 _mouseStart;
        private float _mouseStartTime;

        public void Tick()
        {
            PollKeyboard();
            PollTouch();
            PollMouseSwipe();
        }

        private void PollKeyboard()
        {
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
                MoveLeftRequested?.Invoke();

            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
                MoveRightRequested?.Invoke();

            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) ||
                Input.GetKeyDown(KeyCode.Space))
                JumpRequested?.Invoke();

            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
                SlideRequested?.Invoke();

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
                PauseRequested?.Invoke();
        }

        private void PollTouch()
        {
            if (Input.touchCount <= 0)
            {
                _touchActive = false;
                return;
            }

            var touch = Input.GetTouch(0);
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    _touchActive = true;
                    _touchStart = touch.position;
                    _touchStartTime = Time.unscaledTime;
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (_touchActive)
                    {
                        var delta = touch.position - _touchStart;
                        var duration = Time.unscaledTime - _touchStartTime;
                        HandleSwipe(delta, duration, endedByTouch: true);
                    }
                    _touchActive = false;
                    break;

                case TouchPhase.Moved:
                    // 即时响应：滑动过程中超过阈值立即触发，无需抬手
                    if (_touchActive)
                    {
                        var delta = touch.position - _touchStart;
                        if (Mathf.Abs(delta.x) >= SwipeThresholdPx || Mathf.Abs(delta.y) >= SwipeThresholdPx)
                        {
                            HandleSwipe(delta, 1f, endedByTouch: false);
                            _touchStart = touch.position; // 重置起点，支持连续操作
                        }
                    }
                    break;
            }
        }

        private void PollMouseSwipe()
        {
            if (Input.GetMouseButtonDown(0))
            {
                _mouseActive = true;
                _mouseStart = Input.mousePosition;
                _mouseStartTime = Time.unscaledTime;
            }
            else if (Input.GetMouseButtonUp(0) && _mouseActive)
            {
                _mouseActive = false;
                var delta = (Vector2)Input.mousePosition - _mouseStart;
                var duration = Time.unscaledTime - _mouseStartTime;
                HandleSwipe(delta, duration, endedByTouch: true);
            }
        }

        private void HandleSwipe(Vector2 delta, float duration, bool endedByTouch)
        {
            if (delta.sqrMagnitude < SwipeThresholdPx * SwipeThresholdPx)
            {
                // 点按（触屏抬手时）→ 跳跃
                if (endedByTouch && duration <= TapMaxDuration && delta.magnitude <= TapMaxDrift)
                    JumpRequested?.Invoke();
                return;
            }

            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            {
                if (delta.x > 0) MoveRightRequested?.Invoke();
                else MoveLeftRequested?.Invoke();
            }
            else
            {
                if (delta.y > 0) JumpRequested?.Invoke();
                else SlideRequested?.Invoke();
            }
        }
    }
}
