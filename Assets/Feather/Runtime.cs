using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Feather.Analysis;
using Jint;
using Jint.Native;
using Jint.Native.Object;
using UnityEngine;

namespace Feather
{
    public class Runtime : MonoBehaviour
    {
        public static Runtime Instance { get; private set; }
        public Engine Engine { get; private set; }
        public Dictionary<string, ScriptMeta> LoadedScripts { get; set; } = new Dictionary<string, ScriptMeta>();

        private readonly Dictionary<string, string> _scriptContents = new Dictionary<string, string>();
        private readonly Dictionary<string, JavaScript> _scriptAssets = new Dictionary<string, JavaScript>();
        private readonly Dictionary<string, object> _requireCache = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Scripts registered at runtime (AssetBundle, backend) — re-applied on engine rebuild.</summary>
        private readonly Dictionary<string, string> _runtimeScripts = new Dictionary<string, string>(StringComparer.Ordinal);

        private static readonly Regex JsStackFrameRegex = new Regex(
            @"[:(](?<line>\d+):(?<col>\d+)\)?\s*$",
            RegexOptions.Compiled | RegexOptions.Multiline);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            RebuildEngineAndLoadAll();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public ObjectInstance InstantiateClass(ClassMeta classMeta)
        {
            return Engine.Evaluate($"new {classMeta.Name}()").AsObject();
        }

        public void RebuildEngineAndLoadAll()
        {
            LoadedScripts.Clear();
            _scriptContents.Clear();
            _scriptAssets.Clear();
            _requireCache.Clear();

            Engine = UnityApiSurface.CreateEngine();
            RegisterRequire();

#if UNITY_EDITOR
            // Project Assets are source of truth in Editor. Loading Resources first
            // would pin stale build copies (e.g. legacy .js.txt) and skip the real scripts.
            LoadScriptsFromProject();
#else
            LoadScriptsFromPlayerManifest();
            LoadScriptsFromResources();
#endif

            FeatherSettings.Log($"Loaded {LoadedScripts.Count} JavaScript classes");
            LoadRuntimeRegisteredScripts();
        }

        /// <summary>
        /// Register a jsBehaviour class from source at runtime (AssetBundle, downloaded text, etc.).
        /// Returns the class name, or null if analysis/execution failed or the class already exists (use replace).
        /// </summary>
        public string RegisterScript(string source, string assetName = null, bool replace = false)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                Debug.LogError("[Feather] RegisterScript: source is empty.");
                return null;
            }

            if (Engine == null)
            {
                Debug.LogError("[Feather] RegisterScript: Runtime is not ready.");
                return null;
            }

            if (!Analyzer.TryAnalyze(source, out var meta, out var error))
            {
                Debug.LogError($"[Feather] RegisterScript: {error}");
                return null;
            }

            if (!Analyzer.HasJSBehaviour(meta))
            {
                Debug.LogWarning("[Feather] RegisterScript: class must extend jsBehaviour.");
                return null;
            }

            var className = meta.Class.Name;
            if (LoadedScripts.ContainsKey(className) && !replace)
                return null;

            if (LoadedScripts.ContainsKey(className) && replace)
            {
                _runtimeScripts[className] = source;
                RebuildEngineAndLoadAll();
                if (Application.isPlaying)
                    ReloadAllHosts();
                return className;
            }

            var displayName = string.IsNullOrEmpty(assetName) ? className : assetName;
            var registered = LoadScriptFromText(displayName, source, displayName, null);
            if (registered != null)
                _runtimeScripts[className] = source;
            return registered;
        }

        /// <summary>Register a <see cref="JavaScript"/> asset (e.g. from an AssetBundle).</summary>
        public string RegisterScript(JavaScript script, bool replace = false)
        {
            if (script == null) return null;
            return RegisterScript(script.text, script.name, replace);
        }

        /// <summary>Register every <c>.js</c> asset in a loaded bundle. Returns how many classes were registered.</summary>
        public int RegisterScriptsFromBundle(AssetBundle bundle, bool replace = false)
        {
            if (bundle == null) return 0;

            var count = 0;
            foreach (var assetName in bundle.GetAllAssetNames())
            {
                if (!assetName.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                    continue;

                var js = bundle.LoadAsset<JavaScript>(assetName);
                if (js != null)
                {
                    if (RegisterScript(js, replace) != null)
                        count++;
                    continue;
                }

                var text = bundle.LoadAsset<TextAsset>(assetName);
                if (text == null) continue;

                var name = System.IO.Path.GetFileNameWithoutExtension(assetName);
                if (RegisterScript(text.text, name, replace) != null)
                    count++;
            }

            return count;
        }

        private void LoadRuntimeRegisteredScripts()
        {
            if (_runtimeScripts.Count == 0) return;
            foreach (var kv in _runtimeScripts)
            {
                if (LoadedScripts.ContainsKey(kv.Key)) continue;
                LoadScriptFromText(kv.Key, kv.Value, "runtime:" + kv.Key, null);
            }
        }

        public void ReloadScript(JavaScript scriptAsset)
        {
            if (scriptAsset == null) return;

            FeatherSettings.Log($"Hot-reload triggered by {scriptAsset.name} — recreating JS engine");
            RebuildEngineAndLoadAll();
            ReloadAllHosts();
        }

        public void ReloadAllScripts()
        {
            Debug.Log("[Feather] Reloading all JavaScript files (engine recreate)...");
            RebuildEngineAndLoadAll();
            ReloadAllHosts();
            Debug.Log($"[Feather] Reloaded {LoadedScripts.Count} JavaScript classes");
        }

        private static void ReloadAllHosts()
        {
            var all = FindObjectsByType<JavaScriptBehaviour>(FindObjectsInactive.Include);
            // Drop every instance first so peer EnsureJsInstance cannot resurrect a stale engine object
            foreach (var sb in all)
            {
                if (sb != null)
                    sb.InvalidateJsInstance();
            }
            foreach (var sb in all)
            {
                if (sb != null && sb.script != null)
                    sb.ReloadScript();
            }
        }

        public static string FormatJsException(Exception ex, JavaScript asset)
        {
            if (ex == null) return "Unknown error";

            var path = asset != null ? asset.name : "script";
#if UNITY_EDITOR
            if (asset != null)
            {
                var p = UnityEditor.AssetDatabase.GetAssetPath(asset);
                if (!string.IsNullOrEmpty(p)) path = p;
            }
#endif

            var type = ex.GetType();
            if (type.Name == "JavaScriptException" || type.Name == "ParserException")
            {
                try
                {
                    // Jint 3.1+: prefer JavaScriptStackTrace (Location is a by-ref and fails via reflection)
                    var stackProp = type.GetProperty("JavaScriptStackTrace");
                    if (stackProp?.GetValue(ex) is string jsStack &&
                        TryParseJsStackLocation(jsStack, out var stackLine, out var stackCol))
                        return $"{path}:{stackLine}:{stackCol} {ex.Message}";

                    var lineNumber = type.GetProperty("LineNumber")?.GetValue(ex);
                    var column = type.GetProperty("Column")?.GetValue(ex);
                    if (lineNumber is int ln && ln > 0)
                        return $"{path}:{ln}:{column ?? 0} {ex.Message}";
                }
                catch
                {
                    // fall through
                }
            }

            if (ex.InnerException != null && ex.InnerException != ex)
                return FormatJsException(ex.InnerException, asset);

            return $"{path}: {ex.Message}";
        }

        private static bool TryParseJsStackLocation(string jsStack, out int line, out int col)
        {
            line = 0;
            col = 0;
            if (string.IsNullOrEmpty(jsStack)) return false;

            foreach (var raw in jsStack.Split('\n'))
            {
                var frame = raw.Trim();
                if (frame.Length == 0) continue;
                var m = JsStackFrameRegex.Match(frame);
                if (!m.Success) continue;
                if (!int.TryParse(m.Groups["line"].Value, out line)) continue;
                int.TryParse(m.Groups["col"].Value, out col);
                if (line > 0) return true;
            }
            return false;
        }

        private void RegisterRequire()
        {
            Engine.SetValue("Feather", new
            {
                require = new Func<string, object>(RequireScript),
                findBehaviour = new Func<JsValue, object>(FindJsBehaviour),
                findBehaviours = new Func<JsValue, object>(FindJsBehaviours),
            });
            Engine.Execute("function require(path) { return Feather.require(path); }");
        }

        /// <summary>First active JS instance whose class name matches (string or class ctor).</summary>
        private static object FindJsBehaviour(JsValue classNameOrType)
        {
            var className = ResolveJsClassName(classNameOrType);
            if (string.IsNullOrEmpty(className)) return null;
            foreach (var host in UnityEngine.Object.FindObjectsByType<JavaScriptBehaviour>())
            {
                if (host == null || !MatchesJsClass(host, className)) continue;
                host.EnsureJsInstance();
                if (host.JsInstance != null)
                    return host.JsInstance;
            }
            return null;
        }

        /// <summary>All active JS instances whose class name matches (string or class ctor).</summary>
        private static object FindJsBehaviours(JsValue classNameOrType)
        {
            var className = ResolveJsClassName(classNameOrType);
            if (string.IsNullOrEmpty(className))
                return Array.Empty<object>();

            var list = new List<object>();
            foreach (var host in UnityEngine.Object.FindObjectsByType<JavaScriptBehaviour>())
            {
                if (host == null || !MatchesJsClass(host, className)) continue;
                host.EnsureJsInstance();
                if (host.JsInstance != null)
                    list.Add(host.JsInstance);
            }
            return list.ToArray();
        }

        private static string ResolveJsClassName(JsValue arg)
        {
            if (arg.IsNull() || arg.IsUndefined()) return null;
            if (arg.IsString()) return arg.AsString();
            if (arg.IsObject())
            {
                var name = arg.AsObject().Get("name");
                if (name.IsString())
                {
                    var s = name.AsString();
                    if (!string.IsNullOrEmpty(s)) return s;
                }
            }
            return null;
        }

        private static bool MatchesJsClass(JavaScriptBehaviour host, string className) =>
            host != null && host.MatchesJsClass(className);

        private object RequireScript(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new Exception("Feather.require: path is empty");

            var cacheKey = path.Replace("\\", "/");
            if (_requireCache.TryGetValue(cacheKey, out var cached))
                return cached;

            var resourcesPath = cacheKey;
            if (resourcesPath.EndsWith(".js")) resourcesPath = resourcesPath.Substring(0, resourcesPath.Length - 3);

            JavaScript asset = Resources.Load<JavaScript>(resourcesPath);
            if (asset == null)
                asset = Resources.Load<JavaScript>("Feather/Scripts/" + System.IO.Path.GetFileNameWithoutExtension(path));

            // Player copies may still be TextAsset .txt
            if (asset == null)
            {
                var textAsset = Resources.Load<TextAsset>(resourcesPath)
                    ?? Resources.Load<TextAsset>("Feather/Scripts/" + System.IO.Path.GetFileNameWithoutExtension(path));
                if (textAsset != null)
                {
                    try
                    {
                        Engine.Execute(textAsset.text);
                        _requireCache[cacheKey] = true;
                        return true;
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"{textAsset.name}: {ex.Message}", ex);
                    }
                }
            }

#if UNITY_EDITOR
            if (asset == null)
            {
                var guids = UnityEditor.AssetDatabase.FindAssets(System.IO.Path.GetFileNameWithoutExtension(path) + " t:JavaScript");
                foreach (var guid in guids)
                {
                    var p = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    if (p.EndsWith(".js") || p.EndsWith(".jsu") || p.EndsWith(".jsfeather"))
                    {
                        if (p.Replace("\\", "/").EndsWith(path.Replace("\\", "/")) ||
                            System.IO.Path.GetFileNameWithoutExtension(p) == System.IO.Path.GetFileNameWithoutExtension(path))
                        {
                            asset = UnityEditor.AssetDatabase.LoadAssetAtPath<JavaScript>(p);
                            break;
                        }
                    }
                }
            }
#endif

            if (asset == null)
                throw new Exception($"Feather.require: could not find '{path}'");

            try
            {
                Engine.Execute(asset.text);
                _requireCache[cacheKey] = true;
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception(FormatJsException(ex, asset), ex);
            }
        }

        private void LoadScriptsFromResources()
        {
            foreach (var s in Resources.LoadAll<JavaScript>("Scripts"))
                LoadScript(s);
            foreach (var s in Resources.LoadAll<JavaScript>("Feather/Scripts"))
                LoadScript(s);

            // Legacy TextAsset copies from older build collect
            foreach (var s in Resources.LoadAll<TextAsset>("Feather/Scripts"))
                LoadScriptFromText(s.name, s.text, s.name);
        }

        private void LoadScriptsFromPlayerManifest()
        {
            var manifest = Resources.Load<ScriptManifest>("Feather/ScriptManifest");
            if (manifest == null || manifest.scripts == null) return;
            foreach (var s in manifest.scripts)
            {
                if (s != null) LoadScript(s);
            }
        }

#if UNITY_EDITOR
        private void LoadScriptsFromProject()
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("t:JavaScript", new[] { "Assets" });
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (!(path.EndsWith(".js") || path.EndsWith(".jsu") || path.EndsWith(".jsfeather")))
                    continue;
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<JavaScript>(path);
                if (asset == null) continue;
                var className = GetScriptClassName(asset);
                if (!LoadedScripts.ContainsKey(className))
                    LoadScript(asset);
            }
        }
#endif

        private void LoadScript(JavaScript scriptAsset)
        {
            if (scriptAsset == null) return;
            LoadScriptFromText(scriptAsset.name, scriptAsset.text, FormatAssetPath(scriptAsset), scriptAsset);
        }

        private string LoadScriptFromText(string assetName, string body, string displayPath, JavaScript scriptAsset = null)
        {
            try
            {
                if (!Analyzer.TryAnalyze(body, out var scriptMeta, out var error))
                {
                    Debug.LogError($"[Feather] Failed to analyze {displayPath}: {error}");
                    return null;
                }

                if (!Analyzer.HasJSBehaviour(scriptMeta))
                    return null;

                var className = scriptMeta.Class.Name;
#if UNITY_EDITOR
                var fileName = assetName.Contains('.') ? assetName.Split('.')[0] : assetName;
                if (!string.Equals(className, fileName, StringComparison.Ordinal))
                {
                    Debug.LogWarning(
                        $"[Feather] Class '{className}' in {displayPath} does not match file name '{fileName}'. " +
                        "Prefer matching names (C# convention).");
                }
#endif

                if (LoadedScripts.ContainsKey(className))
                {
                    FeatherSettings.LogScriptLoad($"Skip duplicate class {className} from {assetName}");
                    return null;
                }

                Engine.Execute(body);
                LoadedScripts.Add(className, scriptMeta);
                _scriptContents[className] = body;
                if (scriptAsset != null)
                    _scriptAssets[className] = scriptAsset;
                FeatherSettings.LogScriptLoad($"Loaded {className} from {assetName}");
                return className;
            }
            catch (Exception ex)
            {
                if (scriptAsset != null)
                    Debug.LogError($"[Feather] Failed to load {FormatJsException(ex, scriptAsset)}");
                else
                    Debug.LogError($"[Feather] Failed to load {displayPath}: {ex.Message}");
                return null;
            }
        }

        private string GetScriptClassName(JavaScript scriptAsset)
        {
            if (!string.IsNullOrEmpty(scriptAsset.ClassName))
                return scriptAsset.ClassName;
            try
            {
                if (Analyzer.TryAnalyze(scriptAsset.text, out var meta, out _) && meta?.Class != null)
                    return meta.Class.Name;
            }
            catch { /* ignore */ }
            return scriptAsset.name.Split('.')[0];
        }

        private static string FormatAssetPath(JavaScript asset)
        {
#if UNITY_EDITOR
            var p = UnityEditor.AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(p) ? asset.name : p;
#else
            return asset.name;
#endif
        }
    }
}
