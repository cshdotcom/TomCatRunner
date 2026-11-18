using System;
using UnityEngine;

namespace TomCatRunner.Gameplay.World
{
    /// <summary>
    /// 猫控制器 - 三车道跑酷：切道、跳跃、滑铲、碰撞、程序化动画
    /// 由 GameFlowController 设置 TargetSpeed 驱动前进
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CatController : MonoBehaviour
    {
        public const float LaneWidth = 2.2f;
        public static readonly float[] LaneX = { -LaneWidth, 0f, LaneWidth };

        /// <summary>磁铁是否生效（金币读取此静态值）</summary>
        public static bool MagnetActive;

        // ==== 运行状态 ====
        public int Lane { get; private set; } = 1;
        public bool IsAlive => _alive;
        public bool IsRunning => _running;
        public float CurrentSpeed => _speed;
        public float TargetSpeed { get; set; } = 0f;
        public float DeltaZ { get; private set; }
        public bool ShieldActive => _shieldTimer > 0f;
        public bool BoostActive => _boostTimer > 0f;
        public float MagnetTimer => _magnetTimer;
        public float ShieldTimer => _shieldTimer;
        public float BoostTimer => _boostTimer;

        // ==== 事件（GameFlowController 订阅）====
        public event Action<CoinBehaviour> OnCoinTriggered;
        public event Action<PowerupBehaviour> OnPowerupTriggered;
        public event Action<ObstacleMarker> OnObstacleTriggered;

        // ==== 调校参数 ====
        private const float LaneSwitchSpeed = 14f;      // 切道插值速度
        private const float JumpVelocity = 8.6f;        // 起跳初速
        private const float Gravity = -24f;             // 重力（手动积分）
        private const float SlideDuration = 0.72f;      // 滑铲时长
        private const float JumpBufferTime = 0.12f;     // 输入缓冲
        private const float BodyHeight = 1.4f;          // 站立碰撞高度
        private const float SlideHeight = 0.62f;        // 滑铲碰撞高度

        // ==== 内部状态 ====
        private Rigidbody _rb;
        private CapsuleCollider _collider;
        private bool _alive = true;
        private bool _running;
        private float _speed;
        private float _vy;
        private bool _grounded = true;
        private float _jumpBufferTimer;
        private float _slideTimer;
        private float _magnetTimer, _shieldTimer, _boostTimer;
        private bool _dying;

        // ==== 视觉引用（CatBuilder 填充）====
        public CatVisuals Visuals;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _collider = GetComponent<CapsuleCollider>();
            _rb.isKinematic = true;
            _rb.useGravity = false;
            gameObject.tag = "Player";
        }

        private void OnEnable() { MagnetActive = false; }
        private void OnDisable() { MagnetActive = false; }

        public void SetRunning(bool running)
        {
            _running = running;
            if (!running) TargetSpeed = 0f;
        }

        public void ResetState()
        {
            _alive = true;
            _dying = false;
            _running = false;
            _speed = 0f;
            TargetSpeed = 0f;
            _vy = 0f;
            _grounded = true;
            _slideTimer = 0f;
            _jumpBufferTimer = 0f;
            Lane = 1;
            _magnetTimer = _shieldTimer = _boostTimer = 0f;
            MagnetActive = false;
            transform.position = new Vector3(0f, 0f, 0f);
            transform.rotation = Quaternion.identity;
            SetColliderHeight(BodyHeight);
            if (Visuals != null) Visuals.ResetToIdle();
        }

        // =====================================================================
        // 指令（InputService → GameFlowController → 这里）
        // =====================================================================

        public void MoveLeft()
        {
            if (!_alive || !_running) return;
            if (Lane > 0) Lane--;
        }

        public void MoveRight()
        {
            if (!_alive || !_running) return;
            if (Lane < 2) Lane++;
        }

        public void Jump()
        {
            if (!_alive || !_running) return;
            if (_grounded)
                DoJump();
            else
                _jumpBufferTimer = JumpBufferTime; // 落地前缓冲
        }

        public void Slide()
        {
            if (!_alive || !_running) return;
            if (!_grounded)
            {
                // 空中快速下压落地接滑铲
                _vy = Mathf.Min(_vy, -14f);
                _slideTimer = SlideDuration; // 落地后继续滑
            }
            else if (_slideTimer <= 0f)
            {
                _slideTimer = SlideDuration;
            }
        }

        private void DoJump()
        {
            _vy = JumpVelocity;
            _grounded = false;
            _slideTimer = 0f;
            SetColliderHeight(BodyHeight);
            Visuals?.OnJump();
        }

        // =====================================================================
        // 道具
        // =====================================================================

        public void ActivateMagnet(float duration) => _magnetTimer = Mathf.Max(_magnetTimer, duration);
        public void ActivateShield(float duration) => _shieldTimer = Mathf.Max(_shieldTimer, duration);
        public void ActivateBoost(float duration) => _boostTimer = Mathf.Max(_boostTimer, duration);

        // =====================================================================
        // 更新
        // =====================================================================

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // 计时器
            _magnetTimer = Mathf.Max(0f, _magnetTimer - dt);
            _shieldTimer = Mathf.Max(0f, _shieldTimer - dt);
            _boostTimer = Mathf.Max(0f, _boostTimer - dt);
            MagnetActive = _magnetTimer > 0f;

            if (_dying)
            {
                UpdateDying(dt);
                return;
            }

            if (!_running)
            {
                Visuals?.UpdateIdle(dt);
                return;
            }

            // ---- 前进（含加/减速）----
            float target = TargetSpeed * (BoostActive ? 1.6f : 1f);
            _speed = Mathf.MoveTowards(_speed, target, 18f * dt);
            float dz = _speed * dt;
            DeltaZ = dz;

            // ---- 垂直物理 ----
            if (!_grounded)
            {
                _vy += Gravity * dt;
                var p = transform.position;
                p.y += _vy * dt;
                if (p.y <= 0f)
                {
                    p.y = 0f;
                    _grounded = true;
                    _vy = 0f;
                    Visuals?.OnLand();
                    if (_jumpBufferTimer > 0f) { _jumpBufferTimer = 0f; DoJump(); }
                    else if (_slideTimer > 0f) SetColliderHeight(SlideHeight);
                }
                transform.position = p;
            }
            _jumpBufferTimer = Mathf.Max(0f, _jumpBufferTimer - dt);

            // ---- 滑铲 ----
            if (_slideTimer > 0f)
            {
                _slideTimer -= dt;
                SetColliderHeight(SlideHeight);
                if (_slideTimer <= 0f && _grounded)
                    SetColliderHeight(BodyHeight);
            }

            // ---- 车道插值 ----
            var pos = transform.position;
            pos.z += dz;
            float targetX = LaneX[Lane];
            float dx = Mathf.MoveTowards(pos.x, targetX, LaneSwitchSpeed * dt);
            // 切道倾斜由视觉处理
            pos.x = dx;
            transform.position = pos;

            // ---- 视觉动画 ----
            Visuals?.UpdateRun(dt, _speed, (targetX - pos.x), !_grounded, _slideTimer > 0f);
        }

        private void UpdateDying(float dt)
        {
            // 死亡翻滚：继续缓慢前移 + 前倾旋转 + 弹起下落
            _speed = Mathf.MoveTowards(_speed, 0f, 10f * dt);
            transform.position += Vector3.forward * (_speed * dt);

            if (!_grounded)
            {
                _vy += Gravity * dt;
                var p = transform.position;
                p.y += _vy * dt;
                if (p.y <= 0f) { p.y = 0f; _grounded = true; _vy = 0f; }
                transform.position = p;
            }
            Visuals?.UpdateDying(dt);
        }

        private void SetColliderHeight(float height)
        {
            if (_collider == null) return;
            _collider.height = height;
            _collider.center = new Vector3(0f, height * 0.5f, 0f);
        }

        // =====================================================================
        // 碰撞（触发器）
        // =====================================================================

        private void OnTriggerEnter(Collider other)
        {
            if (!_alive) return;

            var coin = other.GetComponent<CoinBehaviour>();
            if (coin != null) { OnCoinTriggered?.Invoke(coin); return; }

            var powerup = other.GetComponent<PowerupBehaviour>();
            if (powerup != null) { OnPowerupTriggered?.Invoke(powerup); return; }

            var obstacle = other.GetComponentInParent<ObstacleMarker>();
            if (obstacle != null)
            {
                if (BoostActive) return; // 冲刺无敌：撞碎障碍
                if (obstacle.Kind == ObstacleKind.Fence && !_grounded && transform.position.y > 0.35f) return; // 跳过
                if (obstacle.Kind == ObstacleKind.Beam && _slideTimer > 0f) return;                            // 滑过
                if (obstacle.Kind == ObstacleKind.Spikes && transform.position.y > 0.45f) return;               // 跳过

                OnObstacleTriggered?.Invoke(obstacle);
            }
        }

        /// <summary>护盾抵挡：消耗护盾并短暂无敌</summary>
        public bool TryConsumeShield()
        {
            if (_shieldTimer > 0f)
            {
                _shieldTimer = 0f;
                return true;
            }
            return false;
        }

        public void Die()
        {
            if (!_alive) return;
            _alive = false;
            _dying = true;
            _running = false;
            _vy = 4.2f;      // 小弹起
            _grounded = false;
            _speed = Mathf.Min(_speed, 6f);
            MagnetActive = false;
            Visuals?.OnDie();
        }
    }

    /// <summary>
    /// 猫视觉动画驱动 - CatBuilder 构建的骨骼引用与程序化动画
    /// </summary>
    public class CatVisuals
    {
        public Transform Model;
        public Transform Body, Head, Chest;
        public Transform EarLeft, EarRight;
        public Transform TailRoot, TailMid, TailTip;
        public Transform LegFL, LegFR, LegBL, LegBR;
        public Renderer[] Renderers;

        private float _runPhase;
        private float _tiltVel;
        private float _squash;
        private float _dieSpin;
        private Vector3 _modelBasePos = new Vector3(0f, 0.62f, 0f);
        private readonly Quaternion _modelBaseRot = Quaternion.identity;

        private static readonly Color HitFlash = new Color(1f, 0.25f, 0.2f);

        public void ResetToIdle()
        {
            _runPhase = 0f; _squash = 0f; _dieSpin = 0f;
            if (Model == null) return;
            Model.localPosition = _modelBasePos;
            Model.localRotation = _modelBaseRot;
            Model.localScale = Vector3.one;
            SetEmission(0f);
        }

        public void UpdateIdle(float dt)
        {
            // 待机：呼吸 + 尾巴慢摆 + 偶尔耳朵抖动
            if (Model == null) return;
            _runPhase += dt;
            float breathe = Mathf.Sin(_runPhase * 2.1f) * 0.02f;
            Model.localScale = new Vector3(1f - breathe * 0.5f, 1f + breathe, 1f - breathe * 0.5f);
            Model.localPosition = _modelBasePos;

            if (TailRoot != null) TailRoot.localRotation = Quaternion.Euler(0f, 0f, -20f + Mathf.Sin(_runPhase * 1.3f) * 8f);
            if (TailMid != null) TailMid.localRotation = Quaternion.Euler(0f, 0f, 25f + Mathf.Sin(_runPhase * 1.3f + 0.5f) * 10f);
            if (Head != null) Head.localRotation = Quaternion.Euler(Mathf.Sin(_runPhase * 0.7f) * 4f, Mathf.Sin(_runPhase * 0.43f) * 12f, 0f);
        }

        public void OnJump()
        {
            _squash = -0.35f; // 拉伸
        }

        public void OnLand()
        {
            _squash = 0.45f;  // 压扁
        }

        public void OnDie()
        {
            _dieSpin = 0f;
            SetEmission(0.9f, HitFlash);
        }

        public void UpdateRun(float dt, float speed, float laneOffset, bool airborne, bool sliding)
        {
            if (Model == null) return;

            _runPhase += dt * Mathf.Max(3f, speed * 1.15f);

            // ---- 切道倾斜（视觉）----
            float tilt = Mathf.Clamp(-laneOffset * 18f, -26f, 26f);
            float zRot = Mathf.SmoothDampAngle(Model.localRotation.eulerAngles.z > 180f
                ? Model.localRotation.eulerAngles.z - 360f
                : Model.localRotation.eulerAngles.z, tilt, ref _tiltVel, 0.08f);
            Model.localRotation = Quaternion.Euler(0f, 0f, zRot);

            // ---- 跑步弹跳 ----
            float bob = Mathf.Abs(Mathf.Sin(_runPhase * 2f)) * 0.055f * (speed / 10f);
            Model.localPosition = _modelBasePos + Vector3.up * bob;

            // ---- 腿部摆动 ----
            if (!airborne && !sliding)
            {
                float swing = 42f;
                SetLeg(LegFL, swing);
                SetLeg(LegFR, -swing);
                SetLeg(LegBL, -swing * 0.9f);
                SetLeg(LegBR, swing * 0.9f);
            }
            else if (airborne)
            {
                // 空中：前腿前伸，后腿后蹬
                if (LegFL != null) LegFL.localRotation = Quaternion.Euler(55f, 0f, 0f);
                if (LegFR != null) LegFR.localRotation = Quaternion.Euler(48f, 0f, 0f);
                if (LegBL != null) LegBL.localRotation = Quaternion.Euler(-52f, 0f, 0f);
                if (LegBR != null) LegBR.localRotation = Quaternion.Euler(-46f, 0f, 0f);
            }
            else
            {
                // 滑铲：腿向后贴
                if (LegFL != null) LegFL.localRotation = Quaternion.Euler(-75f, 0f, 0f);
                if (LegFR != null) LegFR.localRotation = Quaternion.Euler(-80f, 0f, 0f);
                if (LegBL != null) LegBL.localRotation = Quaternion.Euler(-20f, 0f, 0f);
                if (LegBR != null) LegBR.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            }

            // ---- 身体姿态 ----
            if (Body != null)
            {
                float lean = Mathf.Clamp(speed * 0.9f, 0f, 12f);
                Body.localRotation = Quaternion.Euler(
                    airborne ? -8f : (sliding ? 62f : lean),
                    0f, 0f);
            }

            // ---- 滑铲整体压低 ----
            float targetModelY = sliding ? 0.3f : _modelBasePos.y + bob;
            var lp = Model.localPosition;
            lp.y = Mathf.Lerp(lp.y, targetModelY, dt * 16f);
            Model.localPosition = lp;

            // ---- 拉伸/压扁恢复 ----
            if (_squash != 0f)
            {
                _squash = Mathf.MoveTowards(_squash, 0f, dt * 4.5f);
                float s = _squash;
                Model.localScale = new Vector3(1f + s * 0.5f, 1f - s, 1f + s * 0.25f);
            }
            else Model.localScale = Vector3.one;

            // ---- 尾巴 ----
            float wag = Mathf.Sin(_runPhase * 1.7f) * (10f + speed * 0.8f);
            if (TailRoot != null) TailRoot.localRotation = Quaternion.Euler(-8f, 0f, -14f + wag * 0.4f);
            if (TailMid != null) TailMid.localRotation = Quaternion.Euler(-4f, 0f, 30f + wag);
            if (TailTip != null) TailTip.localRotation = Quaternion.Euler(4f, 0f, 28f + wag * 1.2f);

            // ---- 头部微动 + 耳朵 ----
            if (Head != null)
                Head.localRotation = Quaternion.Euler(airborne ? 8f : Mathf.Sin(_runPhase) * 3f, 0f, 0f);
            float earTwitch = Mathf.Sin(_runPhase * 0.9f) * 5f;
            if (EarLeft != null) EarLeft.localRotation = Quaternion.Euler(0f, 0f, 14f + earTwitch);
            if (EarRight != null) EarRight.localRotation = Quaternion.Euler(0f, 0f, -14f - earTwitch);
        }

        public void UpdateDying(float dt)
        {
            _dieSpin += dt * 460f;
            if (Model == null) return;
            Model.localRotation = Quaternion.Euler(_dieSpin, 0f, 0f);
            Model.localPosition = new Vector3(0f, Mathf.Max(0.15f, _modelBasePos.y * 0.7f), 0f);
        }

        private void SetLeg(Transform leg, float swingDeg)
        {
            if (leg == null) return;
            float angle = Mathf.Sin(_runPhase * 2f) * swingDeg;
            leg.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }

        /// <summary>设置自发光强度（受击闪红 / 护盾金光）</summary>
        public void SetEmission(float intensity, Color? color = null)
        {
            if (Renderers == null) return;
            foreach (var r in Renderers)
            {
                if (r == null) continue;
                var mat = r.material;
                if (mat == null) continue;
                if (intensity <= 0.001f)
                {
                    mat.DisableKeyword("_EMISSION");
                }
                else
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", (color ?? Color.white) * intensity);
                }
            }
        }
    }
}
