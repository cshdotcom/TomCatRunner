using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TomCatRunner.Editor.CI
{
    /// <summary>
    /// CI/CD 构建脚本 - 供 GitHub Actions 调用
    /// 支持 Windows Standalone、WebGL、Addressables 构建
    /// </summary>
    public class CIBuildScript
    {
        private const string PRODUCT_NAME = "TomCatRunner";
        private const string COMPANY_NAME = "TomCatRunner Studio";
        private const string VERSION_FILE = "Assets/_Project/Resources/VersionInfo.asset";

        /// <summary>
        /// 设置 Android 构建为 APK 格式（供命令行调用）
        /// </summary>
        public static void SetAndroidBuildAPK()
        {
            EditorUserBuildSettings.buildAppBundle = false; // false = APK, true = AAB
            Debug.Log("[CIBuildScript] Android build set to APK format");
        }

        [MenuItem("TomCatRunner/CI/Build Windows Standalone")]
        public static void PerformWindowsBuild()
        {
            ConfigurePlayerSettings();
            BuildWindowsStandalone();
        }

        /// <summary>
        /// CI 入口：Windows x86_64 Standalone 构建（供 game-ci/unity-builder 调用）
        /// </summary>
        public static void BuildWindows64()
        {
            ConfigurePlayerSettings();
            BuildWindowsStandalone();
        }

        [MenuItem("TomCatRunner/CI/Build WebGL")]
        public static void PerformWebGLBuild()
        {
            ConfigurePlayerSettings();
            BuildWebGL();
        }

        /// <summary>
        /// CI 入口：WebGL 构建（供 game-ci/unity-builder 调用）
        /// </summary>
        public static void BuildWebGL()
        {
            ConfigurePlayerSettings();
            BuildWebGLInternal();
        }

        /// <summary>
        /// CI 入口：Android APK 构建（供 game-ci/unity-builder 调用）
        /// </summary>
        public static void BuildAndroid()
        {
            ConfigurePlayerSettings();
            SetAndroidBuildAPK(); // 强制 APK 格式（非 AAB）
            BuildAndroidInternal();
        }

        [MenuItem("TomCatRunner/CI/Build Android APK")]
        public static void PerformAndroidBuild()
        {
            BuildAndroid();
        }

        private static void BuildAndroidInternal()
        {
            string buildPath = "Builds/Android";
            string fullPath = Path.Combine(buildPath, $"{PRODUCT_NAME}.apk");

            Directory.CreateDirectory(buildPath);

            var scenes = GetBuildScenes();
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = fullPath,
                target = BuildTarget.Android,
                options = BuildOptions.StrictMode
            };

            Debug.Log($"[CIBuildScript] Building Android APK to: {fullPath}");

            var report = BuildPipeline.BuildPlayer(options);
            HandleBuildReport(report, "Android");

            // 生成版本信息文件
            GenerateVersionInfo(buildPath, "Android");
        }

        private static void BuildWebGLInternal()
        {
            string buildPath = "Builds/WebGL";

            Directory.CreateDirectory(buildPath);

            var scenes = GetBuildScenes();
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.StrictMode
            };

            Debug.Log($"[CIBuildScript] Building WebGL to: {buildPath}");

            var report = BuildPipeline.BuildPlayer(options);
            HandleBuildReport(report, "WebGL");

            // 生成版本信息文件
            GenerateVersionInfo(buildPath, "WebGL");
        }

        [MenuItem("TomCatRunner/CI/Build Addressables")]
        public static void BuildAddressablesContent()
        {
            BuildAddressables();
        }

        [MenuItem("TomCatRunner/CI/Build All")]
        public static void BuildAll()
        {
            ConfigurePlayerSettings();
            BuildAddressables();
            BuildWindowsStandalone();
            BuildWebGL();
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.productName = PRODUCT_NAME;
            PlayerSettings.companyName = COMPANY_NAME;
            PlayerSettings.applicationIdentifier = "com.tomcatrunner.game";

            // 版本号自动递增（基于 git tag 或构建号）
            var version = GetNextVersion();
            PlayerSettings.bundleVersion = version;
            PlayerSettings.Android.bundleVersionCode = GetVersionCode(version);

            // 图形 API 设置
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });

            // Input 处理：强制旧 Input Manager（UnityEngine.Input API）
            // InputService.cs 全部用 Input.GetKeyDown / GetAxis / GetTouch / GetMouseButton，
            // 但 Unity 2022.3 默认 activeInputHandler=1 (Input System)，
            // 运行时每帧抛 InvalidOperationException: 'you have switched active Input handling
            // to Input System package'，玩家无法操作。这里显式切回 InputManager(0)。
            //
            // 用 reflection 设置，避免 InputHandler 枚举在 Roslyn 兜底编译时不可见
            // （ci/compile.py 拉的 UnityEditor 引用程序集未必暴露 InputHandler 公开类型）。
            try
            {
                var prop = typeof(PlayerSettings).GetProperty("activeInputHandler");
                if (prop != null)
                {
                    prop.SetValue(null, Enum.ToObject(prop.PropertyType, 0)); // 0 = InputManager (Old)
                    Debug.Log($"[CIBuildScript] activeInputHandler set to 0 (InputManager) via reflection, type={prop.PropertyType.Name}");
                }
                else
                {
                    Debug.LogWarning("[CIBuildScript] activeInputHandler property not found on PlayerSettings");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CIBuildScript] Failed to set activeInputHandler via reflection: {e.Message}");
            }

            // 脚本后端
            // 注意：game-ci/unity-builder 默认 docker 镜像不带 IL2CPP 模块
            // StandaloneWindows64 用 Mono 即可（IL2CPP 需要 windows-il2cpp 镜像）
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            // WebGL 必须 IL2CPP，game-ci 对 WebGL 会自动用 webgl-il2cpp 镜像
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.WebGL, ScriptingImplementation.IL2CPP);
            // Android 用 IL2CPP，game-ci 对 Android 会自动用 android 镜像（含 Android SDK/NDK + IL2CPP）
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);

            // Android CPU 架构：ARMv7 + ARM64（Google Play 要求 ARM64）
            PlayerSettings.Android.targetArchitectures = UnityEditor.AndroidArchitecture.ARMv7 | UnityEditor.AndroidArchitecture.ARM64;

            // Android 最低 SDK 版本：API 22（IL2CPP 支持的最低版本，强转避免枚举名差异）
            PlayerSettings.Android.minSdkVersion = (UnityEditor.AndroidSdkVersions)22;

            // 优化设置（Mono 后端不能用 High stripping）
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Standalone, ManagedStrippingLevel.Minimal);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, ManagedStrippingLevel.Low);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Low);

            // WebGL 特定设置
            // 必须禁用压缩：GitHub Pages 不会发送 Content-Encoding: br / gzip 头，
            // 浏览器拿到 .framework.js.br 等文件后无法解码 → 加载失败。
            // 改用 Disabled 后 framework.js / wasm / data 直接以未压缩形态 serve，
            // 文件会变大（约 30~40MB），但能跨平台稳定加载（桌面/手机浏览器均可）。
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.threadsSupport = false; // 需要 SharedArrayBuffer 支持

            // 渲染管线资产（内置管线；若未来引入 URP，在这里加载资产）
            var rpAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>(
                "Assets/_Project/Settings/URP/URPAsset.asset");
            if (rpAsset != null)
            {
                UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset = rpAsset;
                Debug.Log("[CIBuildScript] URP asset loaded");
            }
            else
            {
                Debug.LogWarning("[CIBuildScript] URP asset not found, using built-in render pipeline");
            }

            // Quality 设置
            QualitySettings.SetQualityLevel(2, true); // Medium quality for CI

            Debug.Log($"[CIBuildScript] Player settings configured. Version: {version}");
        }

        private static void BuildWindowsStandalone()
        {
            string buildPath = "Builds/Windows";
            string exeName = $"{PRODUCT_NAME}.exe";
            string fullPath = Path.Combine(buildPath, exeName);

            // 确保输出目录存在
            Directory.CreateDirectory(buildPath);

            var scenes = GetBuildScenes();
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = fullPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.StrictMode
            };

            Debug.Log($"[CIBuildScript] Building Windows Standalone to: {fullPath}");

            var report = BuildPipeline.BuildPlayer(options);
            HandleBuildReport(report, "Windows Standalone");

            // 复制 Addressables 到 StreamingAssets（如果需要本地运行）
            CopyAddressablesToStreamingAssets(buildPath);

            // 生成版本信息文件
            GenerateVersionInfo(buildPath, "Windows");
        }

        private static void BuildAddressables()
        {
            Debug.Log("[CIBuildScript] Building Addressables content...");

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[CIBuildScript] AddressableAssetSettings not found!");
                throw new InvalidOperationException("Addressables not configured");
            }

            // 清理旧构建
            string buildPath = "Builds/Addressables";
            if (Directory.Exists(buildPath))
            {
                Directory.Delete(buildPath, true);
            }
            Directory.CreateDirectory(buildPath);

            // 设置构建路径（通过 Profile 变量）
            settings.RemoteCatalogBuildPath.SetVariableByName(settings, "RemoteBuildPath");
            settings.RemoteCatalogLoadPath.SetVariableByName(settings, "RemoteLoadPath");

            // 执行构建（同步完成，失败时抛异常）
            AddressableAssetSettings.BuildPlayerContent();

            Debug.Log("[CIBuildScript] Addressables build completed successfully!");

            // 验证 catalog 存在
            string catalogPath = Path.Combine(buildPath, "catalog.json");
            if (!File.Exists(catalogPath))
            {
                // 尝试查找带 hash 的 catalog
                var catalogs = Directory.GetFiles(buildPath, "catalog_*.json");
                if (catalogs.Length == 0)
                {
                    throw new FileNotFoundException("Addressables catalog not found after build");
                }
            }

            // 生成版本信息
            GenerateAddressablesVersionInfo(buildPath);
        }

        private static string[] GetBuildScenes()
        {
            // 从 Build Settings 获取场景列表
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                // 默认场景：单一入口场景（全部内容由 GameplayBootstrap 程序化构建）
                scenes = new[]
                {
                    "Assets/_Project/Scenes/Main.unity"
                };
            }

            Debug.Log($"[CIBuildScript] Build scenes: {string.Join(", ", scenes)}");
            return scenes;
        }

        private static void HandleBuildReport(BuildReport report, string targetName)
        {
            var summary = report.summary;

            Debug.Log($"[CIBuildScript] {targetName} Build Result: {summary.result}");
            Debug.Log($"[CIBuildScript] Total Size: {summary.totalSize / (1024 * 1024):F2} MB");
            Debug.Log($"[CIBuildScript] Total Time: {summary.totalTime.TotalSeconds:F1} s");
            Debug.Log($"[CIBuildScript] Total Errors: {summary.totalErrors}");
            Debug.Log($"[CIBuildScript] Total Warnings: {summary.totalWarnings}");

            if (summary.result == BuildResult.Failed)
            {
                var errors = report.steps.SelectMany(s => s.messages)
                    .Where(m => m.type == LogType.Error)
                    .ToArray();

                foreach (var error in errors)
                {
                    Debug.LogError($"[CIBuildScript] Build Error: {error.content}");
                }

                throw new BuildFailedException($"{targetName} build failed with {summary.totalErrors} errors");
            }

            if (summary.totalWarnings > 100)
            {
                Debug.LogWarning($"[CIBuildScript] High warning count: {summary.totalWarnings}");
            }
        }

        private static void CopyAddressablesToStreamingAssets(string buildOutputPath)
        {
            string srcPath = "Assets/AddressablesOutput"; // Addressables 本地构建输出
            string dstPath = Path.Combine(buildOutputPath, $"{PRODUCT_NAME}_Data", "StreamingAssets", "aa");

            if (Directory.Exists(srcPath))
            {
                if (Directory.Exists(dstPath))
                {
                    Directory.Delete(dstPath, true);
                }
                Directory.CreateDirectory(dstPath);

                foreach (var file in Directory.GetFiles(srcPath, "*", SearchOption.AllDirectories))
                {
                    string relativePath = file.Substring(srcPath.Length + 1);
                    string destFile = Path.Combine(dstPath, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destFile));
                    File.Copy(file, destFile, true);
                }

                Debug.Log($"[CIBuildScript] Copied Addressables to StreamingAssets: {dstPath}");
            }
        }

        private static void GenerateVersionInfo(string buildPath, string platform)
        {
            var versionInfo = new VersionInfo
            {
                Version = PlayerSettings.bundleVersion,
                Platform = platform,
                BuildDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                GitCommit = GetGitCommitHash(),
                GitBranch = GetGitBranch(),
                UnityVersion = Application.unityVersion
            };

            string json = JsonUtility.ToJson(versionInfo, true);
            string versionFile = Path.Combine(buildPath, "version.json");
            File.WriteAllText(versionFile, json);

            Debug.Log($"[CIBuildScript] Version info written to: {versionFile}");
        }

        private static void GenerateAddressablesVersionInfo(string buildPath)
        {
            var versionInfo = new
            {
                Version = PlayerSettings.bundleVersion,
                BuildDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                GitCommit = GetGitCommitHash(),
                CatalogVersion = GetAddressablesCatalogVersion(buildPath)
            };

            string json = JsonUtility.ToJson(versionInfo, true);
            string versionFile = Path.Combine(buildPath, "version.json");
            File.WriteAllText(versionFile, json);

            Debug.Log($"[CIBuildScript] Addressables version info written to: {versionFile}");
        }

        private static string GetNextVersion()
        {
            // 优先从 git tag 获取版本
            string gitTag = GetLatestGitTag();
            if (!string.IsNullOrEmpty(gitTag) && gitTag.StartsWith("v"))
            {
                string version = gitTag.Substring(1); // 去掉 'v' 前缀
                // 自动递增 patch 版本
                var parts = version.Split('.');
                if (parts.Length >= 3 && int.TryParse(parts[2], out int patch))
                {
                    parts[2] = (patch + 1).ToString();
                    return string.Join(".", parts);
                }
                return version;
            }

            // 回退到构建号
            int buildNumber = GetBuildNumber();
            return $"1.0.{buildNumber}";
        }

        private static int GetVersionCode(string version)
        {
            var parts = version.Split('.');
            if (parts.Length >= 3 && int.TryParse(parts[2], out int patch))
            {
                return (int.Parse(parts[0]) * 10000) + (int.Parse(parts[1]) * 100) + patch;
            }
            return 10000;
        }

        private static int GetBuildNumber()
        {
            string buildNumberEnv = Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER");
            if (!string.IsNullOrEmpty(buildNumberEnv) && int.TryParse(buildNumberEnv, out int num))
            {
                return num;
            }
            return DateTime.Now.Year * 10000 + DateTime.Now.Month * 100 + DateTime.Now.Day;
        }

        private static string GetLatestGitTag()
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = "describe --tags --abbrev=0",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return output;
            }
            catch
            {
                return null;
            }
        }

        private static string GetGitCommitHash()
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = "rev-parse --short HEAD",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return output;
            }
            catch
            {
                return "unknown";
            }
        }

        private static string GetGitBranch()
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = "rev-parse --abbrev-ref HEAD",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return output;
            }
            catch
            {
                return "unknown";
            }
        }

        private static string GetAddressablesCatalogVersion(string buildPath)
        {
            try
            {
                var catalogs = Directory.GetFiles(buildPath, "catalog_*.json");
                if (catalogs.Length > 0)
                {
                    return Path.GetFileNameWithoutExtension(catalogs[0]).Replace("catalog_", "");
                }
            }
            catch { }
            return "unknown";
        }

        [Serializable]
        private class VersionInfo
        {
            public string Version;
            public string Platform;
            public string BuildDate;
            public string GitCommit;
            public string GitBranch;
            public string UnityVersion;
        }
    }
}