using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Feather.Editor
{
    public static class TypeScriptDefinitionGenerator
    {
        private const string LegacyApiStampFile = "FeatherApiStamp.txt";
        private const string LegacyProjectDefsStampFile = "FeatherProjectDefsStamp.txt";

        /// <summary>
        /// Full JS project setup: Feather.d.ts, Unity*.d.ts, Project.d.ts, jsconfig, link.xml, settings.
        /// </summary>
        /// <param name="quiet">Less console noise (used for first-install auto setup).</param>
        public static void GenerateOrUpdateJsProject(bool quiet = false)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Feather", "Generating JS project…", 0.1f);
                var projectRoot = ProjectRoot();
                FeatherSettings.GetOrCreateSettings();
                MigrateLegacyStampFiles();

                EditorUtility.DisplayProgressBar("Feather", "Writing Feather.d.ts…", 0.2f);
                WriteFeatherDefinitions(projectRoot);

                EditorUtility.DisplayProgressBar("Feather", "Writing Unity API definitions…", 0.4f);
                WriteUnityDefinitions(projectRoot);

                EditorUtility.DisplayProgressBar("Feather", "Writing Project.d.ts…", 0.7f);
                WriteProjectDefinitions(projectRoot);

                EditorUtility.DisplayProgressBar("Feather", "Writing jsconfig + link.xml…", 0.85f);
                WriteJSConfig(projectRoot);
                WriteApiStamp();
                LinkXmlGenerator.Generate();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                if (quiet)
                    Debug.Log("[Feather] JS project ready (defs + jsconfig + link.xml).");
                else
                    Debug.Log("[Feather] JS project generated/updated (defs + jsconfig + link.xml + settings).");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Feather] Failed to generate/update JS project: {ex}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>True when stamp, jsconfig, and core definition files look present and up to date.</summary>
        public static bool JsProjectIsCurrent()
        {
            var root = ProjectRoot();
            if (!StampMatches()) return false;
            if (!File.Exists(Path.Combine(root, "jsconfig.json"))) return false;
            if (!File.Exists(Path.Combine(root, "Feather.d.ts"))) return false;
            if (!File.Exists(Path.Combine(root, "Unity.d.ts"))) return false;
            if (!File.Exists(Path.Combine(root, "Project.d.ts"))) return false;
            return true;
        }

        public static bool StampMatches()
        {
            MigrateLegacyStampFiles();
            var settings = FeatherSettings.Instance ?? FeatherSettings.GetOrCreateSettings();
            if (settings == null) return false;
            return string.Equals(
                (settings.jsApiStamp ?? string.Empty).Trim(),
                UnityApiSurface.GetStamp(),
                StringComparison.Ordinal);
        }

        private static string ProjectRoot() => Directory.GetParent(Application.dataPath).FullName;

        public static string ProjectDefinitionsPath => Path.Combine(ProjectRoot(), "Project.d.ts");

        public static string GetStoredProjectDefinitionsFingerprint()
        {
            MigrateLegacyStampFiles();
            var settings = FeatherSettings.Instance ?? FeatherSettings.GetOrCreateSettings();
            return settings != null ? (settings.projectDefinitionsStamp ?? string.Empty).Trim() : string.Empty;
        }

        public static void StoreProjectDefinitionsFingerprint(string fingerprint)
        {
            var settings = FeatherSettings.GetOrCreateSettings();
            settings.projectDefinitionsStamp = fingerprint ?? string.Empty;
            EditorUtility.SetDirty(settings);
        }

        private static void WriteApiStamp()
        {
            var settings = FeatherSettings.GetOrCreateSettings();
            settings.jsApiStamp = UnityApiSurface.GetStamp();
            EditorUtility.SetDirty(settings);
        }

        /// <summary>One-time: fold root stamp files into FeatherSettings, then delete them.</summary>
        public static void MigrateLegacyStampFiles()
        {
            var root = ProjectRoot();
            var settings = FeatherSettings.Instance ?? FeatherSettings.GetOrCreateSettings();
            if (settings == null) return;

            var dirty = false;
            var apiPath = Path.Combine(root, LegacyApiStampFile);
            if (File.Exists(apiPath))
            {
                if (string.IsNullOrEmpty(settings.jsApiStamp))
                {
                    settings.jsApiStamp = File.ReadAllText(apiPath).Trim();
                    dirty = true;
                }
                File.Delete(apiPath);
            }

            var projectPath = Path.Combine(root, LegacyProjectDefsStampFile);
            if (File.Exists(projectPath))
            {
                if (string.IsNullOrEmpty(settings.projectDefinitionsStamp))
                {
                    settings.projectDefinitionsStamp = File.ReadAllText(projectPath).Trim();
                    dirty = true;
                }
                File.Delete(projectPath);
            }

            if (dirty)
                EditorUtility.SetDirty(settings);
        }

        private static void WriteJSConfig(string root)
        {
            var json = @"{
  ""compilerOptions"": {
    ""target"": ""ES6"",
    ""lib"": [""ES6""],
    ""allowJs"": true,
    ""checkJs"": false,
    ""noEmit"": true,
    ""skipLibCheck"": true,
    ""moduleResolution"": ""node"",
    ""experimentalDecorators"": true,
    ""noImplicitAny"": false
  },
  ""include"": [
    ""Unity.d.ts"",
    ""Feather.d.ts"",
    ""Project.d.ts"",
    ""Package.*.d.ts"",
    ""**/*.js"",
    ""**/*.jsu"",
    ""**/*.jsfeather""
  ],
  ""exclude"": [
    ""Unity.*.d.ts"",
    ""node_modules"",
    ""Library/**/*"",
    ""Logs/**/*"",
    ""Temp/**/*"",
    ""UserSettings/**/*""
  ]
}
";
            File.WriteAllText(Path.Combine(root, "jsconfig.json"), json);
        }

        private static void WriteFeatherDefinitions(string root)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// Feather-specific TypeScript definitions (curated)");
            sb.AppendLine("// Generated at: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine();
            sb.AppendLine("declare class jsBehaviour {");
            sb.AppendLine("    gameObject: Unity.GameObject;");
            sb.AppendLine("    transform: Unity.Transform;");
            sb.AppendLine("    /** Mirrors MonoBehaviour.enabled on the host. */");
            sb.AppendLine("    enabled: boolean;");
            sb.AppendLine("    invoke(callback: Function, delay?: number): void;");
            sb.AppendLine("    invokeRepeating(callback: Function, delay: number, interval: number): void;");
            sb.AppendLine("    /** Cancel timers started with invoke / invokeRepeating (not startCoroutine). */");
            sb.AppendLine("    cancelInvoke(): void;");
            sb.AppendLine("    /**");
            sb.AppendLine("     * Drive a JS generator/iterator, or a timer callback when intervalSeconds is set.");
            sb.AppendLine("     * Yields: null/undefined → next frame; number → seconds; YieldInstruction → as-is.");
            sb.AppendLine("     */");
            sb.AppendLine("    startCoroutine(generatorOrFn: Function | Iterator<any>, intervalSeconds?: number): any;");
            sb.AppendLine("    stopCoroutine(handle: any): void;");
            sb.AppendLine("    stopAllCoroutines(): void;");
            sb.AppendLine("    /** Yield helper: WaitForSeconds (use inside a generator coroutine). */");
            sb.AppendLine("    wait(seconds: number): any;");
            sb.AppendLine("    /** Yield helper: wait one frame (same as `yield null`). */");
            sb.AppendLine("    nextFrame(): any;");
            sb.AppendLine();
            sb.AppendLine("    Awake?(): void;");
            sb.AppendLine("    Start?(): void;");
            sb.AppendLine("    OnEnable?(): void;");
            sb.AppendLine("    OnDisable?(): void;");
            sb.AppendLine("    Update?(): void;");
            sb.AppendLine("    LateUpdate?(): void;");
            sb.AppendLine("    FixedUpdate?(): void;");
            sb.AppendLine("    OnDestroy?(): void;");
            sb.AppendLine("    OnCollisionEnter?(collision: Unity.Collision): void;");
            sb.AppendLine("    OnCollisionStay?(collision: Unity.Collision): void;");
            sb.AppendLine("    OnCollisionExit?(collision: Unity.Collision): void;");
            sb.AppendLine("    OnTriggerEnter?(other: Unity.Collider): void;");
            sb.AppendLine("    OnTriggerStay?(other: Unity.Collider): void;");
            sb.AppendLine("    OnTriggerExit?(other: Unity.Collider): void;");
            sb.AppendLine("    OnCollisionEnter2D?(collision: Unity.Collision2D): void;");
            sb.AppendLine("    OnCollisionStay2D?(collision: Unity.Collision2D): void;");
            sb.AppendLine("    OnCollisionExit2D?(collision: Unity.Collision2D): void;");
            sb.AppendLine("    OnTriggerEnter2D?(other: Unity.Collider2D): void;");
            sb.AppendLine("    OnTriggerStay2D?(other: Unity.Collider2D): void;");
            sb.AppendLine("    OnTriggerExit2D?(other: Unity.Collider2D): void;");
            sb.AppendLine("    OnBecameVisible?(): void;");
            sb.AppendLine("    OnBecameInvisible?(): void;");
            sb.AppendLine("    OnWillRenderObject?(): void;");
            sb.AppendLine("    OnRenderObject?(): void;");
            sb.AppendLine("    OnApplicationFocus?(hasFocus: boolean): void;");
            sb.AppendLine("    OnApplicationPause?(pauseStatus: boolean): void;");
            sb.AppendLine("    OnApplicationQuit?(): void;");
            sb.AppendLine("    OnGUI?(): void;");
            sb.AppendLine("    OnDrawGizmos?(): void;");
            sb.AppendLine("    OnDrawGizmosSelected?(): void;");
            sb.AppendLine("    OnAnimatorIK?(layerIndex: number): void;");
            sb.AppendLine("    OnAnimatorMove?(): void;");
            sb.AppendLine("    OnJsEvent?(): void;");
            sb.AppendLine("    OnJsEvent1?(): void;");
            sb.AppendLine("    OnJsEvent2?(): void;");
            sb.AppendLine("    OnJsEvent3?(): void;");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("type PropertyDecorator = (target: any, propertyKey: string) => void;");
            sb.AppendLine();
            sb.AppendLine("// Opt-in Inspector visibility (lowercase @public is a JS reserved word — use @Public)");
            sb.AppendLine("declare const Public: PropertyDecorator;");
            sb.AppendLine();
            sb.AppendLine("// Inspector metadata (pair with @Public)");
            sb.AppendLine("declare function Range(min: number, max: number): PropertyDecorator;");
            sb.AppendLine("declare function Tooltip(text: string): PropertyDecorator;");
            sb.AppendLine("declare function Header(text: string): PropertyDecorator;");
            sb.AppendLine("declare function Space(pixels?: number): PropertyDecorator;");
            sb.AppendLine("declare const TextArea: PropertyDecorator;");
            sb.AppendLine("declare function Multiline(lines?: number): PropertyDecorator;");
            sb.AppendLine("declare function Min(value: number): PropertyDecorator;");
            sb.AppendLine("declare function Max(value: number): PropertyDecorator;");
            sb.AppendLine("declare const Required: PropertyDecorator;");
            sb.AppendLine("declare const Scene: PropertyDecorator;");
            sb.AppendLine("declare const Assets: PropertyDecorator;");
            sb.AppendLine("declare const Layer: PropertyDecorator;");
            sb.AppendLine("declare const Tag: PropertyDecorator;");
            sb.AppendLine("declare function ColorUsage(hdr: boolean, showAlpha?: boolean): PropertyDecorator;");
            sb.AppendLine();
            sb.AppendLine("// Value types: `tint = Color` / `offset = Vector3`; also `new Color(1,0,0)`");
            sb.AppendLine("// C# operators are exposed as Color.multiply / Vector3.add / … (JS has no `*` overload)");
            foreach (var name in ValueTypeCtorAliases)
                sb.AppendLine($"declare const {name}: Unity.{name} & typeof Unity.{name};");
            sb.AppendLine();
            sb.AppendLine("// Typed ref markers: `field = MeshRenderer` → IntelliSense; pair with @Public for Inspector");
            sb.AppendLine("// Alias avoids `declare const MeshRenderer: Unity.MeshRenderer` circularity with the class name.");
            foreach (var (name, unityType) in RefMarkers)
            {
                sb.AppendLine($"type __Feather_{name} = {unityType};");
                sb.AppendLine($"declare const {name}: __Feather_{name};");
            }
            sb.AppendLine("declare function List<T>(item: T): T[];");
            sb.AppendLine();
            sb.AppendLine("declare function importNamespace(namespace: string): any;");
            sb.AppendLine("declare function require(path: string): any;");
            sb.AppendLine("declare const Feather: {");
            sb.AppendLine("    require(path: string): any;");
            sb.AppendLine("    /** First matching JS instance. Options: `{ includeInactive?: boolean }`. */");
            sb.AppendLine("    findBehaviour(className: string | Function, options?: { includeInactive?: boolean }): any;");
            sb.AppendLine("    /** All matching JS instances. Options: `{ includeInactive?: boolean }`. */");
            sb.AppendLine("    findBehaviours(className: string | Function, options?: { includeInactive?: boolean }): any[];");
            sb.AppendLine("    /** JS instances in a loaded scene (Scene, or scene name string). */");
            sb.AppendLine("    findBehavioursInScene(scene: any | string, className?: string | Function, options?: { includeInactive?: boolean }): any[];");
            sb.AppendLine("    /** Unity GameObject/Component/JavaScriptBehaviour → JS instance. */");
            sb.AppendLine("    getBehaviour(unityObject: any, className?: string | Function): any;");
            sb.AppendLine("    /** Add a JavaScriptBehaviour host and return its JS instance. */");
            sb.AppendLine("    createBehaviour(gameObject: Unity.GameObject, scriptOrClass: any | string | Function): any;");
            sb.AppendLine("    listScripts(): string[];");
            sb.AppendLine("    getScript(className: string | Function): any;");
            sb.AppendLine("    isScriptLoaded(className: string | Function): boolean;");
            sb.AppendLine("    registerScript(source: string, assetName?: string, replace?: boolean): string | null;");
            sb.AppendLine("    registerScript(script: any, replace?: boolean): string | null;");
            sb.AppendLine("    registerScriptsFromBundle(bundle: any, replace?: boolean): number;");
            sb.AppendLine("    loadBundleFromFile(path: string, replace?: boolean): any;");
            sb.AppendLine("    loadBundleFromMemory(bytes: any, replace?: boolean): any;");
            sb.AppendLine("    /** Downloads text then registerScript. Callback: (className|null, error|null) => void */");
            sb.AppendLine("    downloadAndRegister(url: string, callback?: (className: string | null, error: string | null) => void, replace?: boolean): void;");
            sb.AppendLine("    unloadScript(className: string | Function): boolean;");
            sb.AppendLine("    reloadAll(): void;");
            sb.AppendLine("    onSceneLoaded(callback: (scene: any, mode: any) => void): void;");
            sb.AppendLine("    waitForSeconds(seconds: number): any;");
            sb.AppendLine("    waitForEndOfFrame(): any;");
            sb.AppendLine("    waitUntil(predicate: () => boolean): any;");
            sb.AppendLine("    waitWhile(predicate: () => boolean): any;");
            sb.AppendLine("};");
            sb.AppendLine();
            sb.AppendLine("// Host MonoBehaviour for all JS scripts (not a per-script C# type)");
            sb.AppendLine("declare const JavaScriptBehaviour: typeof Unity.MonoBehaviour & (new (...args: any[]) => Unity.MonoBehaviour);");
            sb.AppendLine();
            File.WriteAllText(Path.Combine(root, "Feather.d.ts"), sb.ToString());
        }

        /// <summary>Global ctor aliases for common Unity structs (<c>new Color(1,0,0)</c>).</summary>
        private static readonly string[] ValueTypeCtorAliases =
        {
            "Color", "Color32", "Vector2", "Vector3", "Vector4", "Quaternion", "Rect",
        };

        /// <summary>Name → TypeScript type in the Unity ambient namespace.</summary>
        private static readonly (string Name, string UnityType)[] RefMarkers =
        {
            ("GameObject", "Unity.GameObject"),
            ("Transform", "Unity.Transform"),
            ("Rigidbody", "Unity.Rigidbody"),
            ("Rigidbody2D", "Unity.Rigidbody2D"),
            ("Light", "Unity.Light"),
            ("Camera", "Unity.Camera"),
            ("AudioSource", "Unity.AudioSource"),
            ("AudioClip", "Unity.AudioClip"),
            ("Text", "Unity.Text"),
            ("Button", "Unity.Button"),
            ("Image", "Unity.Image"),
            ("Slider", "Unity.Slider"),
            ("Toggle", "Unity.Toggle"),
            ("RawImage", "Unity.RawImage"),
            ("Canvas", "Unity.Canvas"),
            ("Animator", "Unity.Animator"),
            ("Collider", "Unity.Collider"),
            ("BoxCollider", "Unity.BoxCollider"),
            ("SphereCollider", "Unity.SphereCollider"),
            ("CapsuleCollider", "Unity.CapsuleCollider"),
            ("MeshCollider", "Unity.MeshCollider"),
            ("Collider2D", "Unity.Collider2D"),
            ("BoxCollider2D", "Unity.BoxCollider2D"),
            ("CircleCollider2D", "Unity.CircleCollider2D"),
            ("PolygonCollider2D", "Unity.PolygonCollider2D"),
            ("Renderer", "Unity.Renderer"),
            ("MeshRenderer", "Unity.MeshRenderer"),
            ("SpriteRenderer", "Unity.SpriteRenderer"),
            ("LineRenderer", "Unity.LineRenderer"),
            ("ParticleSystem", "Unity.ParticleSystem"),
            ("Texture2D", "Unity.Texture2D"),
            ("Texture", "Unity.Texture"),
            ("Material", "Unity.Material"),
            ("Mesh", "Unity.Mesh"),
            ("Sprite", "Unity.Sprite"),
            ("UnityEvent", "Unity.UnityEvent"),
        };

        private static void WriteUnityDefinitions(string root)
        {
            // Remove previous package definition files so disabled packages don't linger.
            foreach (var stale in Directory.GetFiles(root, "Package.*.d.ts"))
            {
                try { File.Delete(stale); }
                catch { /* ignore */ }
            }

            var assemblies = UnityApiSurface.GetAssemblies();
            var unityByAssembly = new Dictionary<string, List<Type>>();
            var packageByJsNs = new Dictionary<string, List<Type>>();

            foreach (var assembly in assemblies)
            {
                var key = SanitizeFilePart(assembly.GetName().Name);
                IEnumerable<Type> types;
                try { types = assembly.GetExportedTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null); }

                foreach (var type in types)
                {
                    if (!UnityApiSurface.ShouldIncludeType(type))
                        continue;

                    if (type.Namespace.StartsWith("UnityEngine", StringComparison.Ordinal))
                    {
                        if (!unityByAssembly.ContainsKey(key))
                            unityByAssembly[key] = new List<Type>();
                        unityByAssembly[key].Add(type);
                    }
                    else if (UnityApiSurface.IsExtraClrAssembly(assembly.GetName().Name))
                    {
                        var jsNs = UnityApiSurface.NamespaceToJsIdentifier(type.Namespace);
                        if (string.IsNullOrEmpty(jsNs)) continue;
                        if (!packageByJsNs.ContainsKey(jsNs))
                            packageByJsNs[jsNs] = new List<Type>();
                        packageByJsNs[jsNs].Add(type);
                    }
                }
            }

            var barrel = new StringBuilder();
            barrel.AppendLine("// Auto-generated Unity TypeScript definitions for Feather");
            barrel.AppendLine("// IntelliSense only — runtime is Jint CLR via UnityApiSurface");
            barrel.AppendLine("// Generated at: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            barrel.AppendLine("// Stamp: " + UnityApiSurface.GetStamp());
            barrel.AppendLine();
            barrel.AppendLine("declare namespace Unity {");

            // First pass: all emitted names (so TsType can emit `any` for missing nested types)
            var emitted = new HashSet<string>();
            var typesToEmit = new List<(string key, Type type)>();
            foreach (var kv in unityByAssembly.OrderBy(k => k.Key))
            {
                foreach (var type in kv.Value.OrderBy(t => t.Name))
                {
                    if (!emitted.Add(type.Name)) continue;
                    typesToEmit.Add((kv.Key, type));
                }
            }

            foreach (var group in typesToEmit.GroupBy(t => t.key))
            {
                var fileName = $"Unity.{group.Key}.d.ts";
                var sb = new StringBuilder();
                sb.AppendLine($"// Auto-generated from assembly group {group.Key}");
                sb.AppendLine("declare namespace Unity {");

                foreach (var (_, type) in group)
                {
                    AppendType(sb, type, "    ", emitted);
                    AppendType(barrel, type, "    ", emitted);
                }

                sb.AppendLine("}");
                File.WriteAllText(Path.Combine(root, fileName), sb.ToString());
            }

            barrel.AppendLine("}");
            barrel.AppendLine();
            // Do NOT emit `declare var Unity: typeof Unity` — it circularly poisons the namespace.
            File.WriteAllText(Path.Combine(root, "Unity.d.ts"), barrel.ToString());

            WritePackageDefinitions(root, packageByJsNs);
        }

        private static void WritePackageDefinitions(string root, Dictionary<string, List<Type>> packageByJsNs)
        {
            foreach (var kv in packageByJsNs.OrderBy(k => k.Key))
            {
                var jsNs = kv.Key;
                var emitted = new HashSet<string>();
                var sb = new StringBuilder();
                sb.AppendLine($"// Auto-generated package types for Feather (global alias: {jsNs})");
                sb.AppendLine($"// CLR: importNamespace — dots become underscores in the JS global name");
                sb.AppendLine($"declare namespace {jsNs} {{");

                foreach (var type in kv.Value.OrderBy(t => t.Name))
                {
                    if (!emitted.Add(type.Name)) continue;
                    AppendType(sb, type, "    ", emitted);
                }

                sb.AppendLine("}");
                sb.AppendLine($"declare const {jsNs}: typeof {jsNs};");
                File.WriteAllText(Path.Combine(root, $"Package.{SanitizeFilePart(jsNs)}.d.ts"), sb.ToString());
            }
        }

        private static void WriteProjectDefinitions(string root)
        {
            var fingerprint = ComputeProjectDefinitionsFingerprint();
            var text = BuildProjectDefinitionsText(fingerprint);
            File.WriteAllText(Path.Combine(root, "Project.d.ts"), text);
            StoreProjectDefinitionsFingerprint(fingerprint);
        }

        /// <summary>
        /// Stable hash of project Component types + public instance methods (for skip-if-unchanged).
        /// Must run on the main thread after assemblies are loaded.
        /// </summary>
        public static string ComputeProjectDefinitionsFingerprint()
        {
            var sb = new StringBuilder(256);
            foreach (var type in UnityApiSurface.GetProjectComponentTypes())
            {
                sb.Append(type.FullName ?? type.Name).Append('\n');
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                             .Where(m => !m.IsSpecialName)
                             .OrderBy(m => m.Name)
                             .Take(40))
                {
                    sb.Append("  ").Append(method.Name).Append('(');
                    sb.Append(string.Join(",", method.GetParameters().Select(p => p.ParameterType.FullName ?? p.ParameterType.Name)));
                    sb.Append(")\n");
                }
            }

            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            var hex = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
                hex.Append(b.ToString("x2"));
            return hex.ToString();
        }

        public static string BuildProjectDefinitionsText(string fingerprint = null)
        {
            fingerprint ??= ComputeProjectDefinitionsFingerprint();

            var sb = new StringBuilder();
            sb.AppendLine("// Auto-generated project Component types for Feather IntelliSense / decorators");
            sb.AppendLine("// Generated at: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("// Fingerprint: " + fingerprint);
            sb.AppendLine();

            foreach (var type in UnityApiSurface.GetProjectComponentTypes())
            {
                // interface + const marker so `field = MyMono` types as the instance;
                // `& (new () => …)` so FindObjectOfType(MyMono) accepts it as a type ctor.
                sb.AppendLine($"interface {type.Name} extends Unity.MonoBehaviour {{");
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                             .Where(m => !m.IsSpecialName)
                             .Take(40))
                {
                    var parameters = string.Join(", ", method.GetParameters().Select(p => $"{SafeName(p.Name)}: any"));
                    sb.AppendLine($"    {method.Name}({parameters}): any;");
                }
                sb.AppendLine("}");
                sb.AppendLine($"declare const {type.Name}: {type.Name} & (new (...args: any[]) => {type.Name}) & PropertyDecorator;");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static void AppendType(StringBuilder sb, Type type, string indent, HashSet<string> emittedUnityNames)
        {
            if (type.IsEnum)
            {
                sb.AppendLine($"{indent}enum {type.Name} {{");
                foreach (var name in Enum.GetNames(type))
                {
                    try
                    {
                        var value = Convert.ToInt64(Enum.Parse(type, name));
                        sb.AppendLine($"{indent}    {name} = {value},");
                    }
                    catch
                    {
                        sb.AppendLine($"{indent}    {name},");
                    }
                }
                sb.AppendLine($"{indent}}}");
                sb.AppendLine();
                return;
            }

            var isStatic = type.IsAbstract && type.IsSealed;
            // Structs (Color, Vector3, …) must be `class` so `new Unity.Color(...)` / statics type-check.
            // Interfaces cannot be constructed and make static members awkward in TS.
            var inherits = "";
            if (!type.IsValueType && type.BaseType != null && type.BaseType != typeof(object) &&
                type.BaseType != typeof(ValueType) && UnityApiSurface.ShouldIncludeType(type.BaseType) &&
                emittedUnityNames.Contains(type.BaseType.Name))
            {
                // Unqualified: we emit inside `declare namespace Unity`, so Object → Unity.Object.
                inherits = $" extends {type.BaseType.Name}";
            }

            // C# static classes → TS class with static members (NOT namespace + static, which is invalid TS)
            if (isStatic)
            {
                sb.AppendLine($"{indent}class {type.Name} {{");
                sb.AppendLine($"{indent}    private constructor();");
            }
            else
            {
                sb.AppendLine($"{indent}class {type.Name}{inherits} {{");
            }

            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (var field in type.GetFields(flags).Where(f => !f.IsSpecialName).Take(80))
            {
                var st = field.IsStatic || isStatic ? "static " : "";
                sb.AppendLine($"{indent}    {st}{field.Name}: {TsType(field.FieldType, emittedUnityNames)};");
            }

            foreach (var prop in type.GetProperties(flags).Where(p => p.GetIndexParameters().Length == 0).Take(80))
            {
                var getter = prop.GetMethod;
                var st = (getter?.IsStatic == true) || isStatic ? "static " : "";
                var ro = !prop.CanWrite ? "readonly " : "";
                sb.AppendLine($"{indent}    {st}{ro}{prop.Name}: {TsType(prop.PropertyType, emittedUnityNames)};");
            }

            foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Take(8))
            {
                if (isStatic) continue;
                var parameters = string.Join(", ", ctor.GetParameters().Select(p => $"{SafeName(p.Name)}?: {TsType(p.ParameterType, emittedUnityNames)}"));
                sb.AppendLine($"{indent}    constructor({parameters});");
            }

            foreach (var method in SelectMethodsForDts(type.GetMethods(flags).Where(m => !m.IsSpecialName)))
            {
                var st = method.IsStatic || isStatic ? "static " : "";
                var parameters = string.Join(", ", method.GetParameters().Select(p => $"{SafeName(p.Name)}?: {TsType(p.ParameterType, emittedUnityNames)}"));
                sb.AppendLine($"{indent}    {st}{method.Name}({parameters}): {TsType(method.ReturnType, emittedUnityNames)};");
            }

            // C# operators → friendly static aliases (JS has no `color * 0.5`).
            // Runtime binds these via __featherWrapMathType → CLR op_*.
            foreach (var method in type.GetMethods(flags)
                         .Where(m => m.IsSpecialName && m.IsStatic && UnityApiSurface.OperatorAliasNames.ContainsKey(m.Name))
                         .OrderBy(m => m.Name)
                         .ThenBy(m => m.GetParameters().Length)
                         .Take(24))
            {
                var alias = UnityApiSurface.OperatorAliasNames[method.Name];
                var parameters = string.Join(", ", method.GetParameters().Select(p => $"{SafeName(p.Name)}?: {TsType(p.ParameterType, emittedUnityNames)}"));
                sb.AppendLine($"{indent}    static {alias}({parameters}): {TsType(method.ReturnType, emittedUnityNames)};");
            }

            sb.AppendLine($"{indent}}}");
            sb.AppendLine();
        }

        /// <summary>
        /// Prefer concrete <c>System.Type</c> overloads (e.g. FindObjectOfType(type)) over parameterless generics.
        /// </summary>
        private static IEnumerable<MethodInfo> SelectMethodsForDts(IEnumerable<MethodInfo> methods)
        {
            foreach (var group in methods.GroupBy(m => m.Name).Take(80))
            {
                var all = group.OrderBy(m => m.GetParameters().Length).ToList();
                var typeOverloads = all
                    .Where(m => !m.IsGenericMethod && !m.ContainsGenericParameters &&
                                m.GetParameters().Any(p => p.ParameterType == typeof(Type)))
                    .Take(4)
                    .ToList();

                if (typeOverloads.Count > 0)
                {
                    foreach (var m in typeOverloads)
                        yield return m;
                    continue;
                }

                yield return all.First();
            }
        }

        /// <summary>Types inside <c>declare namespace Unity</c> — bare names only when emitted; else <c>any</c>.</summary>
        private static string TsType(Type type, HashSet<string> emittedUnityNames)
        {
            if (type == typeof(Type)) return "any"; // pass CLR type ctor: FindObjectOfType(Unity.Light)
            if (type == null || type == typeof(void)) return "void";
            if (type.IsGenericParameter) return "any";
            if (type.IsPointer) return "any";
            if (type == typeof(bool)) return "boolean";
            if (type == typeof(string)) return "string";
            if (type == typeof(char)) return "string";
            if (type.IsPrimitive) return "number";
            if (type == typeof(decimal)) return "number";
            if (type == typeof(object)) return "any";
            if (type.IsArray) return TsType(type.GetElementType(), emittedUnityNames) + "[]";
            if (type.IsByRef) return TsType(type.GetElementType(), emittedUnityNames);
            if (type.IsGenericType) return "any";
            if (type.IsNested) return "any"; // nested types are not emitted as top-level Unity.* names
            if (type.Namespace != null && type.Namespace.StartsWith("UnityEngine"))
                return emittedUnityNames != null && emittedUnityNames.Contains(type.Name) ? type.Name : "any";
            return "any";
        }

        /// <summary>External references (Feather.d.ts) always qualify with <c>Unity.</c>.</summary>
        private static string TsTypeExternal(Type type)
        {
            if (type == null || type == typeof(void)) return "void";
            if (type.IsGenericParameter) return "any";
            if (type == typeof(bool)) return "boolean";
            if (type == typeof(string) || type == typeof(char)) return "string";
            if (type.IsPrimitive || type == typeof(decimal)) return "number";
            if (type == typeof(object)) return "any";
            if (type.IsArray) return TsTypeExternal(type.GetElementType()) + "[]";
            if (type.IsByRef) return TsTypeExternal(type.GetElementType());
            if (type.IsGenericType) return "any";
            if (type.Namespace != null && type.Namespace.StartsWith("UnityEngine"))
                return "Unity." + type.Name;
            return "any";
        }

        private static string SafeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "arg";
            if (name is "function" or "var" or "let" or "const" or "class" or "default" or "enum" or "export" or "import")
                return "_" + name;
            return name.Replace("<", "").Replace(">", "").Replace(",", "");
        }

        private static string SanitizeFilePart(string name)
        {
            return name.Replace("UnityEngine.", "").Replace(".", "_").Replace(" ", "");
        }
    }
}
