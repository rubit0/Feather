#if UNITY_EDITOR
using UnityEditor.PackageManager;

namespace Feather.Editor
{
    /// <summary>
    /// Resolves the Feather root whether developed under <c>Assets/Feather</c>
    /// or installed via UPM (PackageCache).
    /// </summary>
    internal static class FeatherPackageUtil
    {
        public const string PackageName = "com.rubit0.feather";
        public const string DevAssetsRoot = "Assets/Feather";

        public static string PackageRoot
        {
            get
            {
                var info = PackageInfo.FindForAssembly(typeof(Runtime).Assembly);
                if (info != null && !string.IsNullOrEmpty(info.assetPath))
                    return info.assetPath.Replace('\\', '/').TrimEnd('/');
                return DevAssetsRoot;
            }
        }

        public static string EditorFolder => PackageRoot + "/Editor";
    }
}
#endif
