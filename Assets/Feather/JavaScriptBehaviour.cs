using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Feather.Analysis;
using Jint;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using UnityEngine;
using UnityEngine.Events;

namespace Feather
{
    [AddComponentMenu("")]
    public class JavaScriptBehaviour : MonoBehaviour
    {
        public enum BridgeKind
        {
            UnityObject = 0,
            Float = 1,
            Int = 2,
            Bool = 3,
            String = 4,
            Vector2 = 5,
            Vector3 = 6,
            Vector4 = 7,
            Color = 8,
            UnityEvent = 9
        }

        [Serializable]
        public class BridgeProperties
        {
            public string name;
            public BridgeKind kind = BridgeKind.UnityObject;
            public bool isList;
            public bool hasSerializedValue;

            public UnityEngine.Object gameObject;
            public Component component;
            public UnityEvent unityEvent = new UnityEvent();

            public List<UnityEngine.Object> gameObjectList = new List<UnityEngine.Object>();
            public List<Component> componentList = new List<Component>();
            public List<UnityEvent> unityEventList = new List<UnityEvent>();

            public float floatValue;
            public int intValue;
            public bool boolValue;
            public string stringValue = string.Empty;
            public Vector2 vector2Value;
            public Vector3 vector3Value;
            public Vector4 vector4Value;
            public Color colorValue = Color.white;
        }

        [SerializeField] public BridgeProperties[] properties = Array.Empty<BridgeProperties>();
        [SerializeField] public JavaScript script;

        private ScriptMeta _scriptMeta;
        private ObjectInstance _jsBehaviourInstance;
        private readonly Dictionary<string, Function> _gameObjectLifeCycleCallbacks
            = new Dictionary<string, Function>();
        /// <summary>Timers from invoke / invokeRepeating (cleared by cancelInvoke).</summary>
        private readonly List<Coroutine> _invokeCoroutines = new List<Coroutine>();
        /// <summary>Generator / interval coroutines from startCoroutine.</summary>
        private readonly List<Coroutine> _jsCoroutines = new List<Coroutine>();
        private readonly Dictionary<Coroutine, ObjectInstance> _coroutineIterators
            = new Dictionary<Coroutine, ObjectInstance>();
        private static readonly JsValue[] NoArgs = Array.Empty<JsValue>();

        private static readonly string[] LifecycleMethodNames =
        {
            "Awake", "Start", "OnEnable", "OnDisable", "Update", "LateUpdate", "FixedUpdate", "OnDestroy",
            "OnCollisionEnter", "OnCollisionStay", "OnCollisionExit",
            "OnTriggerEnter", "OnTriggerStay", "OnTriggerExit",
            "OnCollisionEnter2D", "OnCollisionStay2D", "OnCollisionExit2D",
            "OnTriggerEnter2D", "OnTriggerStay2D", "OnTriggerExit2D",
            "OnBecameVisible", "OnBecameInvisible", "OnWillRenderObject", "OnRenderObject",
            "OnApplicationFocus", "OnApplicationPause", "OnApplicationQuit",
            "OnGUI", "OnDrawGizmos", "OnDrawGizmosSelected",
            "OnAnimatorIK", "OnAnimatorMove"
        };

        public string JsClassName =>
            _scriptMeta?.Class?.Name ?? (script != null ? script.name.Split('.')[0] : null);

        public ObjectInstance JsInstance => _jsBehaviourInstance;

        private bool _awakeInvoked;

        /// <summary>True when this host's script class name (or asset stem) matches <paramref name="className"/>.</summary>
        public bool MatchesJsClass(string className)
        {
            if (string.IsNullOrEmpty(className)) return true;
            if (string.Equals(JsClassName, className, StringComparison.Ordinal))
                return true;
            if (script == null) return false;
            var fileStem = script.name;
            var dot = fileStem.IndexOf('.');
            if (dot >= 0) fileStem = fileStem.Substring(0, dot);
            return string.Equals(fileStem, className, StringComparison.Ordinal);
        }

        /// <summary>Create the JS instance if needed (for cross-refs before peer Awake).</summary>
        internal void EnsureJsInstance()
        {
            if (_jsBehaviourInstance == null)
                InitializeFromScript(callAwake: false);
        }

        /// <summary>Drop the JS instance after an engine rebuild (before peers reload).</summary>
        internal void InvalidateJsInstance()
        {
            StopAllTrackedCoroutines();
            _gameObjectLifeCycleCallbacks.Clear();
            _jsBehaviourInstance = null;
            _awakeInvoked = false;
        }

        private void Awake()
        {
            InitializeFromScript(callAwake: true);
        }

        private void Start() => CallLifecycle("Start");
        private void OnEnable() => CallLifecycle("OnEnable");
        private void OnDisable() => CallLifecycle("OnDisable");
        private void Update() => CallLifecycle("Update");
        private void LateUpdate() => CallLifecycle("LateUpdate");
        private void FixedUpdate() => CallLifecycle("FixedUpdate");

        private void OnDestroy()
        {
            StopAllTrackedCoroutines();
            CallLifecycle("OnDestroy");
        }

        private void OnCollisionEnter(Collision collision) => CallLifecycle("OnCollisionEnter", collision);
        private void OnCollisionStay(Collision collision) => CallLifecycle("OnCollisionStay", collision);
        private void OnCollisionExit(Collision collision) => CallLifecycle("OnCollisionExit", collision);
        private void OnTriggerEnter(Collider other) => CallLifecycle("OnTriggerEnter", other);
        private void OnTriggerStay(Collider other) => CallLifecycle("OnTriggerStay", other);
        private void OnTriggerExit(Collider other) => CallLifecycle("OnTriggerExit", other);
        private void OnCollisionEnter2D(Collision2D collision) => CallLifecycle("OnCollisionEnter2D", collision);
        private void OnCollisionStay2D(Collision2D collision) => CallLifecycle("OnCollisionStay2D", collision);
        private void OnCollisionExit2D(Collision2D collision) => CallLifecycle("OnCollisionExit2D", collision);
        private void OnTriggerEnter2D(Collider2D other) => CallLifecycle("OnTriggerEnter2D", other);
        private void OnTriggerStay2D(Collider2D other) => CallLifecycle("OnTriggerStay2D", other);
        private void OnTriggerExit2D(Collider2D other) => CallLifecycle("OnTriggerExit2D", other);
        private void OnBecameVisible() => CallLifecycle("OnBecameVisible");
        private void OnBecameInvisible() => CallLifecycle("OnBecameInvisible");
        private void OnWillRenderObject() => CallLifecycle("OnWillRenderObject");
        private void OnRenderObject() => CallLifecycle("OnRenderObject");
        private void OnApplicationFocus(bool hasFocus) => CallLifecycle("OnApplicationFocus", hasFocus);
        private void OnApplicationPause(bool pauseStatus) => CallLifecycle("OnApplicationPause", pauseStatus);
        private void OnApplicationQuit() => CallLifecycle("OnApplicationQuit");
        private void OnGUI() => CallLifecycle("OnGUI");
        private void OnDrawGizmos() => CallLifecycle("OnDrawGizmos");
        private void OnDrawGizmosSelected() => CallLifecycle("OnDrawGizmosSelected");
        private void OnAnimatorIK(int layerIndex) => CallLifecycle("OnAnimatorIK", layerIndex);
        private void OnAnimatorMove() => CallLifecycle("OnAnimatorMove");

        /// <summary>UnityEvent trampoline — wire persistent calls to these from the inspector.</summary>
        public void InvokeJs(string methodName) => InvokeJsMethod(methodName);

        public void InvokeJs0() => InvokeJsMethod("OnJsEvent");
        public void InvokeJs1() => InvokeJsMethod("OnJsEvent1");
        public void InvokeJs2() => InvokeJsMethod("OnJsEvent2");
        public void InvokeJs3() => InvokeJsMethod("OnJsEvent3");

        /// <summary>Call a named JS instance method (for UnityEvent persistent targets).</summary>
        public void CallJsMethod(string methodName) => InvokeJsMethod(methodName);

        public void ReloadScript()
        {
            InvalidateJsInstance();
            InitializeFromScript(callAwake: true);
            // Unity will not re-fire Start/OnEnable for an already-enabled host
            if (isActiveAndEnabled)
            {
                CallLifecycle("OnEnable");
                CallLifecycle("Start");
            }
        }

        private void InitializeFromScript(bool callAwake)
        {
            if (_jsBehaviourInstance == null)
            {
                if (script == null)
                {
                    Debug.LogError(FormatLog("No JavaScript file assigned."));
                    return;
                }

                if (Runtime.Instance == null)
                {
                    Debug.LogError(FormatLog("Feather Runtime is not available."));
                    return;
                }

                var className = ResolveClassName();
                if (string.IsNullOrEmpty(className))
                {
                    Debug.LogError(FormatLog("Could not resolve JavaScript class name."));
                    return;
                }

                // AssetBundle / late-loaded scripts: register from the assigned asset on first use
                if (!Runtime.Instance.LoadedScripts.ContainsKey(className) &&
                    script != null &&
                    !string.IsNullOrEmpty(script.text))
                {
                    Runtime.Instance.RegisterScript(script);
                }

                if (!Runtime.Instance.LoadedScripts.ContainsKey(className))
                {
                    Debug.LogError(FormatLog(
                        $"JavaScript class '{className}' not found. " +
                        $"Available: {string.Join(", ", Runtime.Instance.LoadedScripts.Keys)}"));
                    return;
                }

                _scriptMeta = Runtime.Instance.LoadedScripts[className];
                _jsBehaviourInstance = Runtime.Instance.InstantiateClass(_scriptMeta.Class);
                _jsBehaviourInstance.Set("gameObject", JsValue.FromObject(Runtime.Instance.Engine, gameObject));
                _jsBehaviourInstance.Set("transform", JsValue.FromObject(Runtime.Instance.Engine, transform));

                BindJsHelpers();
                InjectSerializedProperties();
                CacheLifecycleCallbacks();
            }

            if (callAwake && !_awakeInvoked)
            {
                _awakeInvoked = true;
                CallLifecycle("Awake");
            }
        }

        private string ResolveClassName()
        {
            if (script == null) return null;
            if (!string.IsNullOrEmpty(script.ClassName))
                return script.ClassName;
            try
            {
                if (Analyzer.TryAnalyze(script.text, out var meta, out _) && meta?.Class != null)
                    return meta.Class.Name;
            }
            catch { /* fall through */ }
            return script.name.Split('.')[0];
        }

        private void BindJsHelpers()
        {
            var engine = Runtime.Instance.Engine;
            _jsBehaviourInstance.Set("invoke", JsValue.FromObject(engine, new Action<JsValue, float>(InvokeJsCallback)));
            _jsBehaviourInstance.Set("invokeRepeating", JsValue.FromObject(engine, new Action<JsValue, float, float>(InvokeRepeatingJsCallback)));
            _jsBehaviourInstance.Set("cancelInvoke", JsValue.FromObject(engine, new Action(CancelAllInvokes)));
            // Overloads via optional 2nd arg: generator/iterator, or timer callback + intervalSeconds
            _jsBehaviourInstance.Set("startCoroutine", JsValue.FromObject(engine, new Func<JsValue, JsValue, object>(StartJsCoroutine)));
            _jsBehaviourInstance.Set("stopCoroutine", JsValue.FromObject(engine, new Action<object>(StopJsCoroutine)));
            _jsBehaviourInstance.Set("stopAllCoroutines", JsValue.FromObject(engine, new Action(StopAllGeneratorCoroutines)));
            _jsBehaviourInstance.Set("wait", JsValue.FromObject(engine, new Func<float, object>(seconds =>
                seconds > 0f ? (object)new WaitForSeconds(seconds) : null)));
            _jsBehaviourInstance.Set("nextFrame", JsValue.FromObject(engine, new Func<object>(() => null)));

            // this.enabled ↔ MonoBehaviour.enabled
            var getter = new ClrFunction(engine, "get enabled",
                (_, _) => JsValue.FromObject(engine, enabled),
                0, PropertyFlag.Configurable);
            var setter = new ClrFunction(engine, "set enabled",
                (_, args) =>
                {
                    if (args != null && args.Length > 0 && args[0].IsBoolean())
                        enabled = args[0].AsBoolean();
                    return JsValue.Undefined;
                },
                1, PropertyFlag.Configurable);
            _jsBehaviourInstance.DefineOwnProperty("enabled",
                new GetSetPropertyDescriptor(getter, setter, enumerable: true, configurable: true));
        }

        private void InjectSerializedProperties()
        {
            // Bridge array is authoritative (Inspector values). Meta only supplies list/decorator hints.
            if (properties == null || properties.Length == 0)
                return;

            var engine = Runtime.Instance.Engine;
            var metaProps = _scriptMeta?.Class?.Properties;

            foreach (var match in properties)
            {
                if (string.IsNullOrEmpty(match.name))
                    continue;

                var meta = metaProps?.FirstOrDefault(p => p.Name == match.name);

                switch (match.kind)
                {
                    case BridgeKind.UnityEvent:
                        if (match.unityEvent != null)
                            _jsBehaviourInstance.Set(match.name, JsValue.FromObject(engine, match.unityEvent));
                        break;
                    case BridgeKind.UnityObject:
                        InjectUnityObjectProperty(match, meta, engine);
                        break;
                    default:
                        InjectPrimitiveProperty(match, engine);
                        break;
                }
            }
        }

        private void InjectUnityObjectProperty(BridgeProperties match, Analysis.Property meta, Engine engine)
        {
            var isList = match.isList || (meta != null && meta.IsArray);
            var decorator = meta?.Decorator;
            var isJsBehaviourRef = decorator == "JavaScriptBehaviour" || !string.IsNullOrEmpty(meta?.JsBehaviourClass);

            if (isList)
            {
                if (decorator == "UnityEvent")
                {
                    var list = match.unityEventList != null ? match.unityEventList.ToArray() : Array.Empty<UnityEvent>();
                    _jsBehaviourInstance.Set(match.name, JsValue.FromObject(engine, list));
                    return;
                }

                // JS class lists (`others = List(Coin)`) → peer JS instances
                if (isJsBehaviourRef)
                {
                    var hosts = match.componentList ?? new List<Component>();
                    var instances = new List<object>();
                    foreach (var c in hosts)
                    {
                        if (c is JavaScriptBehaviour host)
                        {
                            host.EnsureJsInstance();
                            if (host.JsInstance != null)
                                instances.Add(host.JsInstance);
                        }
                    }
                    _jsBehaviourInstance.Set(match.name, JsValue.FromObject(engine, instances.ToArray()));
                    return;
                }

                // GameObject lists and non-Component assets (Texture2D, …) use gameObjectList
                var useObjectList = decorator == "GameObject"
                    || match.componentList == null
                    || match.componentList.Count == 0;
                if (useObjectList && match.gameObjectList != null)
                {
                    _jsBehaviourInstance.Set(match.name, JsValue.FromObject(engine, match.gameObjectList.ToArray()));
                    return;
                }

                var components = match.componentList != null ? match.componentList.ToArray() : Array.Empty<Component>();
                _jsBehaviourInstance.Set(match.name, JsValue.FromObject(engine, components));
                return;
            }

            // JS class refs (`other = Coin`) → inject the peer JS instance (or clear)
            if (isJsBehaviourRef)
            {
                if (match.component is JavaScriptBehaviour host)
                {
                    host.EnsureJsInstance();
                    if (host.JsInstance != null)
                    {
                        _jsBehaviourInstance.Set(match.name, host.JsInstance);
                        return;
                    }
                }
                _jsBehaviourInstance.Set(match.name, JsValue.Undefined);
                return;
            }

            var objectToInject = match.gameObject != null ? match.gameObject : (UnityEngine.Object)match.component;
            _jsBehaviourInstance.Set(
                match.name,
                objectToInject != null
                    ? JsValue.FromObject(engine, objectToInject)
                    : JsValue.Undefined);
        }

        private void InjectPrimitiveProperty(BridgeProperties match, Engine engine)
        {
            object value = match.kind switch
            {
                BridgeKind.Float => match.floatValue,
                BridgeKind.Int => match.intValue,
                BridgeKind.Bool => match.boolValue,
                BridgeKind.String => match.stringValue ?? string.Empty,
                BridgeKind.Vector2 => match.vector2Value,
                BridgeKind.Vector3 => match.vector3Value,
                BridgeKind.Vector4 => match.vector4Value,
                BridgeKind.Color => match.colorValue,
                _ => null
            };
            if (value != null)
                _jsBehaviourInstance.Set(match.name, JsValue.FromObject(engine, value));
        }

        private void CacheLifecycleCallbacks()
        {
            _gameObjectLifeCycleCallbacks.Clear();
            if (_scriptMeta?.Class?.Methods == null) return;

            foreach (var methodName in _scriptMeta.Class.Methods)
            {
                if (!LifecycleMethodNames.Contains(methodName)) continue;
                var fn = _jsBehaviourInstance.Get(methodName);
                if (fn.IsObject() && fn.AsObject() is Function fi)
                    _gameObjectLifeCycleCallbacks[methodName] = fi;
            }
        }

        private void CallLifecycle(string name, object arg = null)
        {
            if (!_gameObjectLifeCycleCallbacks.TryGetValue(name, out var fn) || _jsBehaviourInstance == null)
                return;
            try
            {
                if (arg == null)
                    fn.Call(_jsBehaviourInstance, NoArgs);
                else
                    fn.Call(_jsBehaviourInstance, new[] { JsValue.FromObject(Runtime.Instance.Engine, arg) });
            }
            catch (Exception ex)
            {
                Debug.LogError(FormatLog($"Error in {name}: {Runtime.FormatJsException(ex, script)}"));
            }
        }

        private void InvokeJsMethod(string methodName)
        {
            if (_jsBehaviourInstance == null || string.IsNullOrEmpty(methodName)) return;
            try
            {
                var fn = _jsBehaviourInstance.Get(methodName);
                if (fn.IsObject() && fn.AsObject() is Function fi)
                    fi.Call(_jsBehaviourInstance, NoArgs);
                else
                    Debug.LogWarning(FormatLog($"JS method '{methodName}' not found."));
            }
            catch (Exception ex)
            {
                Debug.LogError(FormatLog($"Error calling {methodName}: {Runtime.FormatJsException(ex, script)}"));
            }
        }

        private void InvokeJsCallback(JsValue callback, float delay)
        {
            if (callback.IsNull() || callback.IsUndefined()) return;
            var co = StartCoroutine(InvokeDelayed(callback, delay, 0f, once: true));
            _invokeCoroutines.Add(co);
        }

        private void InvokeRepeatingJsCallback(JsValue callback, float delay, float interval)
        {
            if (callback.IsNull() || callback.IsUndefined()) return;
            var co = StartCoroutine(InvokeDelayed(callback, delay, interval, once: false));
            _invokeCoroutines.Add(co);
        }

        /// <summary>
        /// JS: <c>startCoroutine(iterator)</c>, <c>startCoroutine(function*)</c>,
        /// or timer mode <c>startCoroutine(fn, intervalSeconds)</c>.
        /// Yields: <c>null</c>/undefined → next frame; number → WaitForSeconds; YieldInstruction → as-is.
        /// </summary>
        private object StartJsCoroutine(JsValue callback, JsValue intervalArg)
        {
            if (callback.IsNull() || callback.IsUndefined()) return null;

            if (TryGetIterator(callback, out var iterator))
                return TrackGeneratorCoroutine(StartCoroutine(RunJsIterator(iterator)), iterator);

            if (!(callback.AsObject() is Function fi))
            {
                Debug.LogWarning(FormatLog("startCoroutine expects a generator, iterator, or function."));
                return null;
            }

            // Timer mode: plain callbacks only (do not Call — that would run them early)
            if (intervalArg.IsNumber())
            {
                var interval = (float)intervalArg.AsNumber();
                return TrackGeneratorCoroutine(
                    StartCoroutine(InvokeDelayed(callback, 0f, Mathf.Max(0.0001f, interval), once: false)),
                    null);
            }

            // Generator function / factory: call once, drive the returned iterator
            try
            {
                var produced = fi.Call(_jsBehaviourInstance ?? JsValue.Undefined, NoArgs);
                if (TryGetIterator(produced, out iterator))
                    return TrackGeneratorCoroutine(StartCoroutine(RunJsIterator(iterator)), iterator);
            }
            catch (Exception ex)
            {
                Debug.LogError(FormatLog($"startCoroutine error: {Runtime.FormatJsException(ex, script)}"));
                return null;
            }

            Debug.LogWarning(FormatLog(
                "startCoroutine: pass a generator (function*), an iterator, or a callback with intervalSeconds."));
            return null;
        }

        private static bool TryGetIterator(JsValue value, out ObjectInstance iterator)
        {
            iterator = null;
            if (value.IsNull() || value.IsUndefined() || !value.IsObject())
                return false;

            var obj = value.AsObject();
            var next = obj.Get("next");
            if (next.IsObject() && next.AsObject() is Function)
            {
                iterator = obj;
                return true;
            }

            return false;
        }

        private IEnumerator RunJsIterator(ObjectInstance iterator)
        {
            while (true)
            {
                JsValue step;
                try
                {
                    var next = iterator.Get("next");
                    if (!(next.AsObject() is Function nextFn))
                        yield break;
                    step = nextFn.Call(iterator, NoArgs);
                }
                catch (Exception ex)
                {
                    Debug.LogError(FormatLog($"coroutine error: {Runtime.FormatJsException(ex, script)}"));
                    yield break;
                }

                if (!step.IsObject())
                    yield break;

                var stepObj = step.AsObject();
                var done = stepObj.Get("done");
                if (done.IsBoolean() && done.AsBoolean())
                    yield break;

                yield return ToYieldInstruction(stepObj.Get("value"));
            }
        }

        private object ToYieldInstruction(JsValue value)
        {
            if (value.IsNull() || value.IsUndefined())
                return null;

            if (value.IsNumber())
            {
                var seconds = (float)value.AsNumber();
                return seconds > 0f ? (object)new WaitForSeconds(seconds) : null;
            }

            if (value.IsObject())
            {
                var clr = value.ToObject();
                if (clr is YieldInstruction || clr is CustomYieldInstruction || clr is IEnumerator)
                    return clr;
            }

            Debug.LogWarning(FormatLog(
                $"coroutine yield ignored (use null, seconds, or a YieldInstruction): {value}"));
            return null;
        }

        private IEnumerator InvokeDelayed(JsValue callback, float delay, float interval, bool once)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            while (true)
            {
                try
                {
                    if (callback.AsObject() is Function fi)
                        fi.Call(_jsBehaviourInstance ?? JsValue.Undefined, NoArgs);
                }
                catch (Exception ex)
                {
                    Debug.LogError(FormatLog($"invoke error: {Runtime.FormatJsException(ex, script)}"));
                    yield break;
                }

                if (once || interval <= 0f) yield break;
                yield return new WaitForSeconds(interval);
            }
        }

        private object TrackGeneratorCoroutine(Coroutine co, ObjectInstance iterator)
        {
            _jsCoroutines.Add(co);
            if (iterator != null)
                _coroutineIterators[co] = iterator;
            return co;
        }

        private void StopJsCoroutine(object handle)
        {
            if (handle is Coroutine co && co != null)
            {
                CloseIteratorFor(co);
                StopCoroutine(co);
                _jsCoroutines.Remove(co);
                _invokeCoroutines.Remove(co);
            }
        }

        private void CancelAllInvokes()
        {
            foreach (var co in _invokeCoroutines)
            {
                if (co != null) StopCoroutine(co);
            }
            _invokeCoroutines.Clear();
        }

        private void StopAllGeneratorCoroutines()
        {
            foreach (var co in _jsCoroutines)
            {
                if (co == null) continue;
                CloseIteratorFor(co);
                StopCoroutine(co);
            }
            _jsCoroutines.Clear();
            _coroutineIterators.Clear();
        }

        private void StopAllTrackedCoroutines()
        {
            CancelAllInvokes();
            StopAllGeneratorCoroutines();
            StopAllCoroutines();
        }

        private void CloseIteratorFor(Coroutine co)
        {
            if (!_coroutineIterators.TryGetValue(co, out var iterator) || iterator == null)
                return;
            _coroutineIterators.Remove(co);
            CloseIterator(iterator);
        }

        private static void CloseIterator(ObjectInstance iterator)
        {
            try
            {
                var ret = iterator.Get("return");
                if (ret.IsObject() && ret.AsObject() is Function retFn)
                    retFn.Call(iterator, NoArgs);
            }
            catch
            {
                // best-effort cleanup for generator finally blocks
            }
        }

        private string FormatLog(string message)
        {
            var className = JsClassName ?? "?";
            var goName = gameObject != null ? gameObject.name : "?";
            var path = script != null ? UnityEngine.Application.isEditor
                ? GetEditorAssetPath()
                : script.name : "?";
            return $"[Feather:{goName}/{className}] {message} ({path})";
        }

        private string GetEditorAssetPath()
        {
#if UNITY_EDITOR
            return script != null ? UnityEditor.AssetDatabase.GetAssetPath(script) : "?";
#else
            return script != null ? script.name : "?";
#endif
        }

        public static BridgeKind KindFromAnalysis(Analysis.Property prop)
        {
            // Kind is authoritative (e.g. tint = Color → FieldKind.Color, no Decorator)
            return prop.Kind switch
            {
                FieldKind.Float => BridgeKind.Float,
                FieldKind.Int => BridgeKind.Int,
                FieldKind.Bool => BridgeKind.Bool,
                FieldKind.String => BridgeKind.String,
                FieldKind.Vector2 => BridgeKind.Vector2,
                FieldKind.Vector3 => BridgeKind.Vector3,
                FieldKind.Vector4 => BridgeKind.Vector4,
                FieldKind.Color => BridgeKind.Color,
                FieldKind.UnityEvent => BridgeKind.UnityEvent,
                FieldKind.UnityObject => BridgeKind.UnityObject,
                _ when prop.HasDecorator =>
                    prop.Decorator == "UnityEvent" ? BridgeKind.UnityEvent : BridgeKind.UnityObject,
                _ => BridgeKind.UnityObject
            };
        }

        public static void ApplyDefaults(BridgeProperties bridge, Analysis.Property prop)
        {
            if (bridge.hasSerializedValue || !prop.HasDefault) return;
            switch (prop.Kind)
            {
                case FieldKind.Float:
                    bridge.floatValue = prop.DefaultFloat;
                    break;
                case FieldKind.Int:
                    bridge.intValue = prop.DefaultInt;
                    break;
                case FieldKind.Bool:
                    bridge.boolValue = prop.DefaultBool;
                    break;
                case FieldKind.String:
                    bridge.stringValue = prop.DefaultString ?? string.Empty;
                    break;
                case FieldKind.Vector2:
                    bridge.vector2Value = new Vector2(prop.DefaultX, prop.DefaultY);
                    break;
                case FieldKind.Vector3:
                    bridge.vector3Value = new Vector3(prop.DefaultX, prop.DefaultY, prop.DefaultZ);
                    break;
                case FieldKind.Vector4:
                    bridge.vector4Value = new Vector4(prop.DefaultX, prop.DefaultY, prop.DefaultZ, prop.DefaultW);
                    break;
                case FieldKind.Color:
                    bridge.colorValue = new Color(prop.DefaultX, prop.DefaultY, prop.DefaultZ, prop.DefaultW);
                    break;
            }
        }
    }
}
