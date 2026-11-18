using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;
using TomCatRunner.Common;

namespace TomCatRunner.Seasons
{
    /// <summary>
    /// 内容热更管理器
    /// 支持：启动检查更新、增量下载、进度 UI、失败重试、本地 CDN 模拟
    /// </summary>
    public class ContentUpdater : MonoSingleton<ContentUpdater>
    {
        [Header("更新配置")]
        [SerializeField] private bool _checkOnStart = true;
        [SerializeField] private bool _autoDownloadOnWifi = true;
        [SerializeField] private int _maxConcurrentDownloads = 3;
        [SerializeField] private int _maxRetryCount = 3;
        [SerializeField] private float _retryDelaySeconds = 2f;
        [SerializeField] private long _maxCacheSizeBytes = 500 * 1024 * 1024; // 500MB

        [Header("本地 CDN 模拟")]
        [SerializeField] private bool _useLocalCdnSimulator = true;
        [SerializeField] private string _localCdnPath = "LocalCDN";

        [Header("UI 引用")]
        [SerializeField] private GameObject _updateProgressPrefab;
        [SerializeField] private Transform _uiRoot;

        [Header("调试")]
        [SerializeField] private bool _debugLog = true;

        // 状态
        private UpdateProgressUI _progressUI;
        private ContentUpdateStatus _currentStatus = ContentUpdateStatus.Idle;
        private CancellationTokenSource _cancellationToken;
        private List<string> _pendingLabels = new List<string>();
        private Dictionary<string, int> _retryCounts = new Dictionary<string, int>();
        private long _totalDownloadSize = 0;
        private long _downloadedSize = 0;

        // 事件
        public event Action<ContentUpdateStatus> OnStatusChanged;
        public event Action<float> OnProgressChanged;
        public event Action<string> OnLabelProgressChanged;
        public event Action<UpdateResult> OnUpdateCompleted;
        public event Action<string, string> OnError;

        // 属性
        public ContentUpdateStatus CurrentStatus => _currentStatus;
        public float Progress => _totalDownloadSize > 0 ? (float)_downloadedSize / _totalDownloadSize : 0f;
        public long TotalDownloadSize => _totalDownloadSize;
        public long DownloadedSize => _downloadedSize;
        public bool IsUpdating => _currentStatus != ContentUpdateStatus.Idle && _currentStatus != ContentUpdateStatus.Completed;

        protected override void OnInit()
        {
            base.OnInit();

            // 初始化 Addressables
            Addressables.InitializeAsync().Completed += handle =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    if (_checkOnStart)
                    {
                        CheckForUpdatesAsync().Forget();
                    }
                }
                else
                {
                    Debug.LogError("[ContentUpdater] Addressables initialization failed");
                }
            };
        }

        /// <summary>
        /// 检查更新
        /// </summary>
        public async UniTask<UpdateCheckResult> CheckForUpdatesAsync(string[] labels = null)
        {
            if (_currentStatus != ContentUpdateStatus.Idle)
            {
                return new UpdateCheckResult { hasUpdates = false, error = "Already updating" };
            }

            SetStatus(ContentUpdateStatus.CheckingUpdates);
            _cancellationToken = new CancellationTokenSource();

            try
            {
                // 获取远程目录
                var catalogResult = await Addressables.CheckForCatalogUpdates(false).Task;
                var hasCatalogUpdate = catalogResult.Count > 0;

                // 检查特定标签的更新
                var labelsToCheck = labels ?? new[] { "season", "ui", "chunks" };
                _pendingLabels.Clear();

                foreach (var label in labelsToCheck)
                {
                    var sizeResult = await Addressables.GetDownloadSizeAsync(label).Task;
                    if (sizeResult > 0)
                    {
                        _pendingLabels.Add(label);
                        _totalDownloadSize += sizeResult;
                    }
                }

                var result = new UpdateCheckResult
                {
                    hasUpdates = _pendingLabels.Count > 0 || hasCatalogUpdate,
                    catalogUpdate = hasCatalogUpdate,
                    labelsToUpdate = _pendingLabels.ToArray(),
                    totalSizeBytes = _totalDownloadSize,
                    estimatedTimeSeconds = EstimateDownloadTime(_totalDownloadSize)
                };

                if (result.hasUpdates)
                {
                    SetStatus(ContentUpdateStatus.UpdatesAvailable);
                    ShowProgressUI();
                }
                else
                {
                    SetStatus(ContentUpdateStatus.Completed);
                }

                if (_debugLog) Debug.Log($"[ContentUpdater] Check complete: {result.hasUpdates}, size: {FormatBytes(_totalDownloadSize)}");
                return result;
            }
            catch (Exception e)
            {
                var errorResult = new UpdateCheckResult { hasUpdates = false, error = e.Message };
                SetStatus(ContentUpdateStatus.Error);
                OnError?.Invoke("CheckForUpdates", e.Message);
                return errorResult;
            }
        }

        /// <summary>
        /// 开始下载更新
        /// </summary>
        public async UniTask<UpdateResult> DownloadUpdatesAsync(bool showUI = true)
        {
            if (_pendingLabels.Count == 0)
            {
                return new UpdateResult { success = true, message = "No updates to download" };
            }

            if (showUI) ShowProgressUI();
            SetStatus(ContentUpdateStatus.Downloading);
            _downloadedSize = 0;
            _retryCounts.Clear();

            try
            {
                // 更新目录
                if (await UpdateCatalogAsync() == false)
                {
                    throw new Exception("Failed to update catalog");
                }

                // 下载每个标签的内容
                foreach (var label in _pendingLabels)
                {
                    if (_cancellationToken.Token.IsCancellationRequested) break;

                    await DownloadLabelAsync(label);
                }

                // 清理缓存
                await CleanupCacheAsync();

                var result = new UpdateResult
                {
                    success = true,
                    message = "Update completed",
                    downloadedBytes = _downloadedSize,
                    labelsUpdated = _pendingLabels.ToArray()
                };

                SetStatus(ContentUpdateStatus.Completed);
                OnUpdateCompleted?.Invoke(result);

                if (_debugLog) Debug.Log($"[ContentUpdater] Download completed: {FormatBytes(_downloadedSize)}");
                return result;
            }
            catch (Exception e)
            {
                var result = new UpdateResult { success = false, message = e.Message };
                SetStatus(ContentUpdateStatus.Error);
                OnError?.Invoke("DownloadUpdates", e.Message);
                return result;
            }
        }

        private async UniTask<bool> UpdateCatalogAsync()
        {
            SetStatus(ContentUpdateStatus.UpdatingCatalog);

            try
            {
                var handle = Addressables.CheckForCatalogUpdates(true);
                await handle.Task;

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    var catalogs = handle.Result;
                    if (catalogs.Count > 0)
                    {
                        var updateHandle = Addressables.UpdateCatalogs(catalogs, false);
                        await updateHandle.Task;
                        return updateHandle.Status == AsyncOperationStatus.Succeeded;
                    }
                }
                return true; // 无需更新
            }
            catch (Exception e)
            {
                Debug.LogError($"[ContentUpdater] Catalog update failed: {e.Message}");
                return false;
            }
        }

        private async UniTask DownloadLabelAsync(string label)
        {
            SetStatus(ContentUpdateStatus.Downloading);
            OnLabelProgressChanged?.Invoke(label);

            int retryCount = 0;
            bool success = false;

            while (retryCount <= _maxRetryCount && !success)
            {
                if (_cancellationToken.Token.IsCancellationRequested) return;

                try
                {
                    var handle = Addressables.DownloadDependenciesAsync(label, Addressables.MergeMode.Union, false);

                    // 监控进度
                    while (!handle.IsDone)
                    {
                        if (_cancellationToken.Token.IsCancellationRequested) return;

                        var progress = handle.GetDownloadStatus();
                        _downloadedSize = _totalDownloadSize - (long)(progress.Percent * _totalDownloadSize);
                        OnProgressChanged?.Invoke(Progress);

                        await UniTask.Delay(100, cancellationToken: _cancellationToken.Token);
                    }

                    if (handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        success = true;
                        _downloadedSize += handle.GetDownloadStatus().DownloadedBytes;
                        OnProgressChanged?.Invoke(Progress);

                        if (_debugLog) Debug.Log($"[ContentUpdater] Downloaded label: {label}");
                    }
                    else
                    {
                        throw handle.OperationException ?? new Exception("Download failed");
                    }
                }
                catch (Exception e)
                {
                    retryCount++;
                    _retryCounts[label] = retryCount;

                    if (retryCount <= _maxRetryCount)
                    {
                        if (_debugLog) Debug.LogWarning($"[ContentUpdater] Retry {retryCount}/{_maxRetryCount} for {label}: {e.Message}");
                        await UniTask.Delay(TimeSpan.FromSeconds(_retryDelaySeconds * retryCount), cancellationToken: _cancellationToken.Token);
                    }
                    else
                    {
                        throw new Exception($"Failed to download {label} after {_maxRetryCount} retries: {e.Message}");
                    }
                }
            }
        }

        private async UniTask CleanupCacheAsync()
        {
            SetStatus(ContentUpdateStatus.CleaningCache);

            try
            {
                var cacheSize = Caching.currentCacheForWriting.spaceOccupied;
                if (cacheSize > _maxCacheSizeBytes)
                {
                    Caching.ClearCache();
                    if (_debugLog) Debug.Log($"[ContentUpdater] Cache cleaned: {FormatBytes(cacheSize)}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ContentUpdater] Cache cleanup failed: {e.Message}");
            }
        }

        /// <summary>
        /// 取消当前更新
        /// </summary>
        public void CancelUpdate()
        {
            _cancellationToken?.Cancel();
            SetStatus(ContentUpdateStatus.Idle);
            HideProgressUI();

            if (_debugLog) Debug.Log("[ContentUpdater] Update cancelled");
        }

        /// <summary>
        /// 设置标签优先级下载
        /// </summary>
        public async UniTask<UpdateResult> DownloadPriorityLabelsAsync(string[] labels)
        {
            _pendingLabels = labels.ToList();
            _totalDownloadSize = 0;

            foreach (var label in labels)
            {
                var size = await Addressables.GetDownloadSizeAsync(label).Task;
                _totalDownloadSize += size;
            }

            return await DownloadUpdatesAsync();
        }

        /// <summary>
        /// 预加载关键资源
        /// </summary>
        public async UniTask PreloadCriticalAssetsAsync()
        {
            var criticalLabels = new[] { "core", "ui" };

            foreach (var label in criticalLabels)
            {
                var handle = Addressables.LoadAssetsAsync<GameObject>(label, null);
                await handle.Task;
                Addressables.Release(handle);
            }

            if (_debugLog) Debug.Log("[ContentUpdater] Critical assets preloaded");
        }

        // ===================== 本地 CDN 模拟器 =====================

        /// <summary>
        /// 启动本地 CDN 模拟器
        /// </summary>
        public void StartLocalCdnSimulator(int port = 8080)
        {
            if (!_useLocalCdnSimulator) return;

            var cdnPath = Path.Combine(Application.streamingAssetsPath, _localCdnPath);
            if (!Directory.Exists(cdnPath))
            {
                Directory.CreateDirectory(cdnPath);
            }

            // 这里可以启动一个简单的 HTTP 服务器
            // 实际项目中建议使用轻量级 HTTP 服务器库
            Debug.Log($"[ContentUpdater] Local CDN simulator path: {cdnPath}");
        }

        /// <summary>
        /// 将 Addressables 构建输出复制到本地 CDN 目录
        /// </summary>
        [ContextMenu("Copy Build To Local CDN")]
        public void CopyBuildToLocalCdn()
        {
            var buildPath = Path.Combine(Application.dataPath, "../Library/com.unity.addressables/aa/Build");
            var cdnPath = Path.Combine(Application.streamingAssetsPath, _localCdnPath);

            if (Directory.Exists(buildPath))
            {
                CopyDirectory(buildPath, cdnPath);
#if UNITY_EDITOR
                UnityEditor.AssetDatabase.Refresh();
#endif
                Debug.Log($"[ContentUpdater] Build copied to Local CDN: {cdnPath}");
            }
        }

        private void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (var file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
            }
            foreach (var dir in Directory.GetDirectories(source))
            {
                CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
            }
        }

        // ===================== UI 管理 =====================

        private void ShowProgressUI()
        {
            if (_progressUI == null && _updateProgressPrefab != null)
            {
                var instance = Instantiate(_updateProgressPrefab, _uiRoot);
                _progressUI = instance.GetComponent<UpdateProgressUI>();
                _progressUI.Initialize(this);
            }
            _progressUI?.Show();
        }

        private void HideProgressUI()
        {
            _progressUI?.Hide();
        }

        private void SetStatus(ContentUpdateStatus status)
        {
            _currentStatus = status;
            OnStatusChanged?.Invoke(status);
        }

        private float EstimateDownloadTime(long bytes)
        {
            // 假设平均 5MB/s
            return bytes / (5f * 1024 * 1024);
        }

        private string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            int i = 0;
            double size = bytes;
            while (size >= 1024 && i < units.Length - 1)
            {
                size /= 1024;
                i++;
            }
            return $"{size:F1} {units[i]}";
        }

        protected override void OnDispose()
        {
            CancelUpdate();
            _cancellationToken?.Dispose();
            base.OnDispose();
        }
    }

    // ===================== 数据结构 =====================

    public enum ContentUpdateStatus
    {
        Idle,
        CheckingUpdates,
        UpdatesAvailable,
        UpdatingCatalog,
        Downloading,
        CleaningCache,
        Completed,
        Error
    }

    [Serializable]
    public class UpdateCheckResult
    {
        public bool hasUpdates;
        public bool catalogUpdate;
        public string[] labelsToUpdate;
        public long totalSizeBytes;
        public float estimatedTimeSeconds;
        public string error;
    }

    [Serializable]
    public class UpdateResult
    {
        public bool success;
        public string message;
        public long downloadedBytes;
        public string[] labelsUpdated;
    }

    // ===================== 进度 UI 组件 =====================

    /// <summary>
    /// 更新进度 UI
    /// </summary>
    public class UpdateProgressUI : MonoBehaviour
    {
        [Header("UI 引用")]
        [SerializeField] private TMPro.TextMeshProUGUI _statusText;
        [SerializeField] private TMPro.TextMeshProUGUI _progressText;
        [SerializeField] private TMPro.TextMeshProUGUI _sizeText;
        [SerializeField] private TMPro.TextMeshProUGUI _speedText;
        [SerializeField] private UnityEngine.UI.Slider _progressBar;
        [SerializeField] private UnityEngine.UI.Button _cancelButton;
        [SerializeField] private UnityEngine.UI.Button _retryButton;
        [SerializeField] private GameObject _errorPanel;
        [SerializeField] private TMPro.TextMeshProUGUI _errorText;

        private ContentUpdater _updater;
        private float _lastUpdateTime;
        private long _lastDownloadedSize;

        public void Initialize(ContentUpdater updater)
        {
            _updater = updater;

            _updater.OnStatusChanged += OnStatusChanged;
            _updater.OnProgressChanged += OnProgressChanged;
            _updater.OnLabelProgressChanged += OnLabelProgress;
            _updater.OnUpdateCompleted += OnUpdateCompleted;
            _updater.OnError += OnError;

            _cancelButton?.onClick.AddListener(() => _updater.CancelUpdate());
            _retryButton?.onClick.AddListener(() => RetryUpdate());

            gameObject.SetActive(false);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            _lastUpdateTime = Time.realtimeSinceStartup;
            _lastDownloadedSize = 0;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void OnStatusChanged(ContentUpdateStatus status)
        {
            var statusTexts = new Dictionary<ContentUpdateStatus, string>
            {
                [ContentUpdateStatus.CheckingUpdates] = "检查更新中...",
                [ContentUpdateStatus.UpdatesAvailable] = "发现更新",
                [ContentUpdateStatus.UpdatingCatalog] = "更新目录...",
                [ContentUpdateStatus.Downloading] = "下载中...",
                [ContentUpdateStatus.CleaningCache] = "清理缓存...",
                [ContentUpdateStatus.Completed] = "更新完成",
                [ContentUpdateStatus.Error] = "更新失败"
            };

            _statusText.text = statusTexts.GetValueOrDefault(status, status.ToString());

            _cancelButton?.gameObject.SetActive(status == ContentUpdateStatus.Downloading || status == ContentUpdateStatus.UpdatingCatalog);
            _retryButton?.gameObject.SetActive(status == ContentUpdateStatus.Error);
            _errorPanel?.SetActive(status == ContentUpdateStatus.Error);
        }

        private void OnProgressChanged(float progress)
        {
            _progressBar.value = progress;
            _progressText.text = $"{progress * 100:F1}%";

            // 计算下载速度
            float now = Time.realtimeSinceStartup;
            float deltaTime = now - _lastUpdateTime;
            long deltaSize = _updater.DownloadedSize - _lastDownloadedSize;

            if (deltaTime > 0.5f)
            {
                float speed = deltaSize / deltaTime;
                _speedText.text = FormatSpeed(speed);
                _lastUpdateTime = now;
                _lastDownloadedSize = _updater.DownloadedSize;
            }

            _sizeText.text = $"{FormatBytes(_updater.DownloadedSize)} / {FormatBytes(_updater.TotalDownloadSize)}";
        }

        private void OnLabelProgress(string label)
        {
            _statusText.text = $"下载: {label}...";
        }

        private void OnUpdateCompleted(UpdateResult result)
        {
            if (result.success)
            {
                _statusText.text = "更新完成！";
                _progressBar.value = 1f;
                _progressText.text = "100%";
            }
            else
            {
                _statusText.text = "更新失败";
                _errorText.text = result.message;
                _errorPanel.SetActive(true);
            }

            // 延迟隐藏
            Invoke(nameof(Hide), 3f);
        }

        private void OnError(string operation, string error)
        {
            _errorText.text = $"{operation}: {error}";
            _errorPanel.SetActive(true);
        }

        private void RetryUpdate()
        {
            _errorPanel.SetActive(false);
            _updater.DownloadUpdatesAsync().Forget();
        }

        private string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            int i = 0;
            double size = bytes;
            while (size >= 1024 && i < units.Length - 1)
            {
                size /= 1024;
                i++;
            }
            return $"{size:F1} {units[i]}";
        }

        private string FormatSpeed(float bytesPerSecond)
        {
            return FormatBytes((long)bytesPerSecond) + "/s";
        }

        private void OnDestroy()
        {
            if (_updater != null)
            {
                _updater.OnStatusChanged -= OnStatusChanged;
                _updater.OnProgressChanged -= OnProgressChanged;
                _updater.OnLabelProgressChanged -= OnLabelProgress;
                _updater.OnUpdateCompleted -= OnUpdateCompleted;
                _updater.OnError -= OnError;
            }
        }
    }
}