using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace TomCatRunner.Seasons.Editor
{
    /// <summary>
    /// Addressables 组配置与构建脚本
    /// 划分组：Season_Core / Season_Phase_N / Shared_Base / UI_Screens / Audio_Music
    /// </summary>
    public static class AddressablesSetup
    {
        private const string SETTINGS_PATH = "Assets/AddressableAssetsData/AddressableAssetSettings.asset";

        [MenuItem("TomCatRunner/Addressables/Configure Groups", false, 100)]
        public static void ConfigureAddressableGroups()
        {
            var settings = GetOrCreateSettings();
            if (settings == null) return;

            // 定义组配置
            var groupConfigs = new[]
            {
                new GroupConfig
                {
                    name = "Season_Core",
                    labels = new[] { "season", "core", "runtime" },
                    schema = new ContentUpdateGroupSchema
                    {
                        StaticContent = false,
                        BuildPath = new BuildPath { Path = "Season_Core" },
                        LoadPath = new LoadPath { Path = "Season_Core" }
                    },
                    bundles = new BundleNamingPolicy { BundleNaming = BundleNamingPolicy.BundleNamingMode.ByFolder },
                    includes = new[] { "Assets/_Project/Seasons/Configs", "Assets/_Project/Seasons/Data" }
                },
                new GroupConfig
                {
                    name = "Season_Phase_0",
                    labels = new[] { "season", "phase0", "neon_cyber" },
                    schema = new ContentUpdateGroupSchema
                    {
                        StaticContent = false,
                        BuildPath = new BuildPath { Path = "Season_Phase_0" },
                        LoadPath = new LoadPath { Path = "Season_Phase_0" }
                    },
                    bundles = new BundleNamingPolicy { BundleNaming = BundleNamingPolicy.BundleNamingMode.ByFolder },
                    includes = new[] { "Assets/_Project/Seasons/Art/Phases/Phase0" }
                },
                new GroupConfig
                {
                    name = "Season_Phase_1",
                    labels = new[] { "season", "phase1", "cyberpunk" },
                    schema = new ContentUpdateGroupSchema
                    {
                        StaticContent = false,
                        BuildPath = new BuildPath { Path = "Season_Phase_1" },
                        LoadPath = new LoadPath { Path = "Season_Phase_1" }
                    },
                    bundles = new BundleNamingPolicy { BundleNaming = BundleNamingPolicy.BundleNamingMode.ByFolder },
                    includes = new[] { "Assets/_Project/Seasons/Art/Phases/Phase1" }
                },
                new GroupConfig
                {
                    name = "Season_Phase_2",
                    labels = new[] { "season", "phase2", "void_darkness" },
                    schema = new ContentUpdateGroupSchema
                    {
                        StaticContent = false,
                        BuildPath = new BuildPath { Path = "Season_Phase_2" },
                        LoadPath = new LoadPath { Path = "Season_Phase_2" }
                    },
                    bundles = new BundleNamingPolicy { BundleNaming = BundleNamingPolicy.BundleNamingMode.ByFolder },
                    includes = new[] { "Assets/_Project/Seasons/Art/Phases/Phase2" }
                },
                new GroupConfig
                {
                    name = "Season_Phase_3",
                    labels = new[] { "season", "phase3", "ultimate_rainbow" },
                    schema = new ContentUpdateGroupSchema
                    {
                        StaticContent = false,
                        BuildPath = new BuildPath { Path = "Season_Phase_3" },
                        LoadPath = new LoadPath { Path = "Season_Phase_3" }
                    },
                    bundles = new BundleNamingPolicy { BundleNaming = BundleNamingPolicy.BundleNamingMode.ByFolder },
                    includes = new[] { "Assets/_Project/Seasons/Art/Phases/Phase3" }
                },
                new GroupConfig
                {
                    name = "Shared_Base",
                    labels = new[] { "shared", "base", "common" },
                    schema = new ContentUpdateGroupSchema
                    {
                        StaticContent = true,  // 基础资源不频繁更新
                        BuildPath = new BuildPath { Path = "Shared_Base" },
                        LoadPath = new LoadPath { Path = "Shared_Base" }
                    },
                    bundles = new BundleNamingPolicy { BundleNaming = BundleNamingPolicy.BundleNamingMode.ByFolder },
                    includes = new[] { "Assets/_Project/Common/Art", "Assets/_Project/Core/Art" }
                },
                new GroupConfig
                {
                    name = "UI_Screens",
                    labels = new[] { "ui", "screens", "runtime" },
                    schema = new ContentUpdateGroupSchema
                    {
                        StaticContent = false,
                        BuildPath = new BuildPath { Path = "UI_Screens" },
                        LoadPath = new LoadPath { Path = "UI_Screens" }
                    },
                    bundles = new BundleNamingPolicy { BundleNaming = BundleNamingPolicy.BundleNamingMode.ByFolder },
                    includes = new[] { "Assets/_Project/UI/Prefabs/Screens", "Assets/_Project/UI/Shaders" }
                },
                new GroupConfig
                {
                    name = "Audio_Music",
                    labels = new[] { "audio", "music", "runtime" },
                    schema = new ContentUpdateGroupSchema
                    {
                        StaticContent = true,
                        BuildPath = new BuildPath { Path = "Audio_Music" },
                        LoadPath = new LoadPath { Path = "Audio_Music" }
                    },
                    bundles = new BundleNamingPolicy { BundleNaming = BundleNamingPolicy.BundleNamingMode.ByFolder },
                    includes = new[] { "Assets/_Project/Audio/Music", "Assets/_Project/Audio/SFX" }
                },
                new GroupConfig
                {
                    name = "Chunks_Whitebox",
                    labels = new[] { "chunks", "whitebox", "testing" },
                    schema = new ContentUpdateGroupSchema
                    {
                        StaticContent = true,
                        BuildPath = new BuildPath { Path = "Chunks_Whitebox" },
                        LoadPath = new LoadPath { Path = "Chunks_Whitebox" }
                    },
                    bundles = new BundleNamingPolicy { BundleNaming = BundleNamingPolicy.BundleNamingMode.ByFolder },
                    includes = new[] { "Assets/_Project/Seasons/Prefabs/Chunks/Whitebox" }
                }
            };

            foreach (var config in groupConfigs)
            {
                CreateOrUpdateGroup(settings, config);
            }

            // 设置默认组
            var defaultGroup = settings.DefaultGroup;
            if (defaultGroup != null && defaultGroup.Name == "Default Local Group")
            {
                settings.RemoveGroup(defaultGroup);
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("[AddressablesSetup] Groups configured successfully");
        }

        private static AddressableAssetSettings GetOrCreateSettings()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                var dir = Path.GetDirectoryName(SETTINGS_PATH);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                settings = AddressableAssetSettings.Create(SETTINGS_PATH, "AddressableAssetSettings", true, true);
                Debug.Log($"[AddressablesSetup] Created new settings at {SETTINGS_PATH}");
            }
            return settings;
        }

        private static void CreateOrUpdateGroup(AddressableAssetSettings settings, GroupConfig config)
        {
            var group = settings.FindGroup(config.name);
            if (group == null)
            {
                group = settings.CreateGroup(config.name, false, false, false, null, typeof(ContentUpdateGroupSchema), typeof(BundledAssetGroupSchema));
                Debug.Log($"[AddressablesSetup] Created group: {config.name}");
            }

            // 配置 Schema
            var contentSchema = group.GetSchema<ContentUpdateGroupSchema>();
            if (contentSchema != null && config.schema != null)
            {
                contentSchema.StaticContent = config.schema.StaticContent;
            }

            var bundleSchema = group.GetSchema<BundledAssetGroupSchema>();
            if (bundleSchema != null && config.bundles != null)
            {
                bundleSchema.BundleNaming = config.bundles.BundleNaming;
            }

            // 添加标签
            foreach (var label in config.labels)
            {
                if (!settings.GetLabels().Contains(label))
                {
                    settings.AddLabel(label);
                }
                group.SetLabel(label, true, true);
            }

            // 添加资源路径
            if (config.includes != null)
            {
                foreach (var includePath in config.includes)
                {
                    AddDirectoryToGroup(settings, group, includePath, config.labels);
                }
            }

            EditorUtility.SetDirty(group);
        }

        private static void AddDirectoryToGroup(AddressableAssetSettings settings, AddressableAssetGroup group, string directoryPath, string[] labels)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
                AssetDatabase.Refresh();
            }

            var guids = AssetDatabase.FindAssets("", new[] { directoryPath });
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var entry = settings.CreateOrMoveEntry(guid, group);
                entry.address = assetPath.Replace("Assets/", "");
                entry.SetLabels(labels, true, true);
            }
        }

        [MenuItem("TomCatRunner/Addressables/Build Player Content", false, 200)]
        public static void BuildPlayerContent()
        {
            var settings = GetOrCreateSettings();
            if (settings == null) return;

            AddressableAssetSettings.BuildPlayerContent();
            Debug.Log("[AddressablesSetup] Build completed");
        }

        [MenuItem("TomCatRunner/Addressables/Clean Build Cache", false, 201)]
        public static void CleanBuildCache()
        {
            var buildPath = Path.Combine(Application.dataPath, "../Library/com.unity.addressables/aa/BuildCache");
            if (Directory.Exists(buildPath))
            {
                Directory.Delete(buildPath, true);
                Debug.Log("[AddressablesSetup] Build cache cleaned");
            }
        }

        [MenuItem("TomCatRunner/Addressables/Generate Catalog Version", false, 202)]
        public static void GenerateCatalogVersion()
        {
            var settings = GetOrCreateSettings();
            if (settings == null) return;

            var version = PlayerSettings.applicationVersion + "." + PlayerSettings.Android.bundleVersionCode;
            var catalogPath = Path.Combine("Assets/StreamingAssets/aa", settings.ProfileSettings.GetValueByName(settings.activeProfileId, "Remote.BuildPath"));

            // 生成版本文件
            var versionData = new CatalogVersionData
            {
                version = version,
                timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                catalogUrl = settings.ProfileSettings.GetValueByName(settings.activeProfileId, "Remote.LoadPath") + "/catalog.json"
            };

            var json = JsonUtility.ToJson(versionData, true);
            var versionFilePath = Path.Combine(Application.streamingAssetsPath, "aa/catalog_version.json");
            Directory.CreateDirectory(Path.GetDirectoryName(versionFilePath));
            File.WriteAllText(versionFilePath, json);

            AssetDatabase.Refresh();
            Debug.Log($"[AddressablesSetup] Catalog version generated: {version}");
        }

        private class GroupConfig
        {
            public string name;
            public string[] labels;
            public ContentUpdateGroupSchema schema;
            public BundledAssetGroupSchema bundles;
            public string[] includes;
        }

        [System.Serializable]
        private class CatalogVersionData
        {
            public string version;
            public long timestamp;
            public string catalogUrl;
        }
    }

    /// <summary>
    /// Addressables 构建后处理 - 生成版本清单、压缩、上传准备
    /// </summary>
    public class AddressablesBuildProcessor : UnityEditor.AddressableAssets.Build.IBuildProcessor
    {
        public int Order => 0;

        public void OnPreBuild(UnityEditor.AddressableAssets.Build.AddressablesBuildContext context)
        {
            Debug.Log("[AddressablesBuildProcessor] Pre-build: Generating version info");
        }

        public void OnPostBuild(UnityEditor.AddressableAssets.Build.AddressablesBuildContext context)
        {
            if (context.Result.Error != null)
            {
                Debug.LogError($"[AddressablesBuildProcessor] Build failed: {context.Result.Error}");
                return;
            }

            GenerateVersionManifest(context);
            CompressBundles(context);
            Debug.Log("[AddressablesBuildProcessor] Post-build processing completed");
        }

        private void GenerateVersionManifest(UnityEditor.AddressableAssets.Build.AddressablesBuildContext context)
        {
            var manifest = new BundleManifest
            {
                version = PlayerSettings.applicationVersion,
                buildNumber = PlayerSettings.Android.bundleVersionCode,
                timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                bundles = new List<BundleInfo>()
            };

            foreach (var bundle in context.Result.BundleInfos)
            {
                manifest.bundles.Add(new BundleInfo
                {
                    name = bundle.Key,
                    hash = bundle.Value.Hash,
                    size = bundle.Value.FileSize,
                    crc = bundle.Value.Crc,
                    labels = bundle.Value.Labels.ToList()
                });
            }

            var outputPath = Path.Combine(context.Settings.RemoteCatalogBuildPath, "bundle_manifest.json");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath, JsonUtility.ToJson(manifest, true));

            // 复制到 StreamingAssets 用于首包
            var streamingPath = Path.Combine(Application.streamingAssetsPath, "aa/bundle_manifest.json");
            Directory.CreateDirectory(Path.GetDirectoryName(streamingPath));
            File.Copy(outputPath, streamingPath, true);

            AssetDatabase.Refresh();
        }

        private void CompressBundles(UnityEditor.AddressableAssets.Build.AddressablesBuildContext context)
        {
            // 可选：启用 Brotli/Gzip 压缩
            var buildPath = context.Settings.RemoteCatalogBuildPath;
            var files = Directory.GetFiles(buildPath, "*.bundle", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                // 这里可以添加压缩逻辑
                // 例如使用 BrotliSharpLib 或 System.IO.Compression
            }
        }

        [System.Serializable]
        private class BundleManifest
        {
            public string version;
            public int buildNumber;
            public long timestamp;
            public List<BundleInfo> bundles;
        }

        [System.Serializable]
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