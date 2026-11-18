using UnityEngine;
using UnityEngine.EventSystems;

namespace TomCatRunner.Gameplay.World
{
    /// <summary>
    /// 相机装备 - 平滑跟随 + 速度 FOV + 震屏 + 菜单环绕 + 死亡拉远
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public Transform Target;

        // 跑酷视角参数
        private static readonly Vector3 FollowOffset = new Vector3(0f, 4.1f, -7.0f);
        private const float LookAhead = 6.5f;
        private const float PosSmooth = 7.5f;
        private const float BaseFov = 62f;
        private const float MaxFov = 74f;

        public bool MenuOrbit { get; set; }
        private float _orbitAngle;

        // 死亡镜头
        public bool DeathView { get; set; }

        // 震屏
        private float _shakeIntensity;
        private float _shakeDecay = 2.2f;
        private Vector3 _shakeOffset;

        // 平滑速度
        private Vector3 _currentVel;
        private float _currentFov = BaseFov;

        private Camera _cam;
        private CatController _catCache;

        public void SetTarget(Transform target)
        {
            Target = target;
            _catCache = target != null ? target.GetComponent<CatController>() : null;
        }

        public static CameraRig Create(Transform parent, string name = "Main Camera")
        {
            var go = new GameObject(name);
            go.tag = "MainCamera";
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, 4.2f, -7f);
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = BaseFov;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 400f;
            go.AddComponent<AudioListener>();
            go.AddComponent<FlareLayer>();
            var rig = go.AddComponent<CameraRig>();
            rig._cam = cam;
            return rig;
        }

        public Camera Camera => _cam;

        public void Shake(float intensity)
        {
            _shakeIntensity = Mathf.Max(_shakeIntensity, intensity);
        }

        private void LateUpdate()
        {
            if (Target == null) return;
            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);

            Vector3 targetPos;
            Quaternion targetRot;

            if (MenuOrbit)
            {
                // 菜单：围绕猫缓慢环绕展示
                _orbitAngle += dt * 12f;
                float rad = _orbitAngle * Mathf.Deg2Rad;
                float radius = 7.5f;
                var center = Target.position + Vector3.up * 0.8f;
                targetPos = center + new Vector3(Mathf.Sin(rad) * radius, 2.6f, -Mathf.Cos(rad) * radius);
                targetRot = Quaternion.LookRotation(center - targetPos, Vector3.up);
                _currentFov = Mathf.Lerp(_currentFov, 55f, dt * 3f);
            }
            else if (DeathView)
            {
                // 死亡：拉远抬升俯视
                var center = Target.position;
                targetPos = center + new Vector3(2.2f, 6.5f, -8.5f);
                targetRot = Quaternion.LookRotation(center + Vector3.forward * 3f - targetPos, Vector3.up);
                _currentFov = Mathf.Lerp(_currentFov, 58f, dt * 3f);
            }
            else
            {
                // 跑酷跟随
                var center = Target.position;
                targetPos = new Vector3(center.x * 0.82f, FollowOffset.y, center.z + FollowOffset.z);
                targetRot = Quaternion.LookRotation(
                    new Vector3(center.x * 0.55f, 1.1f, center.z + LookAhead) - targetPos, Vector3.up);

                // 速度 FOV 反馈
                if (_catCache == null && Target != null)
                    _catCache = Target.GetComponent<CatController>();
                float speed = _catCache != null ? _catCache.CurrentSpeed : 0f;
                float targetFov = Mathf.Lerp(BaseFov, MaxFov, Mathf.Clamp01((speed - 8f) / 10f));
                _currentFov = Mathf.Lerp(_currentFov, targetFov, dt * 4f);
            }

            // 平滑位置
            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _currentVel, 1f / PosSmooth, Mathf.Infinity, dt);

            // 震屏衰减（unscaled：暂停/慢动作时也自然衰减）
            _shakeIntensity = Mathf.Max(0f, _shakeIntensity - _shakeDecay * _shakeIntensity * dt - 0.02f * dt);
            _shakeOffset = new Vector3(
                (Mathf.PerlinNoise(Time.unscaledTime * 27f, 0f) - 0.5f),
                (Mathf.PerlinNoise(0f, Time.unscaledTime * 23f) - 0.5f),
                0f) * _shakeIntensity * 1.6f;

            transform.rotation = targetRot;
            transform.position += _shakeOffset;

            if (_cam != null)
                _cam.fieldOfView = _currentFov;
        }
    }
}
