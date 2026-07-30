using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Feather.Analysis;
using Jint;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace Feather
{
    /// <summary>JS-facing Feather utilities (discovery, bundles, scenes, dynamic hosts).</summary>
    public partial class Runtime
    {
        private readonly List<JsValue> _sceneLoadedCallbacks = new List<JsValue>();
        private bool _sceneLoadedHooked;

        private void EnsureSceneLoadedHook()
        {
            if (_sceneLoadedHooked) return;
            SceneManager.sceneLoaded += OnUnitySceneLoaded;
            _sceneLoadedHooked = true;
        }

        private void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_sceneLoadedCallbacks.Count == 0 || Engine == null) return;
            var sceneVal = JsValue.FromObject(Engine, scene);
            var modeVal = JsValue.FromObject(Engine, mode);
            foreach (var cb in _sceneLoadedCallbacks.ToArray())
            {
                try
                {
                    if (cb.AsObject() is Function fn)
                        fn.Call(JsValue.Undefined, new[] { sceneVal, modeVal });
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Feather] onSceneLoaded error: {FormatJsException(ex, null)}");
                }
            }
        }

        private void ClearJsSessionCallbacks()
        {
            _sceneLoadedCallbacks.Clear();
        }

        public string[] ListScripts() => LoadedScripts.Keys.OrderBy(k => k).ToArray();

        public bool UnloadScript(string className)
        {
            if (string.IsNullOrEmpty(className)) return false;

            var had = LoadedScripts.ContainsKey(className)
                || _runtimeScripts.ContainsKey(className)
                || _scriptContents.ContainsKey(className);
            if (!had) return false;

            _unloadedScripts.Add(className);
            _runtimeScripts.Remove(className);
            _scriptContents.Remove(className);
            _scriptAssets.Remove(className);
            LoadedScripts.Remove(className);

            RebuildEngineAndLoadAll();
            if (Application.isPlaying)
                ReloadAllHosts();
            return true;
        }

        public AssetBundle LoadBundleFromMemory(byte[] data, bool replace = false)
        {
            if (data == null || data.Length == 0)
            {
                Debug.LogError("[Feather] LoadBundleFromMemory: data is empty.");
                return null;
            }

            var bundle = AssetBundle.LoadFromMemory(data);
            if (bundle == null)
            {
                Debug.LogError("[Feather] LoadBundleFromMemory: failed to load bundle.");
                return null;
            }

            RegisterScriptsFromBundle(bundle, replace);
            return bundle;
        }

        public void DownloadAndRegister(string url, Action<string, string> onComplete, bool replace = false)
        {
            if (string.IsNullOrEmpty(url))
            {
                onComplete?.Invoke(null, "url is empty");
                return;
            }

            StartCoroutine(DownloadAndRegisterCoroutine(url, onComplete, replace));
        }

        private IEnumerator DownloadAndRegisterCoroutine(string url, Action<string, string> onComplete, bool replace)
        {
            using (var req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
                if (req.result != UnityWebRequest.Result.Success)
#else
                if (req.isNetworkError || req.isHttpError)
#endif
                {
                    onComplete?.Invoke(null, req.error);
                    yield break;
                }

                var text = req.downloadHandler?.text;
                if (string.IsNullOrEmpty(text))
                {
                    onComplete?.Invoke(null, "empty response");
                    yield break;
                }

                var name = System.IO.Path.GetFileNameWithoutExtension(url);
                if (string.IsNullOrEmpty(name) || name.Contains("?"))
                    name = "DownloadedScript";

                var className = RegisterScript(text, name, replace);
                onComplete?.Invoke(className, className == null ? "register failed" : null);
            }
        }

        public ObjectInstance GetBehaviour(UnityEngine.Object unityObject, string className = null)
        {
            if (unityObject == null) return null;

            JavaScriptBehaviour host = null;
            if (unityObject is JavaScriptBehaviour jb)
                host = jb;
            else if (unityObject is GameObject go)
                host = FindHostOnGameObject(go, className);
            else if (unityObject is Component c)
                host = FindHostOnGameObject(c.gameObject, className);

            if (host == null) return null;
            if (!string.IsNullOrEmpty(className) && !host.MatchesJsClass(className))
                return null;

            host.EnsureJsInstance();
            return host.JsInstance;
        }

        private static JavaScriptBehaviour FindHostOnGameObject(GameObject go, string className)
        {
            if (go == null) return null;
            var hosts = go.GetComponents<JavaScriptBehaviour>();
            if (hosts == null || hosts.Length == 0) return null;
            if (string.IsNullOrEmpty(className))
                return hosts[0];
            foreach (var h in hosts)
            {
                if (h != null && h.MatchesJsClass(className))
                    return h;
            }
            return null;
        }

        public object[] FindBehavioursInScene(Scene scene, string className, bool includeInactive = false)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return Array.Empty<object>();

            var roots = scene.GetRootGameObjects();
            var list = new List<object>();
            foreach (var root in roots)
            {
                if (root == null) continue;
                var hosts = root.GetComponentsInChildren<JavaScriptBehaviour>(includeInactive);
                foreach (var host in hosts)
                {
                    if (host == null) continue;
                    if (!string.IsNullOrEmpty(className) && !host.MatchesJsClass(className))
                        continue;
                    host.EnsureJsInstance();
                    if (host.JsInstance != null)
                        list.Add(host.JsInstance);
                }
            }
            return list.ToArray();
        }

        public ObjectInstance CreateBehaviour(GameObject go, JavaScript scriptAsset)
        {
            if (go == null || scriptAsset == null) return null;

            if (!string.IsNullOrEmpty(scriptAsset.text))
            {
                var className = !string.IsNullOrEmpty(scriptAsset.ClassName)
                    ? scriptAsset.ClassName
                    : scriptAsset.name;
                if (!string.IsNullOrEmpty(className) && !LoadedScripts.ContainsKey(className))
                    RegisterScript(scriptAsset);
            }

            // Defer Awake until script is assigned
            var wasActive = go.activeSelf;
            if (wasActive) go.SetActive(false);

            var host = go.AddComponent<JavaScriptBehaviour>();
            host.script = scriptAsset;

            if (wasActive)
                go.SetActive(true);
            else
                host.EnsureJsInstance();

            return host.JsInstance;
        }

        public ObjectInstance CreateBehaviour(GameObject go, string className)
        {
            if (go == null || string.IsNullOrEmpty(className)) return null;
            if (!LoadedScripts.ContainsKey(className))
            {
                Debug.LogError($"[Feather] CreateBehaviour: class '{className}' is not loaded.");
                return null;
            }

            JavaScript asset = null;
            if (_scriptAssets.TryGetValue(className, out var existing))
                asset = existing;

            if (asset == null && _scriptContents.TryGetValue(className, out var source))
            {
                asset = ScriptableObject.CreateInstance<JavaScript>();
                asset.SetImportData(source, className, true);
                asset.name = className;
            }

            if (asset == null)
            {
                Debug.LogError($"[Feather] CreateBehaviour: no source available for '{className}'.");
                return null;
            }

            return CreateBehaviour(go, asset);
        }

        public void ReloadAll()
        {
            if (Application.isPlaying)
                ReloadAllHosts();
        }

        public object WaitForSecondsYield(float seconds) =>
            seconds > 0f ? (object)new WaitForSeconds(seconds) : null;

        public object WaitForEndOfFrameYield() => new WaitForEndOfFrame();

        public object WaitUntilYield(JsValue predicate)
        {
            if (IsJsMissing(predicate) || !(predicate.AsObject() is Function fn))
                return null;
            return new WaitUntil(() =>
            {
                try
                {
                    var result = fn.Call(JsValue.Undefined, Array.Empty<JsValue>());
                    return result.IsBoolean() && result.AsBoolean();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Feather] waitUntil predicate error: {FormatJsException(ex, null)}");
                    return true;
                }
            });
        }

        public object WaitWhileYield(JsValue predicate)
        {
            if (IsJsMissing(predicate) || !(predicate.AsObject() is Function fn))
                return null;
            return new WaitWhile(() =>
            {
                try
                {
                    var result = fn.Call(JsValue.Undefined, Array.Empty<JsValue>());
                    return result.IsBoolean() && result.AsBoolean();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Feather] waitWhile predicate error: {FormatJsException(ex, null)}");
                    return false;
                }
            });
        }

        // ── JS bindings ──────────────────────────────────────────────────

        private void RegisterFeatherApi()
        {
            EnsureSceneLoadedHook();
            Engine.SetValue("Feather", new
            {
                require = new Func<string, object>(RequireScript),
                findBehaviour = new Func<JsValue, JsValue, object>(FindJsBehaviour),
                findBehaviours = new Func<JsValue, JsValue, object>(FindJsBehaviours),
                findBehavioursInScene = new Func<JsValue, JsValue, JsValue, object>(FindBehavioursInSceneFromJs),
                getBehaviour = new Func<JsValue, JsValue, object>(GetBehaviourFromJs),
                createBehaviour = new Func<JsValue, JsValue, object>(CreateBehaviourFromJs),
                listScripts = new Func<object>(ListScripts),
                getScript = new Func<JsValue, object>(GetScriptFromJs),
                isScriptLoaded = new Func<JsValue, bool>(IsScriptLoadedFromJs),
                registerScript = new Func<JsValue, JsValue, JsValue, object>(RegisterScriptFromJs),
                registerScriptsFromBundle = new Func<JsValue, JsValue, object>(RegisterScriptsFromBundleFromJs),
                loadBundleFromFile = new Func<JsValue, JsValue, object>(LoadBundleFromFileFromJs),
                loadBundleFromMemory = new Func<JsValue, JsValue, object>(LoadBundleFromMemoryFromJs),
                downloadAndRegister = new Action<JsValue, JsValue, JsValue>(DownloadAndRegisterFromJs),
                unloadScript = new Func<JsValue, bool>(UnloadScriptFromJs),
                reloadAll = new Action(ReloadAll),
                onSceneLoaded = new Action<JsValue>(OnSceneLoadedFromJs),
                waitForSeconds = new Func<float, object>(WaitForSecondsYield),
                waitForEndOfFrame = new Func<object>(WaitForEndOfFrameYield),
                waitUntil = new Func<JsValue, object>(WaitUntilYield),
                waitWhile = new Func<JsValue, object>(WaitWhileYield),
            });
            Engine.Execute("function require(path) { return Feather.require(path); }");
        }

        private object RegisterScriptFromJs(JsValue sourceOrAsset, JsValue nameOrReplace, JsValue replaceArg)
        {
            if (IsJsMissing(sourceOrAsset))
                return null;

            if (sourceOrAsset.IsString())
            {
                var source = sourceOrAsset.AsString();
                string assetName = null;
                var replace = false;

                if (!IsJsMissing(nameOrReplace) && nameOrReplace.IsString())
                    assetName = nameOrReplace.AsString();
                else if (!IsJsMissing(nameOrReplace) && nameOrReplace.IsBoolean())
                    replace = nameOrReplace.AsBoolean();

                if (!IsJsMissing(replaceArg) && replaceArg.IsBoolean())
                    replace = replaceArg.AsBoolean();

                return RegisterScript(source, assetName, replace);
            }

            var clr = sourceOrAsset.ToObject();
            if (clr is JavaScript js)
            {
                var replace = !IsJsMissing(nameOrReplace) && nameOrReplace.IsBoolean() && nameOrReplace.AsBoolean();
                if (!IsJsMissing(replaceArg) && replaceArg.IsBoolean())
                    replace = replaceArg.AsBoolean();
                return RegisterScript(js, replace);
            }

            Debug.LogWarning("[Feather] registerScript expects a source string or a JavaScript asset.");
            return null;
        }

        private object RegisterScriptsFromBundleFromJs(JsValue bundleArg, JsValue replaceArg)
        {
            var clr = IsJsMissing(bundleArg) ? null : bundleArg.ToObject();
            if (clr is not AssetBundle bundle)
            {
                Debug.LogWarning("[Feather] registerScriptsFromBundle expects a Unity AssetBundle.");
                return 0;
            }

            var replace = !IsJsMissing(replaceArg) && replaceArg.IsBoolean() && replaceArg.AsBoolean();
            return RegisterScriptsFromBundle(bundle, replace);
        }

        private object LoadBundleFromFileFromJs(JsValue pathArg, JsValue replaceArg)
        {
            if (IsJsMissing(pathArg) || !pathArg.IsString())
            {
                Debug.LogWarning("[Feather] loadBundleFromFile expects a file path string.");
                return null;
            }

            var replace = !IsJsMissing(replaceArg) && replaceArg.IsBoolean() && replaceArg.AsBoolean();
            return LoadBundleFromFile(pathArg.AsString(), replace);
        }

        private bool IsScriptLoadedFromJs(JsValue classNameOrType)
        {
            var className = ResolveJsClassName(classNameOrType);
            return !string.IsNullOrEmpty(className) && LoadedScripts.ContainsKey(className);
        }

        private static string ResolveJsClassName(JsValue arg)
        {
            if (IsJsMissing(arg)) return null;
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

        private object GetScriptFromJs(JsValue classNameOrType)
        {
            var className = ResolveJsClassName(classNameOrType);
            if (string.IsNullOrEmpty(className) || !LoadedScripts.ContainsKey(className))
                return JsValue.Undefined;
            return Engine.GetValue(className);
        }

        private bool UnloadScriptFromJs(JsValue classNameOrType)
        {
            var className = ResolveJsClassName(classNameOrType);
            return UnloadScript(className);
        }

        private object GetBehaviourFromJs(JsValue unityObjectArg, JsValue classArg)
        {
            if (IsJsMissing(unityObjectArg))
                return null;
            var clr = unityObjectArg.ToObject() as UnityEngine.Object;
            var className = ResolveJsClassName(classArg);
            return GetBehaviour(clr, className);
        }

        private object CreateBehaviourFromJs(JsValue goArg, JsValue scriptOrClass)
        {
            var go = IsJsMissing(goArg) ? null : goArg.ToObject() as GameObject;
            if (go == null)
            {
                Debug.LogWarning("[Feather] createBehaviour expects a GameObject.");
                return null;
            }

            if (!IsJsMissing(scriptOrClass) && scriptOrClass.IsString())
                return CreateBehaviour(go, scriptOrClass.AsString());

            var clr = IsJsMissing(scriptOrClass) ? null : scriptOrClass.ToObject();
            if (clr is JavaScript js)
                return CreateBehaviour(go, js);

            var className = ResolveJsClassName(scriptOrClass);
            if (!string.IsNullOrEmpty(className))
                return CreateBehaviour(go, className);

            Debug.LogWarning("[Feather] createBehaviour expects a JavaScript asset, class name, or class ctor.");
            return null;
        }

        private object FindBehavioursInSceneFromJs(JsValue sceneArg, JsValue classArg, JsValue optionsArg)
        {
            var className = ResolveJsClassName(classArg);
            var includeInactive = ReadIncludeInactive(optionsArg);
            Scene scene = default;

            if (!IsJsMissing(sceneArg) && sceneArg.IsString())
            {
                scene = SceneManager.GetSceneByName(sceneArg.AsString());
            }
            else if (!IsJsMissing(sceneArg))
            {
                var clr = sceneArg.ToObject();
                if (clr is Scene s)
                    scene = s;
            }

            return FindBehavioursInScene(scene, className, includeInactive);
        }

        private void OnSceneLoadedFromJs(JsValue callback)
        {
            if (IsJsMissing(callback)) return;
            if (!(callback.AsObject() is Function))
            {
                Debug.LogWarning("[Feather] onSceneLoaded expects a function.");
                return;
            }
            _sceneLoadedCallbacks.Add(callback);
        }

        private object LoadBundleFromMemoryFromJs(JsValue dataArg, JsValue replaceArg)
        {
            var bytes = TryReadBytes(dataArg);
            if (bytes == null)
            {
                Debug.LogWarning("[Feather] loadBundleFromMemory expects a byte array.");
                return null;
            }
            var replace = !IsJsMissing(replaceArg) && replaceArg.IsBoolean() && replaceArg.AsBoolean();
            return LoadBundleFromMemory(bytes, replace);
        }

        private void DownloadAndRegisterFromJs(JsValue urlArg, JsValue callbackArg, JsValue replaceArg)
        {
            if (IsJsMissing(urlArg) || !urlArg.IsString())
            {
                Debug.LogWarning("[Feather] downloadAndRegister expects a url string.");
                return;
            }

            Function callback = null;
            if (!IsJsMissing(callbackArg) && callbackArg.AsObject() is Function fn)
                callback = fn;

            var replace = !IsJsMissing(replaceArg) && replaceArg.IsBoolean() && replaceArg.AsBoolean();
            DownloadAndRegister(urlArg.AsString(), (className, error) =>
            {
                if (callback == null) return;
                try
                {
                    callback.Call(
                        JsValue.Undefined,
                        new[]
                        {
                            className != null ? (JsValue)className : JsValue.Null,
                            error != null ? (JsValue)error : JsValue.Null
                        });
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Feather] downloadAndRegister callback error: {FormatJsException(ex, null)}");
                }
            }, replace);
        }

        private static byte[] TryReadBytes(JsValue value)
        {
            if (IsJsMissing(value)) return null;
            var clr = value.ToObject();
            if (clr is byte[] bytes) return bytes;
            if (clr is IList list)
            {
                var result = new byte[list.Count];
                for (var i = 0; i < list.Count; i++)
                    result[i] = Convert.ToByte(list[i]);
                return result;
            }
            return null;
        }

        private static bool IsJsMissing(JsValue value) =>
            value == null || value.IsNull() || value.IsUndefined();

        private static bool ReadIncludeInactive(JsValue optionsArg)
        {
            if (IsJsMissing(optionsArg) || !optionsArg.IsObject())
                return false;
            var flag = optionsArg.AsObject().Get("includeInactive");
            return flag.IsBoolean() && flag.AsBoolean();
        }

        private object FindJsBehaviour(JsValue classNameOrType, JsValue optionsArg)
        {
            var className = ResolveJsClassName(classNameOrType);
            if (string.IsNullOrEmpty(className)) return null;
            var includeInactive = ReadIncludeInactive(optionsArg);
            var hosts = includeInactive
                ? UnityEngine.Object.FindObjectsByType<JavaScriptBehaviour>(FindObjectsInactive.Include)
                : UnityEngine.Object.FindObjectsByType<JavaScriptBehaviour>(FindObjectsInactive.Exclude);
            foreach (var host in hosts)
            {
                if (host == null || !MatchesJsClass(host, className)) continue;
                host.EnsureJsInstance();
                if (host.JsInstance != null)
                    return host.JsInstance;
            }
            return null;
        }

        private object FindJsBehaviours(JsValue classNameOrType, JsValue optionsArg)
        {
            var className = ResolveJsClassName(classNameOrType);
            if (string.IsNullOrEmpty(className))
                return Array.Empty<object>();

            var includeInactive = ReadIncludeInactive(optionsArg);
            var hosts = includeInactive
                ? UnityEngine.Object.FindObjectsByType<JavaScriptBehaviour>(FindObjectsInactive.Include)
                : UnityEngine.Object.FindObjectsByType<JavaScriptBehaviour>(FindObjectsInactive.Exclude);

            var list = new List<object>();
            foreach (var host in hosts)
            {
                if (host == null || !MatchesJsClass(host, className)) continue;
                host.EnsureJsInstance();
                if (host.JsInstance != null)
                    list.Add(host.JsInstance);
            }
            return list.ToArray();
        }
    }
}
