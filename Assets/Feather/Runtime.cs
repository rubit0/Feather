using System.Collections.Generic;
using Feather.Analysis;
using Jint;
using Jint.Native.Object;
using UnityEngine;

namespace Feather
{
    public class Runtime : MonoBehaviour
    {
        public static Runtime Instance  { get; private set; }
        public Engine Engine { get; private set; }
        public Dictionary<string, ScriptMeta> LoadedScripts { get; set; } 
            = new Dictionary<string, ScriptMeta>();
        private Dictionary<string, string> _scriptContents = new Dictionary<string, string>();
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            Engine = new Engine(cfg =>
            {
                cfg.Debugger.Enabled = true;
                cfg.Interop.AllowGetType = true;
                cfg.Interop.AllowSystemReflection = true;
                cfg.AllowClr(
                    typeof(GameObject).Assembly, 
                    typeof(Rigidbody).Assembly, 
                    typeof(AudioListener).Assembly,
                    typeof(Input).Assembly, 
                    typeof(Canvas).Assembly
                    );
            });
            Engine.Execute("var Unity = importNamespace('UnityEngine');");
            // Define base interface class
            Engine.Execute(@"class jsBehaviour {
                                @GameObject
                                gameObject;
                                @Transform
                                transform
                            }");
            // Load scripts from Resources/Scripts directory
            var allScripts = Resources.LoadAll<TextAsset>("Scripts");
            LoadScriptsFromArray(allScripts);
            
            // Also load any .js files that might be in other locations
            LoadScriptsFromProject();
        }

        public ObjectInstance InstantiateClass(ClassMeta classMeta)
        {
            return Engine.Evaluate($"return new {classMeta.Name}();").AsObject();
        }
        
        private void LoadScriptsFromArray(TextAsset[] scripts)
        {
            foreach (var scriptText in scripts)
            {
                LoadScript(scriptText);
            }
        }
        
        private void LoadScriptsFromProject()
        {
#if UNITY_EDITOR
            // In editor, also scan for .js files throughout the project
            var guids = UnityEditor.AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets" });
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".js") || path.EndsWith(".jsu") || path.EndsWith(".jsfeather"))
                {
                    var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                    if (asset != null && !LoadedScripts.ContainsKey(GetScriptClassName(asset)))
                    {
                        LoadScript(asset);
                    }
                }
            }
#endif
        }
        
        private void LoadScript(TextAsset scriptAsset)
        {
            try
            {
                var body = scriptAsset.text;
                var script = Analyzer.ParseScript(body);
                if (Analyzer.IsScriptValid(script))
                {
                    Engine.Execute(body);
                    var scriptMeta = Analyzer.AnalyzeScript(script);
                    if (Analyzer.HasJSBehaviour(scriptMeta))
                    {
                        if (!LoadedScripts.ContainsKey(scriptMeta.Class.Name))
                        {
                            LoadedScripts.Add(scriptMeta.Class.Name, scriptMeta);
                            _scriptContents[scriptMeta.Class.Name] = body; // Cache the content
                            // Debug.Log($"✅ Loaded JavaScript: {scriptMeta.Class.Name} from {scriptAsset.name}");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load JavaScript {scriptAsset.name}: {ex.Message}");
            }
        }
        
        private string GetScriptClassName(TextAsset scriptAsset)
        {
            try
            {
                var script = Analyzer.ParseScript(scriptAsset.text);
                if (Analyzer.IsScriptValid(script))
                {
                    var scriptMeta = Analyzer.AnalyzeScript(script);
                    return scriptMeta.Class.Name;
                }
            }
            catch
            {
                // Ignore errors during class name extraction
            }
            return scriptAsset.name;
        }
        
        public void ReloadScript(TextAsset scriptAsset)
        {
            var className = GetScriptClassName(scriptAsset);
            var currentContent = scriptAsset.text;
            
            // Check if content actually changed
            if (_scriptContents.ContainsKey(className) && _scriptContents[className] == currentContent)
            {
                return; // No change, no need to reload
            }
            
            if (FeatherSettings.VerboseLogging)
            {
                Debug.Log($"🔄 Reloading JavaScript: {className}");
            }
            
            // Remove old script from cache
            if (LoadedScripts.ContainsKey(className))
            {
                LoadedScripts.Remove(className);
            }
            
            // Reload the script
            LoadScript(scriptAsset);
            
            // Update content cache
            _scriptContents[className] = currentContent;
            
            // Notify all ScriptBehaviours using this script to reinitialize
            NotifyScriptReloaded(className);
        }
        
        private void NotifyScriptReloaded(string className)
        {
            var allScriptBehaviours = FindObjectsByType<ScriptBehaviour>(FindObjectsSortMode.None);
            foreach (var sb in allScriptBehaviours)
            {
                if (sb.script != null && GetScriptClassName(sb.script) == className)
                {
                    sb.ReloadScript();
                }
            }
        }
        
        public void ReloadAllScripts()
        {
            Debug.Log("🔄 Reloading all JavaScript files...");
            
            // Clear all caches
            LoadedScripts.Clear();
            _scriptContents.Clear();
            
            // Recreate the engine to ensure clean state
            Engine = new Engine(cfg =>
            {
                cfg.Debugger.Enabled = true;
                cfg.Interop.AllowGetType = true;
                cfg.Interop.AllowSystemReflection = true;
                cfg.AllowClr(
                    typeof(GameObject).Assembly, 
                    typeof(Rigidbody).Assembly, 
                    typeof(AudioListener).Assembly,
                    typeof(Input).Assembly, 
                    typeof(Canvas).Assembly
                    );
            });
            Engine.Execute("var Unity = importNamespace('UnityEngine');");
            Engine.Execute(@"class jsBehaviour {
                                @GameObject
                                gameObject;
                                @Transform
                                transform
                            }");
            
            // Reload all scripts
            var allScripts = Resources.LoadAll<TextAsset>("Scripts");
            LoadScriptsFromArray(allScripts);
            LoadScriptsFromProject();
            
            // Notify all ScriptBehaviours to reinitialize
            var allScriptBehaviours = FindObjectsByType<ScriptBehaviour>(FindObjectsSortMode.None);
            foreach (var sb in allScriptBehaviours)
            {
                if (sb.script != null)
                {
                    sb.ReloadScript();
                }
            }
            
            Debug.Log($"✅ Reloaded {LoadedScripts.Count} JavaScript files");
        }
    }
}
