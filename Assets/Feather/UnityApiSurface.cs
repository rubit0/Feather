using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jint;
using Jint.Runtime.Interop;
using UnityEngine;
using UnityEngine.Events;

namespace Feather
{
    /// <summary>
    /// Single source of truth for assemblies exposed to Jint and TypeScript definition generation.
    /// Extend GetAssemblies() to add optional packages (Input System, Cinemachine, URP, etc.).
    /// </summary>
    public static class UnityApiSurface
    {
        public const string UnityNamespaceAlias = "Unity";

        public static bool AllowSystemReflection =>
            FeatherSettings.Instance != null && FeatherSettings.Instance.allowSystemReflection;

        /// <summary>Baseline assemblies always exposed to Jint (not user-selectable packages).</summary>
        public static Assembly[] GetCoreAssemblies()
        {
            var list = new List<Assembly>
            {
                typeof(GameObject).Assembly,
                typeof(Rigidbody).Assembly,
                typeof(Collider2D).Assembly,
                typeof(AudioListener).Assembly,
                typeof(Input).Assembly,
                typeof(Canvas).Assembly,
                typeof(Animator).Assembly,
                typeof(ParticleSystem).Assembly,
            };

            TryAddTypeAssembly(list, "UnityEngine.UI.Button, UnityEngine.UI");
            TryAddTypeAssembly(list, "UnityEngine.UI.Text, UnityEngine.UI");
            TryAddAssemblyByName(list, "Assembly-CSharp");
            TryAddTypeAssembly(list, "UnityEngine.AI.NavMeshAgent, UnityEngine.AIModule");
            TryAddTypeAssembly(list, "UnityEngine.Video.VideoPlayer, UnityEngine.VideoModule");
            TryAddTypeAssembly(list, "UnityEngine.AssetBundle, UnityEngine.AssetBundleModule");
            return list.Distinct().ToArray();
        }

        public static Assembly[] GetAssemblies()
        {
            var list = new List<Assembly>(GetCoreAssemblies());

            var extras = FeatherSettings.Instance != null
                ? FeatherSettings.Instance.extraClrAssemblies
                : null;
            if (extras != null)
            {
                foreach (var name in extras)
                    TryAddAssemblyByName(list, name);
            }

            return list.Distinct().ToArray();
        }

        public static bool IsExtraClrAssembly(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName) || FeatherSettings.Instance == null)
                return false;
            var extras = FeatherSettings.Instance.extraClrAssemblies;
            if (extras == null || extras.Length == 0) return false;
            foreach (var name in extras)
            {
                if (string.Equals(name, assemblyName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static void ConfigureOptions(Options cfg)
        {
            cfg.Debugger.Enabled = false;
            cfg.ExperimentalFeatures = ExperimentalFeature.Generators;
            var allowReflection = AllowSystemReflection;
            cfg.Interop.AllowGetType = allowReflection;
            cfg.Interop.AllowSystemReflection = allowReflection;
            // GetComponent/etc. declare Component/Object — wrap by runtime type so Move, etc. resolve
            cfg.Interop.WrapObjectHandler = (engine, target, _) =>
                target == null ? null : new ObjectWrapper(engine, target, target.GetType());
            cfg.AllowClr(GetAssemblies());
        }

        public static Engine CreateEngine()
        {
            var engine = new Engine(ConfigureOptions);
            engine.Execute($"var {UnityNamespaceAlias} = importNamespace('UnityEngine');");
            try
            {
                engine.Execute("var UnityUI = importNamespace('UnityEngine.UI');");
            }
            catch
            {
                // UI assembly may be unavailable
            }

            RegisterExtraPackageNamespaces(engine);

            engine.Execute(@"class jsBehaviour {
                gameObject;
                transform;
                enabled;
                invoke;
                invokeRepeating;
                cancelInvoke;
                startCoroutine;
                stopCoroutine;
                stopAllCoroutines;
                wait;
                nextFrame;
            }");

            RegisterOperatorAliases(engine);
            RegisterValueTypeCtorAliases(engine);
            RegisterInspectorMetaStubs(engine);
            RegisterTypeMarkers(engine);
            return engine;
        }

        /// <summary>
        /// Expose opted-in package root namespaces as JS globals (dots → underscores),
        /// e.g. <c>Unity.AI.Navigation</c> → <c>Unity_AI_Navigation</c>.
        /// </summary>
        private static void RegisterExtraPackageNamespaces(Engine engine)
        {
            foreach (var ns in GetExtraPackageNamespaces())
            {
                if (string.IsNullOrEmpty(ns) || ns == "UnityEngine" || ns.StartsWith("UnityEngine.", StringComparison.Ordinal))
                    continue;

                var ident = NamespaceToJsIdentifier(ns);
                if (string.IsNullOrEmpty(ident) || ident == UnityNamespaceAlias || ident == "UnityUI")
                    continue;

                try
                {
                    engine.Execute($"var {ident} = importNamespace('{ns}');");
                }
                catch
                {
                    // Namespace may be empty or unavailable
                }
            }
        }

        public static IEnumerable<string> GetExtraPackageNamespaces()
        {
            var found = new HashSet<string>(StringComparer.Ordinal);
            foreach (var assembly in GetAssemblies())
            {
                if (!IsExtraClrAssembly(assembly.GetName().Name))
                    continue;

                IEnumerable<Type> types;
                try { types = assembly.GetExportedTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null); }
                catch { continue; }

                foreach (var type in types)
                {
                    if (string.IsNullOrEmpty(type.Namespace)) continue;
                    if (type.Namespace.StartsWith("UnityEngine", StringComparison.Ordinal)) continue;
                    if (type.Namespace.StartsWith("UnityEditor", StringComparison.Ordinal)) continue;
                    found.Add(type.Namespace);
                }
            }
            return found.OrderBy(n => n);
        }

        public static string NamespaceToJsIdentifier(string ns) =>
            string.IsNullOrEmpty(ns) ? ns : ns.Replace('.', '_');

        public static bool ShouldIncludeType(Type type)
        {
            if (type == null || !type.IsPublic) return false;
            if (type.IsGenericTypeDefinition) return false;
            var full = type.FullName ?? type.Name;
            if (full.Contains("UnityEditor")) return false;
            if (full.Contains(".Internal")) return false;
            if (type.Namespace == null) return false;

            if (IsExtraClrAssembly(type.Assembly.GetName().Name))
            {
                // Opted-in packages: any public runtime namespace (UnityEngine.* or package-specific)
                if (type.Namespace.StartsWith("UnityEditor", StringComparison.Ordinal)) return false;
                return true;
            }

            if (!type.Namespace.StartsWith("UnityEngine", StringComparison.Ordinal)) return false;
            return true;
        }

        public static string GetStamp()
        {
            var version = Application.unityVersion;
            var names = string.Join("|", GetAssemblies().Select(a => a.GetName().Name).OrderBy(n => n));
            var extras = FeatherSettings.Instance?.extraClrAssemblies;
            var extraKey = extras == null || extras.Length == 0
                ? ""
                : string.Join(",", extras.OrderBy(n => n));
            return $"{version}:{names.GetHashCode():X8}:reflect={AllowSystemReflection}:pkg={extraKey.GetHashCode():X8}";
        }

        /// <summary>C# operator method → JS-friendly static alias (shared with TypeScript generation).</summary>
        public static readonly Dictionary<string, string> OperatorAliasNames = new Dictionary<string, string>
        {
            ["op_Addition"] = "add",
            ["op_Subtraction"] = "subtract",
            ["op_Multiply"] = "multiply",
            ["op_Division"] = "divide",
            ["op_UnaryNegation"] = "negate",
            ["op_UnaryPlus"] = "plus",
        };

        /// <summary>
        /// Proxy <c>Unity</c> so types with C# operators expose <c>multiply</c>/<c>add</c>/… aliases.
        /// CLR type objects reject added JS properties, so we wrap on access.
        /// </summary>
        private static void RegisterOperatorAliases(Engine engine)
        {
            engine.Execute(@"
function __featherWrapMathType(T) {
    if (!T) return T;
    var wrap = function(a, b, c, d) {
        if (arguments.length >= 4) return new T(a, b, c, d);
        if (arguments.length === 3) return new T(a, b, c);
        if (arguments.length === 2) return new T(a, b);
        if (arguments.length === 1) return new T(a);
        return new T();
    };
    if (typeof T.op_Multiply === 'function')
        wrap.multiply = function(a, b) { return T.op_Multiply(a, b); };
    if (typeof T.op_Addition === 'function')
        wrap.add = function(a, b) { return T.op_Addition(a, b); };
    if (typeof T.op_Subtraction === 'function')
        wrap.subtract = function(a, b) { return T.op_Subtraction(a, b); };
    if (typeof T.op_Division === 'function')
        wrap.divide = function(a, b) { return T.op_Division(a, b); };
    if (typeof T.op_UnaryNegation === 'function')
        wrap.negate = function(a) { return T.op_UnaryNegation(a); };
    if (typeof T.op_UnaryPlus === 'function')
        wrap.plus = function(a) { return T.op_UnaryPlus(a); };
    wrap.__proto__ = T;
    return wrap;
}
var __featherMathCache = Object.create(null);
var __UnityRaw = Unity;
Unity = new Proxy(__UnityRaw, {
    get(target, prop) {
        var key = String(prop);
        var value = target[prop];
        if (!value) return value;
        if (typeof value.op_Multiply === 'function' ||
            typeof value.op_Addition === 'function' ||
            typeof value.op_Subtraction === 'function' ||
            typeof value.op_Division === 'function' ||
            typeof value.op_UnaryNegation === 'function') {
            if (!__featherMathCache[key])
                __featherMathCache[key] = __featherWrapMathType(value);
            return __featherMathCache[key];
        }
        return value;
    }
});
");
        }

        /// <summary>Bind <c>Color</c>/<c>Vector3</c>/… globals (go through the Unity proxy when wrapped).</summary>
        private static void RegisterValueTypeCtorAliases(Engine engine)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var name in ValueTypeCtorAliasNames)
                sb.AppendLine($"var {name} = Unity.{name};");
            engine.Execute(sb.ToString());
        }

        /// <summary>Public UnityEngine types that declare at least one mapped operator (used by defs / diagnostics).</summary>
        public static IEnumerable<string> GetOperatorTypeNames()
        {
            var names = new HashSet<string>();
            foreach (var assembly in GetAssemblies())
            {
                IEnumerable<Type> types;
                try { types = assembly.GetExportedTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null); }

                foreach (var type in types)
                {
                    if (!ShouldIncludeType(type)) continue;
                    if (!TypeHasMappedOperator(type)) continue;
                    names.Add(type.Name);
                }
            }
            return names.OrderBy(n => n);
        }

        public static bool TypeHasMappedOperator(Type type)
        {
            if (type == null) return false;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (var method in type.GetMethods(flags))
            {
                if (method.IsSpecialName && OperatorAliasNames.ContainsKey(method.Name))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Inspector / List stubs only. Type markers are real CLR <see cref="TypeReference"/>s
        /// so <c>FindObjectOfType(MeshRenderer)</c> / <c>FindObjectOfType(MyMono)</c> work.
        /// </summary>
        private static void RegisterInspectorMetaStubs(Engine engine)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("function __featherRef() { return null; }");
            sb.AppendLine("function List(type) { return __featherRef; }");
            sb.AppendLine("function Range(min, max) { return __featherRef; }");
            sb.AppendLine("function Tooltip(text) { return __featherRef; }");
            sb.AppendLine("function Header(text) { return __featherRef; }");
            sb.AppendLine("function Space(pixels) { return __featherRef; }");
            sb.AppendLine("function Multiline(lines) { return __featherRef; }");
            sb.AppendLine("function Min(value) { return __featherRef; }");
            sb.AppendLine("function Max(value) { return __featherRef; }");
            sb.AppendLine("function ColorUsage(hdr, showAlpha) { return __featherRef; }");
            foreach (var name in InspectorMetaStubNames)
                sb.AppendLine($"var {name} = __featherRef;");
            engine.Execute(sb.ToString());
        }

        /// <summary>
        /// Globals for Unity + project Component types (field markers and FindObjectOfType args).
        /// </summary>
        private static void RegisterTypeMarkers(Engine engine)
        {
            foreach (var (name, type) in GetBuiltInRefMarkerTypes())
            {
                if (type != null)
                    engine.SetValue(name, TypeReference.CreateTypeReference(engine, type));
            }

            foreach (var type in GetProjectComponentTypes())
                engine.SetValue(type.Name, TypeReference.CreateTypeReference(engine, type));

            // Host component — FindObjectsByType(JavaScriptBehaviour) then filter, or use Feather.findBehaviour
            engine.SetValue("JavaScriptBehaviour", TypeReference.CreateTypeReference(engine, typeof(JavaScriptBehaviour)));
        }

        private static IEnumerable<(string Name, Type Type)> GetBuiltInRefMarkerTypes()
        {
            yield return ("GameObject", typeof(GameObject));
            yield return ("Transform", typeof(Transform));
            yield return ("Rigidbody", typeof(Rigidbody));
            yield return ("Rigidbody2D", typeof(Rigidbody2D));
            yield return ("Light", typeof(Light));
            yield return ("Camera", typeof(Camera));
            yield return ("AudioSource", typeof(AudioSource));
            yield return ("AudioClip", typeof(AudioClip));
            yield return ("Canvas", typeof(Canvas));
            yield return ("Animator", typeof(Animator));
            yield return ("Collider", typeof(Collider));
            yield return ("BoxCollider", typeof(BoxCollider));
            yield return ("SphereCollider", typeof(SphereCollider));
            yield return ("CapsuleCollider", typeof(CapsuleCollider));
            yield return ("MeshCollider", typeof(MeshCollider));
            yield return ("CharacterController", typeof(CharacterController));
            yield return ("Collider2D", typeof(Collider2D));
            yield return ("BoxCollider2D", typeof(BoxCollider2D));
            yield return ("CircleCollider2D", typeof(CircleCollider2D));
            yield return ("PolygonCollider2D", typeof(PolygonCollider2D));
            yield return ("Renderer", typeof(Renderer));
            yield return ("MeshRenderer", typeof(MeshRenderer));
            yield return ("SpriteRenderer", typeof(SpriteRenderer));
            yield return ("LineRenderer", typeof(LineRenderer));
            yield return ("ParticleSystem", typeof(ParticleSystem));
            yield return ("Texture2D", typeof(Texture2D));
            yield return ("Texture", typeof(Texture));
            yield return ("Material", typeof(Material));
            yield return ("Mesh", typeof(Mesh));
            yield return ("Sprite", typeof(Sprite));
            yield return ("UnityEvent", typeof(UnityEvent));

            yield return ("Text", Type.GetType("UnityEngine.UI.Text, UnityEngine.UI"));
            yield return ("Button", Type.GetType("UnityEngine.UI.Button, UnityEngine.UI"));
            yield return ("Image", Type.GetType("UnityEngine.UI.Image, UnityEngine.UI"));
            yield return ("Slider", Type.GetType("UnityEngine.UI.Slider, UnityEngine.UI"));
            yield return ("Toggle", Type.GetType("UnityEngine.UI.Toggle, UnityEngine.UI"));
            yield return ("RawImage", Type.GetType("UnityEngine.UI.RawImage, UnityEngine.UI"));
        }

        /// <summary>User MonoBehaviours from Assembly-CSharp (same set as Project.d.ts).</summary>
        public static IEnumerable<Type> GetProjectComponentTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(a =>
                {
                    var n = a.GetName().Name;
                    return n == "Assembly-CSharp" || n.StartsWith("Assembly-CSharp");
                })
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null); }
                })
                .Where(t => t.IsPublic && typeof(Component).IsAssignableFrom(t) && t != typeof(Component) && t != typeof(MonoBehaviour))
                .Where(t => t.Namespace == null || !t.Namespace.StartsWith("Feather"))
                .OrderBy(t => t.Name);
        }

        /// <summary>Must stay in sync with Feather.d.ts value-type ctor aliases.</summary>
        public static readonly string[] ValueTypeCtorAliasNames =
        {
            "Color", "Color32", "Vector2", "Vector3", "Vector4", "Quaternion", "Rect",
        };

        /// <summary>Bare inspector meta stubs (<c>@Public</c>, <c>@Required</c>, …). Call-form metas are functions above.</summary>
        public static readonly string[] InspectorMetaStubNames =
        {
            "Public", "TextArea", "Required", "Scene", "Assets", "Layer", "Tag"
        };

        /// <summary>Global marker names registered on the Jint engine (must stay in sync with Feather.d.ts generation).</summary>
        public static readonly string[] RefMarkerNames =
        {
            "GameObject", "Transform", "Rigidbody", "Rigidbody2D", "Light", "Camera", "AudioSource",
            "AudioClip",
            "Text", "Button", "Image", "Slider", "Toggle", "RawImage", "Canvas", "Animator",
            "Collider", "BoxCollider", "SphereCollider", "CapsuleCollider", "MeshCollider",
            "CharacterController",
            "Collider2D", "BoxCollider2D", "CircleCollider2D", "PolygonCollider2D",
            "Renderer", "MeshRenderer", "SpriteRenderer", "LineRenderer", "ParticleSystem",
            "Texture2D", "Texture", "Material", "Mesh", "Sprite", "UnityEvent",
        };

        private static void TryAddTypeAssembly(List<Assembly> list, string typeName)
        {
            try
            {
                var type = Type.GetType(typeName);
                if (type != null)
                    list.Add(type.Assembly);
            }
            catch
            {
                // ignore
            }
        }

        private static void TryAddAssemblyByName(List<Assembly> list, string assemblyName)
        {
            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == assemblyName);
                if (assembly != null)
                    list.Add(assembly);
            }
            catch
            {
                // ignore
            }
        }
    }
}
