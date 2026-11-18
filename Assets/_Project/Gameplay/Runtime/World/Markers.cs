using UnityEngine;

namespace TomCatRunner.Gameplay.World
{
    /// <summary>障碍种类（影响碰撞判定与受击反馈）</summary>
    public enum ObstacleKind
    {
        Fence,   // 低栏 - 跳跃通过
        Beam,    // 高梁 - 滑铲通过
        Spikes,  // 地刺 - 换道或跳跃
        Crate,   // 高箱 - 只能换道
        Moving,  // 移动箱 - 伺机通过
    }

    /// <summary>障碍标记 - 挂在障碍根节点上，由 CatController 检测</summary>
    public class ObstacleMarker : MonoBehaviour
    {
        public ObstacleKind Kind;
        public float Length = 1f;   // 沿 Z 的碰撞长度（用于跳跃越过的额外判定）
    }

    /// <summary>程序化生成内容标记 - ChunkContentBuilder 用它识别并清理动态生成的子物体</summary>
    public class GeneratedContentMarker : MonoBehaviour
    {
    }

    /// <summary>金币行为 - 旋转、浮动、磁吸、收集动画</summary>
    public class CoinBehaviour : MonoBehaviour
    {
        public int Value = 1;
        public float SpinSpeed = 140f;
        public float BobAmplitude = 0.12f;
        public float BobSpeed = 3f;

        private Transform _cat;
        private float _baseY;
        private float _phase;
        private bool _collected;
        private Vector3 _collectStart;
        private float _collectT;
        private System.Action<CoinBehaviour> _onCollected;

        public void Initialize(Transform cat, float phase, System.Action<CoinBehaviour> onCollected)
        {
            _cat = cat;
            _phase = phase;
            _onCollected = onCollected;
            _baseY = transform.localPosition.y;
            _collected = false;
            transform.localScale = Vector3.one;
            transform.localPosition = new Vector3(transform.localPosition.x, _baseY, transform.localPosition.z);
        }

        private void Update()
        {
            if (_collected)
            {
                // 收集动画：飞向猫 + 放大 + 淡出（无 Shader 依赖，直接缩放到 0）
                _collectT += Time.deltaTime / 0.18f;
                if (_cat != null)
                {
                    transform.position = Vector3.Lerp(_collectStart, _cat.position + Vector3.up * 0.7f, _collectT);
                }
                float s = _collectT < 0.4f ? 1.6f : Mathf.Lerp(1.6f, 0f, (_collectT - 0.4f) / 0.6f);
                transform.localScale = Vector3.one * s;
                if (_collectT >= 1f) { Destroy(gameObject); }
                return;
            }

            float t = Time.time * BobSpeed + _phase;
            var lp = transform.localPosition;
            lp.y = _baseY + Mathf.Sin(t) * BobAmplitude;
            transform.localPosition = lp;
            transform.localRotation = Quaternion.Euler(90f, 0f, Time.time * SpinSpeed + _phase * 40f);

            // 磁吸：向猫飞行
            if (_cat != null && CatController.MagnetActive)
            {
                var toCat = _cat.position + Vector3.up * 0.6f - transform.position;
                if (toCat.sqrMagnitude < 16f) // 4m 半径
                {
                    transform.position += toCat.normalized * (28f * Time.deltaTime);
                }
            }
        }

        /// <summary>外部（CatController 触发器）调用：播放收集动画</summary>
        public void Collect()
        {
            if (_collected) return;
            _collected = true;
            _collectStart = transform.position;
            _collectT = 0f;
            _onCollected?.Invoke(this);
        }
    }

    /// <summary>道具种类</summary>
    public enum PowerupKind
    {
        Magnet,  // 磁铁：金币自动飞来
        Shield,  // 护盾：抵挡一次碰撞
        Boost,   // 冲刺：加速 + 短暂无敌
    }

    /// <summary>道具行为 - 旋转 + 呼吸浮动</summary>
    public class PowerupBehaviour : MonoBehaviour
    {
        public PowerupKind Kind;
        public float SpinSpeed = 90f;

        private float _baseY;
        private bool _collected;
        private float _phase;
        private System.Action<PowerupBehaviour> _onCollected;

        public void Initialize(Transform cat, float phase, System.Action<PowerupBehaviour> onCollected)
        {
            _phase = phase;
            _onCollected = onCollected;
            _baseY = transform.localPosition.y;
            _collected = false;
            transform.localScale = Vector3.one;
        }

        private void Update()
        {
            if (_collected)
            {
                _collectT += Time.deltaTime / 0.22f;
                float s = Mathf.Lerp(1.4f, 0f, _collectT);
                transform.localScale = Vector3.one * s;
                transform.Rotate(Vector3.up, 540f * Time.deltaTime, Space.World);
                if (_collectT >= 1f) Destroy(gameObject);
                return;
            }

            var lp = transform.localPosition;
            lp.y = _baseY + Mathf.Sin(Time.time * 2.2f + _phase) * 0.18f;
            transform.localPosition = lp;
            transform.Rotate(Vector3.up, SpinSpeed * Time.deltaTime, Space.World);
            transform.Rotate(Vector3.right, 30f * Time.deltaTime, Space.World);
        }

        private float _collectT;

        public void Collect()
        {
            if (_collected) return;
            _collected = true;
            _collectT = 0f;
            _onCollected?.Invoke(this);
        }
    }

    /// <summary>移动障碍 - 在两条车道间往返（Boss/Attack 段落）</summary>
    public class MovingObstacle : MonoBehaviour
    {
        public float XMin = -2.2f;
        public float XMax = 2.2f;
        public float Speed = 1.1f;       // 单程耗时（秒）
        public bool StartRight = true;

        private float _elapsed;

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.PingPong(_elapsed / Mathf.Max(0.05f, Speed), 1f);
            float x = Mathf.Lerp(XMin, XMax, StartRight ? t : 1f - t);
            var p = transform.position;
            p.x = x;
            transform.position = p;
        }
    }
}
