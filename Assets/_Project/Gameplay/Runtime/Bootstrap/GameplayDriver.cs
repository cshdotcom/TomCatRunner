using UnityEngine;
using TomCatRunner.Gameplay.Core;

namespace TomCatRunner.Gameplay.Bootstrap
{
    /// <summary>
    /// 帧驱动器 - MonoBehaviour 壳，驱动 GameFlowController
    /// </summary>
    public class GameplayDriver : MonoBehaviour
    {
        private GameFlowController _controller;

        public void Construct(GameFlowController controller) => _controller = controller;

        private void Update()
        {
            _controller?.Tick(Time.deltaTime);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && _controller != null && _controller.State == GameState.Playing)
            {
                // 失焦自动暂停
                _controller.Pause();
            }
        }
    }
}
