using UnityEngine;

namespace TomCatRunner.Common
{
    /// <summary>
    /// 版本信息 ScriptableObject - 运行时可访问构建版本信息
    /// </summary>
    [CreateAssetMenu(fileName = "VersionInfo", menuName = "TomCatRunner/Version Info")]
    public class VersionInfo : ScriptableObject
    {
        [Header("Build Information")]
        [SerializeField] private string _version = "1.0.0";
        [SerializeField] private string _buildDate = "";
        [SerializeField] private string _gitCommit = "";
        [SerializeField] private string _gitBranch = "";
        [SerializeField] private string _unityVersion = "";

        public string Version => _version;
        public string BuildDate => _buildDate;
        public string GitCommit => _gitCommit;
        public string GitBranch => _gitBranch;
        public string UnityVersion => _unityVersion;

        public string FullVersionString => $"{_version} ({_gitCommit}) - {_buildDate}";
    }
}