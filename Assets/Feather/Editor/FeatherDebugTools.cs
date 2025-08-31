using UnityEditor;
using UnityEngine;

namespace Feather.Editor
{
    public static class FeatherDebugTools
    {
        [MenuItem("Feather/Debug/List All JavaScriptBehaviours")]
        public static void ListAllJavaScriptBehaviours()
        {
            var scriptBehaviours = Object.FindObjectsByType<JavaScriptBehaviour>(FindObjectsSortMode.None);
            
            Debug.Log("=== Active JavaScriptBehaviours ===");
            
            if (scriptBehaviours.Length == 0)
            {
                Debug.Log("No JavaScriptBehaviour components found in the scene.");
            }
            else
            {
                foreach (var sb in scriptBehaviours)
                {
                    var scriptName = sb.script != null ? sb.script.name : "None";
                    var propCount = sb.properties != null ? sb.properties.Length : 0;
                    Debug.Log($"GameObject: {sb.gameObject.name}, Script: {scriptName}, Properties: {propCount}");
                }
            }
        }
        
        [MenuItem("Feather/Debug/Check Runtime")]
        public static void CheckRuntime()
        {
            var runtime = Object.FindFirstObjectByType<Runtime>();
            
            if (runtime == null)
            {
                Debug.LogWarning("No Feather Runtime found in scene. JavaScript components will not work.");
            }
            else
            {
                Debug.Log($"Feather Runtime found on GameObject: {runtime.gameObject.name}");
                Debug.Log($"Loaded Scripts: {runtime.LoadedScripts.Count}");
                
                foreach (var script in runtime.LoadedScripts)
                {
                    Debug.Log($"  - {script.Key}: {script.Value.Class.Name}");
                }
            }
        }
        
        [MenuItem("Feather/Debug/Toggle Verbose Logging")]
        public static void ToggleVerboseLogging()
        {
            FeatherSettings.VerboseLogging = !FeatherSettings.VerboseLogging;
            Debug.Log($"Feather verbose logging: {(FeatherSettings.VerboseLogging ? "ENABLED" : "DISABLED")}");
        }
        
        [MenuItem("Feather/Debug/Toggle Verbose Logging", true)]
        public static bool ToggleVerboseLoggingValidate()
        {
            Menu.SetChecked("Feather/Debug/Toggle Verbose Logging", FeatherSettings.VerboseLogging);
            return true;
        }
        
        [MenuItem("Feather/Reload All Scripts")]
        public static void ReloadAllScripts()
        {
            if (Runtime.Instance != null)
            {
                Runtime.Instance.ReloadAllScripts();
            }
            else
            {
                Debug.LogWarning("No Feather Runtime found in scene. Scripts cannot be reloaded.");
            }
        }
        
        [MenuItem("Feather/Reload All Scripts", true)]
        public static bool ReloadAllScriptsValidate()
        {
            return Application.isPlaying && Runtime.Instance != null;
        }
        
        [MenuItem("Feather/Debug/Test Hot Reload")]
        public static void TestHotReload()
        {
            if (Runtime.Instance != null)
            {
                // Find scriptTest.js specifically and reload it
                var scriptTestPath = "Demo/scriptTest.js";
                var scriptAsset = AssetDatabase.LoadAssetAtPath<TextAsset>($"Assets/{scriptTestPath}");
                if (scriptAsset != null)
                {
                    Debug.Log($"🧪 Testing hot reload for scriptTest.js");
                    Runtime.Instance.ReloadScript(scriptAsset);
                }
                else
                {
                    Debug.LogError($"❌ Could not find scriptTest.js at Assets/{scriptTestPath}");
                }
            }
            else
            {
                Debug.LogWarning("No Feather Runtime found in scene.");
            }
        }
        
        [MenuItem("Feather/Debug/Test Hot Reload", true)]
        public static bool TestHotReloadValidate()
        {
            return Application.isPlaying && Runtime.Instance != null;
        }
    }
}
