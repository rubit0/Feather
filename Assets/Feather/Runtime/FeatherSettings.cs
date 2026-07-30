using System;
using UnityEngine;

namespace Feather
{
    public enum JavaScriptEditorPreference
    {
        [InspectorName("Auto")]
        [Tooltip("Prefer Cursor, then VS Code (opens project root for jsconfig IntelliSense). Falls back to Unity default.")]
        AutoPreferJsIde = 0,
        Cursor = 1,
        VSCode = 2,
        [InspectorName("Unity default")]
        [Tooltip("Same editor as Edit → Preferences → External Tools (often Rider/C# context — weak JS IntelliSense).")]
        UnityExternalScriptEditor = 3,
        Custom = 4
    }

    /// <summary>
    /// Project-wide Feather settings (Edit → Project Settings → Feather).
    /// </summary>
    public class FeatherSettings : ScriptableObject
    {
        public const string AssetPath = "Assets/Feather/Resources/FeatherSettings.asset";
        public const string ResourcesName = "FeatherSettings";

        private static FeatherSettings _instance;

        public static FeatherSettings Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<FeatherSettings>(ResourcesName);
                return _instance;
            }
        }

        public bool verboseLogging;
        public bool logScriptLoading;
        public bool logComponentAddition;

        [Tooltip("When enabled, JS can use System.Reflection / GetType on CLR objects. Off by default. Does not change which Unity assemblies are exposed.")]
        public bool allowSystemReflection = false;

        [Tooltip("Resources-relative folder used for player script collection.")]
        public string playerScriptsResourcesPath = "Feather/Scripts";

        [Tooltip("UPM package IDs opted into Feather JS AllowClr and type generation.")]
        public string[] enabledApiPackageIds = Array.Empty<string>();

        [Tooltip("Assembly names resolved from enabled API packages (serialized for player builds).")]
        public string[] extraClrAssemblies = Array.Empty<string>();

        [Tooltip("Where double-click / Open sends .js files. JS IDEs open the Unity project root so jsconfig.json IntelliSense works.")]
        public JavaScriptEditorPreference javascriptEditor = JavaScriptEditorPreference.AutoPreferJsIde;

        [Tooltip("Used when JavaScript Editor is Custom — path to editor binary or .app")]
        public string customJavaScriptEditorPath = "";

        /// <summary>Cached fingerprint of Unity/API surface used to skip regenerating JS defs.</summary>
        [HideInInspector]
        public string jsApiStamp = "";

        /// <summary>Cached fingerprint of project Component types used to skip regenerating Project.d.ts.</summary>
        [HideInInspector]
        public string projectDefinitionsStamp = "";

        public static bool VerboseLogging => Instance != null && Instance.verboseLogging;
        public static bool LogScriptLoading => Instance != null && Instance.logScriptLoading;
        public static bool LogComponentAddition => Instance != null && Instance.logComponentAddition;

        public static void Log(string message)
        {
            if (VerboseLogging)
                Debug.Log($"[Feather] {message}");
        }

        public static void LogScriptLoad(string message)
        {
            if (LogScriptLoading)
                Debug.Log($"[Feather] {message}");
        }

        public static void LogComponentAdd(string message)
        {
            if (LogComponentAddition)
                Debug.Log($"[Feather] {message}");
        }

#if UNITY_EDITOR
        public static FeatherSettings GetOrCreateSettings()
        {
            var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<FeatherSettings>(AssetPath);
            if (settings == null)
            {
                settings = CreateInstance<FeatherSettings>();
                var dir = System.IO.Path.GetDirectoryName(AssetPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(dir))
                {
                    EnsureProjectFolder("Assets/Feather");
                    EnsureProjectFolder("Assets/Feather/Resources");
                }
                UnityEditor.AssetDatabase.CreateAsset(settings, AssetPath);
                UnityEditor.AssetDatabase.SaveAssets();
            }
            _instance = settings;
            return settings;
        }

        private static void EnsureProjectFolder(string folder)
        {
            if (UnityEditor.AssetDatabase.IsValidFolder(folder)) return;
            var parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
            var name = System.IO.Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
                UnityEditor.AssetDatabase.CreateFolder(parent, name);
        }
#endif
    }
}
