using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace TomCatRunner.Seasons.Editor
{
    /// <summary>
    /// Addressables 分组配置工具 - 建立赛季/共享/UI/音频/白模资源分组
    /// 菜单: TomCatRunner > Addressables
    /// </summary>
    public static class AddressablesSetup
    {
        private const string SETTINGS_PATH = "Assets/AddressableAssetsData/AddressableAssetSettings.asset";

        [MenuItem("TomCatRunner/Addressables/Configure Groups", false, 100)]
        public static void ConfigureAddressableGroups()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[AddressablesSetup] AddressableAssetSettings not found. " +
                               "Open Window > Asset Management > Addressables > Groups and create settings first.");
                return;
            }

            // 组配置：名称 / 标签 / 是否静态内容 / 收录目录
            var groupConfigs = new[]
            {
                new GroupConfig
                {
                    name = "Season_Core",
                    labels = new[] { "season", "core", "runtime" },
                    staticContent = false,
                    includes = new[] { "Assets/_Project/Seasons/Configs", "Assets/_Project/Seasons/Data" }
                },
                new GroupConfig
                {
                    name = "Season_Phase_0",
                    labels = new[] { "season", "phase0", "neon_cyber" },
                    staticContent = false,
                    includes = new[] { "Assets/_Project/Seasons/Art/Phases/Phase0" }
                },
                new GroupConfig
                {
                    name = "Season_Phase_1",
                    labels = new[] { "season", "phase1", "cyberpunk" },
                    staticContent = false,
                    includes = new[] { "Assets/_Project/Seasons/Art/Phases/Phase1" }
                },
                new GroupConfig
                {
                    name = "Season_Phase_2",
                    labels = new[] { "season", "phase2", "void_darkness" },
                    staticContent = false,
                    includes = new[] { "Assets/_Project/Seasons/Art/Phases/Phase2" }
                },
                new GroupConfig
                {
                    name = "Season_Phase_3",
                    labels = new[] { "season", "phase3", "ultimate_rainbow" },
                    staticContent = false,
                    includes = new[] { "Assets/_Project/Seasons/Art/Phases/Phase3" }
                },
                new GroupConfig
                {
                    name = "Shared_Base",
                    labels = new[] { "shared", "base", "common" },
                    staticContent = true,   // 基础资源不频繁更新
                    includes = new[] { "Assets/_Project/Common/Art", "Assets/_Project/Core/Art" }
                },
                new GroupConfig
                {
                    name = "UI_Screens",
                    labels = new[] { "ui", "screens", "runtime" },
                    staticContent = false,
                    includes = new[] { "Assets/_Project/UI/Prefabs/Screens", "Assets/_Project/UI/Shaders" }
                },
                new GroupConfig
                {
                    name = "Audio_Music",
                    labels = new[] { "audio", "music", "runtime" },
                    staticContent = true,
                    includes = new[] { "Assets/_Project/Audio/Music", "Assets/_Project/Audio/SFX" }
                },
                new GroupConfig
                {
                    name = "Chunks_Whitebox",
                    labels = new[] { "chunks", "whitebox", "testing" },
                    staticContent = true,
                    includes = new[] { "Assets/_Project/Seasons/Prefabs/Chunks/Whitebox" }
                }
            };

            foreach (var config in groupConfigs)
            {
                CreateOrUpdateGroup(settings, config);
            }

            // 移除未使用的默认空组
            var defaultGroup = settings.DefaultGroup;
            if (defaultGroup != null && defaultGroup.Name == "Default Local Group"
                && defaultGroup.entries.Count == 0)
            {
                settings.RemoveGroup(defaultGroup);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[AddressablesSetup] Configured {groupConfigs.Length} addressable groups.");
        }

        private static void CreateOrUpdateGroup(AddressableAssetSettings settings, GroupConfig config)
        {
            var group = settings.groups.FirstOrDefault(g => g != null && g.Name == config.name);
            if (group == null)
            {
                group = settings.CreateGroup(config.name, false, false, false, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
                Debug.Log($"[AddressablesSetup] Created group: {config.name}");
            }

            // Bundled 路径策略：静态内容走远端可更新路径，动态内容走本地路径
            var bundled = group.GetSchema<BundledAssetGroupSchema>();
            if (bundled != null)
            {
                bundled.BuildPath.SetVariableByName(settings,
                    config.staticContent ? "RemoteBuildPath" : "LocalBuildPath");
                bundled.LoadPath.SetVariableByName(settings,
                    config.staticContent ? "RemoteLoadPath" : "LocalLoadPath");
                bundled.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.NoHash;
                bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            }

            var contentUpdate = group.GetSchema<ContentUpdateGroupSchema>();
            if (contentUpdate != null)
            {
                contentUpdate.StaticContent = config.staticContent;
            }

            foreach (var label in config.labels)
            {
                if (!settings.GetLabels().Contains(label))
                {
                    settings.AddLabel(label);
                }
            }

            foreach (var directory in config.includes)
            {
                AddDirectoryToGroup(settings, group, directory, config.labels);
            }
        }

        private static void AddDirectoryToGroup(AddressableAssetSettings settings,
            AddressableAssetGroup group, string directoryPath, string[] labels)
        {
            if (!Directory.Exists(directoryPath))
            {
                return; // 目录尚不存在时静默跳过
            }

            var guids = AssetDatabase.FindAssets(string.Empty, new[] { directoryPath });
            int added = 0;

            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!File.Exists(assetPath))
                {
                    continue; // 跳过文件夹
                }

                var entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
                foreach (var label in labels)
                {
                    entry.SetLabel(label, true);
                }
                added++;
            }

            if (added > 0)
            {
                Debug.Log($"[AddressablesSetup] {group.Name}: +{added} assets from {directoryPath}");
            }
        }

        [MenuItem("TomCatRunner/Addressables/Build Player Content", false, 200)]
        public static void BuildPlayerContent()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[AddressablesSetup] AddressableAssetSettings not found.");
                return;
            }

            var processor = new AddressablesBuildProcessor();
            processor.OnPreBuild();

            AddressableAssetSettings.BuildPlayerContent();
            Debug.Log("[AddressablesSetup] Player content built.");

            processor.OnPostBuild(settings);
        }

        [MenuItem("TomCatRunner/Addressables/Clean Build Cache", false, 201)]
        public static void CleanBuildCache()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[AddressablesSetup] AddressableAssetSettings not found.");
                return;
            }

            // 1.22.x 无内置 CleanBuildCache API，直接删除构建缓存目录
            string cacheDir = Path.Combine("Library", "com.unity.addressables");
            if (Directory.Exists(cacheDir))
            {
                Directory.Delete(cacheDir, true);
                File.Delete(cacheDir + ".meta");
            }
            Debug.Log("[AddressablesSetup] Addressables build cache cleaned.");
        }

        [MenuItem("TomCatRunner/Addressables/Generate Catalog Version", false, 202)]
        public static void GenerateCatalogVersion()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[AddressablesSetup] AddressableAssetSettings not found.");
                return;
            }

            var processor = new AddressablesBuildProcessor();
            processor.OnPostBuild(settings);
            Debug.Log("[AddressablesSetup] Catalog version regenerated.");
        }

        /// <summary>
        /// 分组描述
        /// </summary>
        [Serializable]
        private class GroupConfig
        {
            public string name;
            public string[] labels;
            public bool staticContent;
            public string[] includes;
        }
    }

    /// <summary>
    /// Addressables 构建后处理工具 - 生成版本清单、压缩、上传准备
    /// （由 CIBuildScript / 菜单在 Addressables 构建完成后手动调用）
    /// </summary>
    public class AddressablesBuildProcessor
    {
        public void OnPreBuild()
        {
            Debug.Log("[AddressablesBuildProcessor] Pre-build: Generating version info");
        }

        public void OnPostBuild(AddressableAssetSettings settings)
        {
            GenerateVersionManifest(settings);
            CompressBundles(settings);
            Debug.Log("[AddressablesBuildProcessor] Post-build processing completed");
        }

        private void GenerateVersionManifest(AddressableAssetSettings settings)
        {
            var manifest = new BundleManifest
            {
                version = PlayerSettings.bundleVersion,
                buildNumber = PlayerSettings.Android.bundleVersionCode,
                timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                bundles = new List<BundleInfo>()
            };

            // 汇总各分组的 bundle 命名信息（哈希在 bundle 构建产物中，由远端 catalog.json 提供）
            foreach (var group in settings.groups)
            {
                if (group == null) continue;
                var schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema == null) continue;

                manifest.bundles.Add(new BundleInfo
                {
                    name = $"{group.name}.bundle",
                    hash = "",
                    size = 0,
                    crc = 0,
                    labels = group.entries.SelectMany(e => e.labels).Distinct().ToList()
                });
            }

            string buildPath = ResolveRemoteBuildPath(settings);
            if (string.IsNullOrEmpty(buildPath)) buildPath = "Library/com.unity.addressables";

            var outputPath = Path.Combine(buildPath, "bundle_manifest.json");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath, JsonUtility.ToJson(manifest, true));

            // 复制到 StreamingAssets 用于首包
            var streamingPath = Path.Combine(Application.streamingAssetsPath, "aa/bundle_manifest.json");
            Directory.CreateDirectory(Path.GetDirectoryName(streamingPath));
            File.Copy(outputPath, streamingPath, true);

            AssetDatabase.Refresh();
        }

        private void CompressBundles(AddressableAssetSettings settings)
        {
            // 可选：启用 Brotli/Gzip 压缩
            string buildPath = ResolveRemoteBuildPath(settings);
            if (string.IsNullOrEmpty(buildPath) || !Directory.Exists(buildPath)) return;

            var files = Directory.GetFiles(buildPath, "*.bundle", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                // 这里可以添加压缩逻辑
                // 例如使用 BrotliSharpLib 或 System.IO.Compression
            }
        }

        private static string ResolveRemoteBuildPath(AddressableAssetSettings settings)
        {
            try
            {
                return settings.RemoteCatalogBuildPath.GetValue(settings);
            }
            catch
            {
                return null;
            }
        }

        [Serializable]
        private class BundleManifest
        {
            public string version;
            public int buildNumber;
            public long timestamp;
            public List<BundleInfo> bundles;
        }

        [Serializable]
        private class BundleInfo
        {
            public string name;
            public string hash;
            public long size;
            public uint crc;
            public List<string> labels;
        }
    }
}
