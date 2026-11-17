using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Cysharp.Threading.Tasks;
using TomCatRunner.Core;
using TomCatRunner.Common;

namespace TomCatRunner.Core.AI
{
    /// <summary>
    /// AutoRunnerBot - AI agent for validating procedural levels
    /// Simulates player behavior to test level playability, difficulty, and fairness
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class AutoRunnerBot : MonoBehaviour
    {
        [Header("Bot Configuration")]
        [SerializeField] private BotDifficulty _difficulty = BotDifficulty.Normal;
        [SerializeField] private float _reactionTime = 0.15f; // 人类平均反应时间
        [SerializeField] private float _laneChangeSpeed = 10f;
        [SerializeField] private float _jumpHeight = 3f;
        [SerializeField] private float _slideDuration = 1f;
        [SerializeField] private float _attackRange = 2f;

        [Header("Sensors")]
        [SerializeField] private float _lookAheadDistance = 15f;
        [SerializeField] private float _sensorHeight = 1f;
        [SerializeField] private LayerMask _obstacleLayer;
        [SerializeField] private LayerMask _collectibleLayer;

        [Header("Validation Settings")]
        [SerializeField] private bool _enableValidation = true;
        [SerializeField] private float _validationTimeout = 60f;
        [SerializeField] private int _maxRetries = 3;

        [Header("Debug")]
        [SerializeField] private bool _drawGizmos = true;
        [SerializeField] private bool _logDecisions = false;

        // 组件引用
        private CharacterController _controller;
        private LevelStreamer _levelStreamer;
        private ProceduralLevelGenerator _levelGenerator;

        // 状态
        private BotState _currentState = BotState.Idle;
        private int _currentLane = 1; // 0=左, 1=中, 2=右
        private float _targetLanePosition;
        private float _verticalVelocity;
        private bool _isGrounded;
        private bool _isSliding;
        private float _slideTimer;

        // 感知
        private List<ObstacleInfo> _detectedObstacles = new List<ObstacleInfo>();
        private List<CollectibleInfo> _detectedCollectibles = new List<CollectibleInfo>();

        // 决策
        private Queue<BotAction> _actionQueue = new Queue<BotAction>();
        private BotAction _currentAction;
        private float _actionTimer;
        private float _lastDecisionTime;

        // 统计
        private ValidationResult _validationResult;
        private float _startTime;
        private float _distanceTraveled;
        private int _coinsCollected;
        private int _obstaclesAvoided;
        private int _obstaclesHit;
        private int _deaths;
        private List<DecisionLog> _decisionLog = new List<DecisionLog>();

        public enum BotDifficulty { Easy, Normal, Hard, Expert }
        public enum BotState { Idle, Running, Jumping, Sliding, Attacking, Dead, Finished }
        public enum BotActionType { None, ChangeLaneLeft, ChangeLaneRight, Jump, Slide, Attack, Wait }

        public struct BotAction
        {
            public BotActionType Type;
            public float Duration;
            public float StartTime;
            public int TargetLane;
        }

        public struct ObstacleInfo
        {
            public Vector3 Position;
            public Vector3 Size;
            public ObstacleType Type;
            public float DistanceToPlayer;
            public int Lane;
        }

        public struct CollectibleInfo
        {
            public Vector3 Position;
            public CollectibleType Type;
            public float DistanceToPlayer;
            public int Lane;
        }

        public enum ObstacleType { Low, High, Wide, Moving, Projectile, Boss }
        public enum CollectibleType { Coin, Gem, Powerup, Health, Key }

        public class ValidationResult
        {
            public bool IsPlayable = true;
            public float CompletionRate = 0f; // 0-1
            public float AverageDifficulty = 0f; // 0-1
            public float FairnessScore = 1f; // 0-1, 1=完全公平
            public int TotalObstacles = 0;
            public int UnavoidableObstacles = 0;
            public int UnreachableCollectibles = 0;
            public float CompletionTime = 0f;
            public List<string> Issues = new List<string>();
            public Dictionary<string, float> Metrics = new Dictionary<string, float>();
        }

        public class DecisionLog
        {
            public float Time;
            public float Distance;
            public BotActionType Action;
            public string Reason;
            public ObstacleInfo? TriggerObstacle;
            public bool Success;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _targetLanePosition = GetLanePosition(_currentLane);
        }

        private void Start()
        {
            _levelStreamer = FindObjectOfType<LevelStreamer>();
            _levelGenerator = FindObjectOfType<ProceduralLevelGenerator>();

            if (_enableValidation)
            {
                StartValidation().Forget();
            }
        }

        private async UniTaskVoid StartValidation()
        {
            _validationResult = new ValidationResult();
            _startTime = Time.time;
            _currentState = BotState.Running;

            await RunValidationLoop();

            _validationResult.CompletionTime = Time.time - _startTime;
            LogValidationResult();
        }

        private async UniTask RunValidationLoop()
        {
            float timeoutTime = Time.time + _validationTimeout;

            while (_currentState != BotState.Finished && _currentState != BotState.Dead && Time.time < timeoutTime)
            {
                UpdateSensors();
                MakeDecision();
                ExecuteAction();
                UpdateMovement();

                _distanceTraveled = transform.position.z;

                // 检查是否完成关卡
                if (HasReachedEnd())
                {
                    _currentState = BotState.Finished;
                    break;
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            if (Time.time >= timeoutTime)
            {
                _validationResult.IsPlayable = false;
                _validationResult.Issues.Add("Validation timeout");
            }

            _currentState = BotState.Finished;
        }

        private void UpdateSensors()
        {
            _detectedObstacles.Clear();
            _detectedCollectibles.Clear();

            // 前方射线检测
            Vector3 origin = transform.position + Vector3.up * _sensorHeight;
            Vector3 forward = transform.forward;

            // 检测障碍物
            RaycastHit[] hits = Physics.SphereCastAll(origin, 2f, forward, _lookAheadDistance, _obstacleLayer);
            foreach (var hit in hits)
            {
                var obstacle = AnalyzeObstacle(hit);
                if (obstacle != null)
                {
                    _detectedObstacles.Add(obstacle.Value);
                }
            }

            // 检测收集物
            RaycastHit[] collectibleHits = Physics.SphereCastAll(origin, 3f, forward, _lookAheadDistance, _collectibleLayer);
            foreach (var hit in collectibleHits)
            {
                var collectible = AnalyzeCollectible(hit);
                if (collectible != null)
                {
                    _detectedCollectibles.Add(collectible.Value);
                }
            }

            // 按距离排序
            _detectedObstacles.Sort((a, b) => a.DistanceToPlayer.CompareTo(b.DistanceToPlayer));
            _detectedCollectibles.Sort((a, b) => a.DistanceToPlayer.CompareTo(b.DistanceToPlayer));
        }

        private ObstacleInfo? AnalyzeObstacle(RaycastHit hit)
        {
            var obstacle = hit.collider.GetComponent<LevelObstacle>();
            if (obstacle == null) return null;

            int lane = GetLaneFromPosition(hit.transform.position.x);

            return new ObstacleInfo
            {
                Position = hit.transform.position,
                Size = hit.collider.bounds.size,
                Type = obstacle.ObstacleType,
                DistanceToPlayer = hit.distance,
                Lane = lane
            };
        }

        private CollectibleInfo? AnalyzeCollectible(RaycastHit hit)
        {
            var collectible = hit.collider.GetComponent<LevelCollectible>();
            if (collectible == null) return null;

            int lane = GetLaneFromPosition(hit.transform.position.x);

            return new CollectibleInfo
            {
                Position = hit.transform.position,
                Type = collectible.CollectibleType,
                DistanceToPlayer = hit.distance,
                Lane = lane
            };
        }

        private void MakeDecision()
        {
            if (Time.time - _lastDecisionTime < _reactionTime) return;

            // 优先处理紧急障碍
            var urgentObstacle = GetUrgentObstacle();
            if (urgentObstacle.HasValue)
            {
                DecideObstacleAvoidance(urgentObstacle.Value);
                _lastDecisionTime = Time.time;
                return;
            }

            // 收集物决策
            var bestCollectible = GetBestCollectible();
            if (bestCollectible.HasValue && CanReachCollectible(bestCollectible.Value))
            {
                DecideCollectiblePickup(bestCollectible.Value);
                _lastDecisionTime = Time.time;
                return;
            }

            // 默认保持中央车道
            if (_currentLane != 1 && _detectedObstacles.Count == 0)
            {
                QueueAction(new BotAction { Type = _currentLane > 1 ? BotActionType.ChangeLaneLeft : BotActionType.ChangeLaneRight, TargetLane = 1 });
            }
        }

        private ObstacleInfo? GetUrgentObstacle()
        {
            foreach (var obs in _detectedObstacles)
            {
                float timeToReach = obs.DistanceToPlayer / GetCurrentSpeed();
                float reactionWindow = _reactionTime + GetActionTimeForObstacle(obs.Type);

                if (timeToReach < reactionWindow && obs.Lane == _currentLane)
                {
                    return obs;
                }
            }
            return null;
        }

        private CollectibleInfo? GetBestCollectible()
        {
            foreach (var col in _detectedCollectibles)
            {
                if (col.Lane == _currentLane || CanChangeLaneInTime(col.DistanceToPlayer))
                {
                    return col;
                }
            }
            return null;
        }

        private bool CanReachCollectible(CollectibleInfo collectible)
        {
            float timeToReach = collectible.DistanceToPlayer / GetCurrentSpeed();
            float laneChangeTime = Mathf.Abs(collectible.Lane - _currentLane) * (3f / _laneChangeSpeed);
            return timeToReach > laneChangeTime + 0.1f;
        }

        private void DecideObstacleAvoidance(ObstacleInfo obstacle)
        {
            BotActionType action = DetermineBestAction(obstacle);

            var logEntry = new DecisionLog
            {
                Time = Time.time - _startTime,
                Distance = _distanceTraveled,
                Action = action,
                Reason = $"Avoid {obstacle.Type} in lane {obstacle.Lane}",
                TriggerObstacle = obstacle,
                Success = false // 将在执行后更新
            };
            _decisionLog.Add(logEntry);

            QueueAction(new BotAction { Type = action, Duration = GetActionTimeForObstacle(obstacle.Type) });
        }

        private BotActionType DetermineBestAction(ObstacleInfo obstacle)
        {
            switch (obstacle.Type)
            {
                case ObstacleType.Low:
                    return BotActionType.Jump;
                case ObstacleType.High:
                    return BotActionType.Slide;
                case ObstacleType.Wide:
                    // 尝试换道
                    if (_currentLane > 0 && IsLaneClear(_currentLane - 1))
                        return BotActionType.ChangeLaneLeft;
                    if (_currentLane < 2 && IsLaneClear(_currentLane + 1))
                        return BotActionType.ChangeLaneRight;
                    return BotActionType.Jump; // 无路可走，强行跳跃
                case ObstacleType.Moving:
                    // 预测移动路径
                    return PredictAndAvoid(obstacle);
                case ObstacleType.Projectile:
                    return BotActionType.Slide;
                case ObstacleType.Boss:
                    return BotActionType.Attack;
                default:
                    return BotActionType.Jump;
            }
        }

        private BotActionType PredictAndAvoid(ObstacleInfo obstacle)
        {
            // 简化的预测逻辑
            var movingObs = obstacle; // 假设有 MovingObstacle 组件
            // 这里应该根据移动方向预测
            if (_currentLane > 0 && IsLaneClear(_currentLane - 1))
                return BotActionType.ChangeLaneLeft;
            if (_currentLane < 2 && IsLaneClear(_currentLane + 1))
                return BotActionType.ChangeLaneRight;
            return BotActionType.Jump;
        }

        private void DecideCollectiblePickup(CollectibleInfo collectible)
        {
            if (collectible.Lane != _currentLane)
            {
                BotActionType laneAction = collectible.Lane < _currentLane ? BotActionType.ChangeLaneLeft : BotActionType.ChangeLaneRight;
                QueueAction(new BotAction { Type = laneAction, TargetLane = collectible.Lane });
            }
        }

        private void ExecuteAction()
        {
            if (_currentAction.Type == BotActionType.None && _actionQueue.Count > 0)
            {
                _currentAction = _actionQueue.Dequeue();
                _actionTimer = 0f;
            }

            if (_currentAction.Type != BotActionType.None)
            {
                _actionTimer += Time.deltaTime;

                switch (_currentAction.Type)
                {
                    case BotActionType.ChangeLaneLeft:
                        ExecuteLaneChange(-1);
                        break;
                    case BotActionType.ChangeLaneRight:
                        ExecuteLaneChange(1);
                        break;
                    case BotActionType.Jump:
                        ExecuteJump();
                        break;
                    case BotActionType.Slide:
                        ExecuteSlide();
                        break;
                    case BotActionType.Attack:
                        ExecuteAttack();
                        break;
                }

                if (_actionTimer >= _currentAction.Duration)
                {
                    _currentAction = new BotAction { Type = BotActionType.None };
                }
            }
        }

        private void ExecuteLaneChange(int direction)
        {
            int targetLane = Mathf.Clamp(_currentLane + direction, 0, 2);
            _targetLanePosition = GetLanePosition(targetLane);
            _currentLane = targetLane;
        }

        private void ExecuteJump()
        {
            if (_isGrounded && _currentState != BotState.Jumping)
            {
                _verticalVelocity = Mathf.Sqrt(2f * Physics.gravity.magnitude * _jumpHeight);
                _currentState = BotState.Jumping;
            }
        }

        private void ExecuteSlide()
        {
            if (_isGrounded && _currentState != BotState.Sliding)
            {
                _isSliding = true;
                _slideTimer = _slideDuration;
                _currentState = BotState.Sliding;
                _controller.height = 1f; // 降低高度
            }
        }

        private void ExecuteAttack()
        {
            _currentState = BotState.Attacking;
            // 攻击逻辑
        }

        private void UpdateMovement()
        {
            // 水平移动（换道）
            float currentX = transform.position.x;
            float newX = Mathf.MoveTowards(currentX, _targetLanePosition, _laneChangeSpeed * Time.deltaTime);
            Vector3 move = new Vector3(newX - currentX, 0, 0);

            // 前进移动
            float speed = GetCurrentSpeed();
            move.z = speed * Time.deltaTime;

            // 垂直移动
            if (_currentState == BotState.Jumping)
            {
                _verticalVelocity += Physics.gravity.y * Time.deltaTime;
                move.y = _verticalVelocity * Time.deltaTime;

                if (_controller.isGrounded && _verticalVelocity <= 0)
                {
                    _verticalVelocity = 0;
                    _currentState = BotState.Running;
                }
            }
            else if (_currentState == BotState.Sliding)
            {
                _slideTimer -= Time.deltaTime;
                if (_slideTimer <= 0)
                {
                    _isSliding = false;
                    _controller.height = 2f;
                    _currentState = BotState.Running;
                }
            }
            else
            {
                _verticalVelocity = -0.5f; // 微小下压保持贴地
                move.y = _verticalVelocity * Time.deltaTime;
            }

            _isGrounded = _controller.isGrounded;
            _controller.Move(move);

            // 更新最后一次决策的成功状态
            if (_decisionLog.Count > 0)
            {
                var lastLog = _decisionLog[_decisionLog.Count - 1];
                if (!_actionQueue.Any() && _currentAction.Type == BotActionType.None)
                {
                    lastLog.Success = true;
                    if (lastLog.TriggerObstacle.HasValue) _obstaclesAvoided++;
                }
            }
        }

        private float GetCurrentSpeed()
        {
            return _difficulty switch
            {
                BotDifficulty.Easy => 15f,
                BotDifficulty.Normal => 20f,
                BotDifficulty.Hard => 25f,
                BotDifficulty.Expert => 30f,
                _ => 20f
            };
        }

        private float GetActionTimeForObstacle(ObstacleType type)
        {
            return type switch
            {
                ObstacleType.Low => 0.5f,
                ObstacleType.High => 0.6f,
                ObstacleType.Wide => 0.8f,
                ObstacleType.Moving => 0.7f,
                ObstacleType.Projectile => 0.4f,
                ObstacleType.Boss => 1f,
                _ => 0.5f
            };
        }

        private bool IsLaneClear(int lane)
        {
            foreach (var obs in _detectedObstacles)
            {
                if (obs.Lane == lane && obs.DistanceToPlayer < _lookAheadDistance * 0.5f)
                    return false;
            }
            return true;
        }

        private bool CanChangeLaneInTime(float distance)
        {
            float timeToReach = distance / GetCurrentSpeed();
            return timeToReach > (3f / _laneChangeSpeed) + _reactionTime;
        }

        private float GetLanePosition(int lane)
        {
            return (lane - 1) * 3f; // 车道宽度 3 单位
        }

        private int GetLaneFromPosition(float x)
        {
            return Mathf.Clamp(Mathf.RoundToInt(x / 3f + 1), 0, 2);
        }

        private bool HasReachedEnd()
        {
            // 检查是否到达关卡终点
            return _distanceTraveled > (_levelGenerator ? _levelGenerator.LastGeneratedLength : 1000f);
        }

        private void LogValidationResult()
        {
            _validationResult.CompletionRate = _distanceTraveled / (_levelGenerator ? _levelGenerator.LastGeneratedLength : 1000f);
            _validationResult.TotalObstacles = _obstaclesAvoided + _obstaclesHit;
            _validationResult.FairnessScore = _obstaclesAvoided > 0 ? (float)_obstaclesAvoided / (_obstaclesAvoided + _obstaclesHit) : 1f;

            if (_validationResult.FairnessScore < 0.8f)
            {
                _validationResult.IsPlayable = false;
                _validationResult.Issues.Add($"Low fairness score: {_validationResult.FairnessScore:P0}");
            }

            if (_deaths > 0)
            {
                _validationResult.IsPlayable = false;
                _validationResult.Issues.Add($"{_deaths} deaths during validation");
            }

            _validationResult.Metrics["DistanceTraveled"] = _distanceTraveled;
            _validationResult.Metrics["CoinsCollected"] = _coinsCollected;
            _validationResult.Metrics["ObstaclesAvoided"] = _obstaclesAvoided;
            _validationResult.Metrics["ObstaclesHit"] = _obstaclesHit;
            _validationResult.Metrics["Deaths"] = _deaths;
            _validationResult.Metrics["DecisionCount"] = _decisionLog.Count;

            Debug.Log($"[AutoRunnerBot] Validation Complete:\n" +
                     $"  Playable: {_validationResult.IsPlayable}\n" +
                     $"  Completion: {_validationResult.CompletionRate:P1}\n" +
                     $"  Fairness: {_validationResult.FairnessScore:P1}\n" +
                     $"  Time: {_validationResult.CompletionTime:F1}s\n" +
                     $"  Issues: {string.Join(", ", _validationResult.Issues)}");
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.collider.CompareTag("Obstacle"))
            {
                _obstaclesHit++;
                HandleObstacleHit(hit);
            }
            else if (hit.collider.CompareTag("Collectible"))
            {
                HandleCollectibleHit(hit);
            }
            else if (hit.collider.CompareTag("KillZone"))
            {
                HandleDeath();
            }
        }

        private void HandleObstacleHit(ControllerColliderHit hit)
        {
            _deaths++;
            _currentState = BotState.Dead;

            _validationResult.Issues.Add($"Hit obstacle at {_distanceTraveled:F1}m: {hit.collider.name}");

            // 重试逻辑
            if (_deaths < _maxRetries)
            {
                Respawn().Forget();
            }
        }

        private void HandleCollectibleHit(ControllerColliderHit hit)
        {
            _coinsCollected++;
            Destroy(hit.collider.gameObject);
        }

        private void HandleDeath()
        {
            _deaths++;
            _currentState = BotState.Dead;

            if (_deaths < _maxRetries)
            {
                Respawn().Forget();
            }
        }

        private async UniTaskVoid Respawn()
        {
            await UniTask.Delay(1000);
            transform.position = new Vector3(0, 1, _distanceTraveled - 50);
            _currentState = BotState.Running;
            _verticalVelocity = 0;
            _controller.height = 2f;
        }

        private void OnDrawGizmos()
        {
            if (!_drawGizmos) return;

            // 绘制传感器范围
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * _sensorHeight, 2f);

            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position + Vector3.up * _sensorHeight, transform.forward * _lookAheadDistance);

            // 绘制检测到的障碍物
            foreach (var obs in _detectedObstacles)
            {
                Gizmos.color = obs.Lane == _currentLane ? Color.red : Color.yellow;
                Gizmos.DrawWireCube(obs.Position, obs.Size);
            }

            // 绘制目标车道
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(new Vector3(_targetLanePosition, transform.position.y + 1, transform.position.z + 2), 0.5f);
        }

        // 公共 API
        public ValidationResult GetValidationResult() => _validationResult;
        public List<DecisionLog> GetDecisionLog() => _decisionLog;
        public void SetDifficulty(BotDifficulty difficulty) => _difficulty = difficulty;
        public void EnableValidation(bool enable) => _enableValidation = enable;
    }

    // 关卡障碍物组件
    public class LevelObstacle : MonoBehaviour
    {
        public AutoRunnerBot.ObstacleType ObstacleType = AutoRunnerBot.ObstacleType.Low;
        public bool IsMoving = false;
        public Vector3 MoveDirection = Vector3.right;
        public float MoveSpeed = 2f;
        public float MoveRange = 3f;
        private Vector3 _startPos;

        private void Start()
        {
            _startPos = transform.position;
        }

        private void Update()
        {
            if (IsMoving)
            {
                float offset = Mathf.PingPong(Time.time * MoveSpeed, MoveRange * 2) - MoveRange;
                transform.position = _startPos + MoveDirection * offset;
            }
        }
    }

    // 关卡收集物组件
    public class LevelCollectible : MonoBehaviour
    {
        public AutoRunnerBot.CollectibleType CollectibleType = AutoRunnerBot.CollectibleType.Coin;
        public int Value = 1;

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                // 收集逻辑
                Destroy(gameObject);
            }
        }
    }
}