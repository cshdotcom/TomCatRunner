using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TomCatRunner.Common;

namespace TomCatRunner.UI
{
    /// <summary>
    /// UI 空间粒子系统 - 基于 Canvas 渲染的轻量级粒子
    /// 支持：爆发、持续发射、跟随、轨迹、文字飘动
    /// </summary>
    public class UIParticleSystem : MonoBehaviour
    {
        [Header("发射器设置")]
        [SerializeField] private ParticleEmitterConfig _defaultConfig;
        [SerializeField] private int _maxParticles = 500;
        [SerializeField] private bool _autoPlay = false;

        [Header("渲染")]
        [SerializeField] private Canvas _targetCanvas;
        [SerializeField] private int _sortingOrder = 100;
        [SerializeField] private Material _defaultParticleMaterial;

        private readonly List<UIParticle> _particles = new List<UIParticle>();
        private readonly Queue<UIParticle> _particlePool = new Queue<UIParticle>();
        private RectTransform _containerRect;
        private Canvas _canvas;
        private bool _isPlaying = false;

        public bool IsPlaying => _isPlaying;
        public int ActiveParticleCount => _particles.Count;

        private void Awake()
        {
            _containerRect = GetComponent<RectTransform>();
            if (_containerRect == null) _containerRect = gameObject.AddComponent<RectTransform>();

            _canvas = _targetCanvas ?? GetComponentInParent<Canvas>();
            if (_canvas == null)
            {
                var canvasObj = new GameObject("UIParticleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasObj.transform.SetParent(transform.parent);
                _canvas = canvasObj.GetComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = _sortingOrder;
            }

            // 预热池
            for (int i = 0; i < Mathf.Min(_maxParticles, 50); i++)
            {
                CreatePooledParticle();
            }

            if (_autoPlay) Play();
        }

        private void Update()
        {
            if (!_isPlaying) return;

            float dt = Time.unscaledDeltaTime;
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var p = _particles[i];
                p.Update(dt);
                if (!p.IsAlive)
                {
                    ReturnToPool(p);
                    _particles.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 爆发式发射
        /// </summary>
        public void Burst(Vector2 position, ParticleEmitterConfig config = null, int count = 10)
        {
            config = config ?? _defaultConfig;
            for (int i = 0; i < count; i++)
            {
                EmitParticle(position, config, true);
            }
        }

        /// <summary>
        /// 单个发射
        /// </summary>
        public UIParticle Emit(Vector2 position, ParticleEmitterConfig config = null)
        {
            config = config ?? _defaultConfig;
            return EmitParticle(position, config, false);
        }

        /// <summary>
        /// 持续发射
        /// </summary>
        public IDisposable EmitContinuous(Vector2 position, ParticleEmitterConfig config, float rate, float duration = -1f)
        {
            var cts = new System.Threading.CancellationTokenSource();
            EmitContinuousRoutine(position, config, rate, duration, cts.Token).Forget();
            return new DisposableAction(() => cts.Cancel());
        }

        private async UniTaskVoid EmitContinuousRoutine(Vector2 position, ParticleEmitterConfig config, float rate, float duration, System.Threading.CancellationToken token)
        {
            float interval = 1f / rate;
            float elapsed = 0f;

            while (!token.IsCancellationRequested && (duration < 0 || elapsed < duration))
            {
                EmitParticle(position, config, false);
                await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: token);
                elapsed += interval;
            }
        }

        private UIParticle EmitParticle(Vector2 position, ParticleEmitterConfig config, bool isBurst)
        {
            var particle = GetPooledParticle();
            particle.Initialize(position, config, _canvas, _containerRect, _defaultParticleMaterial, isBurst);
            _particles.Add(particle);
            return particle;
        }

        private UIParticle GetPooledParticle()
        {
            if (_particlePool.Count > 0)
            {
                var p = _particlePool.Dequeue();
                p.GameObject.SetActive(true);
                return p;
            }
            return CreatePooledParticle();
        }

        private UIParticle CreatePooledParticle()
        {
            var go = new GameObject("UIParticle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_containerRect, false);
            go.SetActive(false);
            return new UIParticle(go);
        }

        private void ReturnToPool(UIParticle particle)
        {
            particle.GameObject.SetActive(false);
            if (_particlePool.Count < _maxParticles)
            {
                _particlePool.Enqueue(particle);
            }
            else
            {
                Destroy(particle.GameObject);
            }
        }

        public void Play() => _isPlaying = true;
        public void Pause() => _isPlaying = false;
        public void Stop()
        {
            _isPlaying = false;
            foreach (var p in _particles) ReturnToPool(p);
            _particles.Clear();
        }

        public void Clear() => Stop();

        private void OnDestroy()
        {
            Stop();
            foreach (var p in _particlePool)
            {
                if (p.GameObject != null) Destroy(p.GameObject);
            }
            _particlePool.Clear();
        }
    }

    /// <summary>
    /// 粒子发射配置
    /// </summary>
    [Serializable]
    public class ParticleEmitterConfig
    {
        [Header("生命周期")]
        public float minLifetime = 0.5f;
        public float maxLifetime = 2f;

        [Header("大小")]
        public float startSize = 20f;
        public float endSize = 0f;
        public AnimationCurve sizeCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

        [Header("颜色")]
        public Gradient colorGradient;
        public bool useRandomColor = false;
        public Color[] randomColors;

        [Header("移动")]
        public Vector2 direction = Vector2.up;
        public float directionSpread = 30f; // 角度
        public float minSpeed = 50f;
        public float maxSpeed = 200f;
        public AnimationCurve speedCurve = AnimationCurve.EaseInOut(0, 1, 1, 0.1f);
        public float gravity = 0f;
        public float drag = 0.98f;

        [Header("旋转")]
        public float minRotation = 0f;
        public float maxRotation = 360f;
        public float rotationSpeed = 0f;

        [Header("缩放")]
        public Vector2 startScale = Vector2.one;
        public Vector2 endScale = Vector2.one;

        [Header("纹理")]
        public Sprite sprite;
        public Material material;

        [Header("淡入淡出")]
        public float fadeInTime = 0.1f;
        public float fadeOutTime = 0.3f;

        public ParticleEmitterConfig()
        {
            colorGradient = new Gradient();
            colorGradient.colorKeys = new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            };
            colorGradient.alphaKeys = new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            };
        }

        public static ParticleEmitterConfig CreateCoinBurst()
        {
            return new ParticleEmitterConfig
            {
                minLifetime = 0.5f,
                maxLifetime = 1.5f,
                startSize = 30f,
                endSize = 10f,
                colorGradient = CreateGradient(Color.yellow, Color.white),
                direction = Vector2.up,
                directionSpread = 60f,
                minSpeed = 100f,
                maxSpeed = 300f,
                gravity = -200f,
                drag = 0.95f,
                rotationSpeed = 360f,
                fadeInTime = 0.05f,
                fadeOutTime = 0.5f
            };
        }

        public static ParticleEmitterConfig CreateSparkle()
        {
            return new ParticleEmitterConfig
            {
                minLifetime = 0.3f,
                maxLifetime = 1f,
                startSize = 10f,
                endSize = 0f,
                colorGradient = CreateGradient(Color.cyan, Color.white),
                direction = Vector2.zero,
                directionSpread = 360f,
                minSpeed = 20f,
                maxSpeed = 80f,
                rotationSpeed = 180f,
                fadeInTime = 0.05f,
                fadeOutTime = 0.2f
            };
        }

        public static ParticleEmitterConfig CreateTextFloat(string text, Color color)
        {
            return new ParticleEmitterConfig
            {
                minLifetime = 1f,
                maxLifetime = 2f,
                startSize = 40f,
                endSize = 40f,
                colorGradient = CreateSolidGradient(color),
                direction = Vector2.up,
                directionSpread = 10f,
                minSpeed = 50f,
                maxSpeed = 80f,
                gravity = -30f,
                drag = 0.98f,
                fadeInTime = 0.1f,
                fadeOutTime = 0.5f
            };
        }

        private static Gradient CreateGradient(Color start, Color end)
        {
            var g = new Gradient();
            g.colorKeys = new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) };
            g.alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) };
            return g;
        }

        private static Gradient CreateSolidGradient(Color color)
        {
            var g = new Gradient();
            g.colorKeys = new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) };
            g.alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) };
            return g;
        }
    }

    /// <summary>
    /// 单个 UI 粒子
    /// </summary>
    public class UIParticle
    {
        public GameObject GameObject { get; private set; }
        public RectTransform RectTransform { get; private set; }
        public Image Image { get; private set; }
        public bool IsAlive { get; private set; } = true;

        private ParticleEmitterConfig _config;
        private Canvas _canvas;
        private RectTransform _container;
        private float _lifetime;
        private float _maxLifetime;
        private Vector2 _velocity;
        private Vector2 _position;
        private float _rotation;
        private float _rotationSpeed;
        private float _currentSize;
        private Vector2 _currentScale;
        private Color _currentColor;
        private float _elapsed = 0f;

        public UIParticle(GameObject go)
        {
            GameObject = go;
            RectTransform = go.GetComponent<RectTransform>();
            Image = go.GetComponent<Image>();
            if (Image == null) Image = go.AddComponent<Image>();
            Image.raycastTarget = false;
        }

        public void Initialize(Vector2 position, ParticleEmitterConfig config, Canvas canvas, RectTransform container, Material defaultMaterial, bool isBurst)
        {
            _config = config;
            _canvas = canvas;
            _container = container;
            _position = position;
            _elapsed = 0f;
            _maxLifetime = UnityEngine.Random.Range(config.minLifetime, config.maxLifetime);
            _lifetime = _maxLifetime;
            IsAlive = true;

            // 设置纹理
            if (config.sprite != null)
            {
                Image.sprite = config.sprite;
            }

            // 设置材质
            Image.material = config.material ?? defaultMaterial;

            // 初始大小
            _currentSize = config.startSize;
            RectTransform.sizeDelta = Vector2.one * _currentSize;

            // 初始缩放
            _currentScale = config.startScale;
            RectTransform.localScale = _currentScale;

            // 初始位置
            RectTransform.anchoredPosition = position;

            // 初始旋转
            _rotation = UnityEngine.Random.Range(config.minRotation, config.maxRotation);
            _rotationSpeed = config.rotationSpeed;
            RectTransform.localRotation = Quaternion.Euler(0, 0, _rotation);

            // 初始速度
            float angle = Mathf.Atan2(config.direction.y, config.direction.x) * Mathf.Rad2Deg;
            angle += UnityEngine.Random.Range(-config.directionSpread, config.directionSpread) * 0.5f;
            float speed = UnityEngine.Random.Range(config.minSpeed, config.maxSpeed);
            _velocity = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * speed;

            // 初始颜色
            if (config.useRandomColor && config.randomColors != null && config.randomColors.Length > 0)
            {
                _currentColor = config.randomColors[UnityEngine.Random.Range(0, config.randomColors.Length)];
            }
            else
            {
                _currentColor = config.colorGradient.Evaluate(0f);
            }
            Image.color = _currentColor;

            // 淡入
            if (config.fadeInTime > 0)
            {
                Image.color = new Color(_currentColor.r, _currentColor.g, _currentColor.b, 0f);
                Image.DOFade(1f, config.fadeInTime).SetEase(Ease.OutQuad).SetUpdate(true);
            }

            GameObject.SetActive(true);
        }

        public void Update(float dt)
        {
            _elapsed += dt;
            float t = _elapsed / _maxLifetime;

            if (t >= 1f)
            {
                IsAlive = false;
                return;
            }

            // 速度曲线
            float speedMultiplier = _config.speedCurve.Evaluate(t);
            _velocity *= _config.drag;

            // 重力
            _velocity.y += _config.gravity * dt;

            // 位置更新
            _position += _velocity * speedMultiplier * dt;
            RectTransform.anchoredPosition = _position;

            // 大小
            _currentSize = Mathf.Lerp(_config.startSize, _config.endSize, _config.sizeCurve.Evaluate(t));
            RectTransform.sizeDelta = Vector2.one * _currentSize;

            // 缩放
            _currentScale = Vector2.Lerp(_config.startScale, _config.endScale, t);
            RectTransform.localScale = _currentScale;

            // 旋转
            _rotation += _rotationSpeed * dt;
            RectTransform.localRotation = Quaternion.Euler(0, 0, _rotation);

            // 颜色
            if (!_config.useRandomColor)
            {
                _currentColor = _config.colorGradient.Evaluate(t);
                Image.color = _currentColor;
            }

            // 淡出
            if (t > 1f - _config.fadeOutTime / _maxLifetime)
            {
                float fadeT = (t - (1f - _config.fadeOutTime / _maxLifetime)) / (_config.fadeOutTime / _maxLifetime);
                var c = Image.color;
                c.a = Mathf.Lerp(1f, 0f, fadeT);
                Image.color = c;
            }
        }
    }

    /// <summary>
    /// 粒子预设工厂
    /// </summary>
    public static class UIParticlePresets
    {
        public static ParticleEmitterConfig CoinBurst => ParticleEmitterConfig.CreateCoinBurst();
        public static ParticleEmitterConfig Sparkle => ParticleEmitterConfig.CreateSparkle();
        public static ParticleEmitterConfig LevelUp =>
            new ParticleEmitterConfig
            {
                minLifetime = 1f,
                maxLifetime = 2f,
                startSize = 40f,
                endSize = 60f,
                colorGradient = CreateGradient(new Color(1f, 0.8f, 0f), new Color(1f, 0.2f, 0.8f)),
                direction = Vector2.up,
                directionSpread = 45f,
                minSpeed = 100f,
                maxSpeed = 250f,
                gravity = -100f,
                drag = 0.96f,
                rotationSpeed = 180f,
                fadeInTime = 0.1f,
                fadeOutTime = 0.8f
            };

        public static ParticleEmitterConfig DamageNumber =>
            new ParticleEmitterConfig
            {
                minLifetime = 0.8f,
                maxLifetime = 1.2f,
                startSize = 50f,
                endSize = 50f,
                colorGradient = CreateSolidGradient(Color.red),
                direction = Vector2.up,
                directionSpread = 20f,
                minSpeed = 80f,
                maxSpeed = 120f,
                gravity = -50f,
                drag = 0.98f,
                fadeInTime = 0.05f,
                fadeOutTime = 0.3f
            };

        public static ParticleEmitterConfig HealNumber =>
            new ParticleEmitterConfig
            {
                minLifetime = 0.8f,
                maxLifetime = 1.2f,
                startSize = 50f,
                endSize = 50f,
                colorGradient = CreateSolidGradient(Color.green),
                direction = Vector2.up,
                directionSpread = 20f,
                minSpeed = 80f,
                maxSpeed = 120f,
                gravity = -50f,
                drag = 0.98f,
                fadeInTime = 0.05f,
                fadeOutTime = 0.3f
            };

        public static ParticleEmitterConfig Confetti =>
            new ParticleEmitterConfig
            {
                minLifetime = 2f,
                maxLifetime = 4f,
                startSize = 20f,
                endSize = 20f,
                colorGradient = CreateGradient(Color.red, Color.blue),
                useRandomColor = true,
                randomColors = new[] { Color.red, Color.blue, Color.green, Color.yellow, Color.magenta, Color.cyan },
                direction = Vector2.up,
                directionSpread = 60f,
                minSpeed = 150f,
                maxSpeed = 400f,
                gravity = -300f,
                drag = 0.97f,
                rotationSpeed = 720f,
                fadeInTime = 0.1f,
                fadeOutTime = 0.5f
            };

        public static ParticleEmitterConfig MagicTrail(Vector2 from, Vector2 to, Color color)
        {
            return new ParticleEmitterConfig
            {
                minLifetime = 0.3f,
                maxLifetime = 0.6f,
                startSize = 15f,
                endSize = 0f,
                colorGradient = CreateSolidGradient(color),
                direction = (to - from).normalized,
                directionSpread = 15f,
                minSpeed = 50f,
                maxSpeed = 100f,
                gravity = 0f,
                drag = 0.9f,
                fadeInTime = 0.02f,
                fadeOutTime = 0.2f
            };
        }

        private static Gradient CreateGradient(Color start, Color end)
        {
            var g = new Gradient();
            g.colorKeys = new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) };
            g.alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) };
            return g;
        }

        private static Gradient CreateSolidGradient(Color color)
        {
            var g = new Gradient();
            g.colorKeys = new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) };
            g.alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) };
            return g;
        }
    }
}