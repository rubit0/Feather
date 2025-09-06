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
        private const string OUTPUT_PATH = "";
        private const string UNITY_DEFINITIONS_FILE = "Unity.d.ts";
        private const string FEATHER_DEFINITIONS_FILE = "Feather.d.ts";
        private const string JSCONFIG_FILE = "jsconfig.json";
        
        // Types to exclude from generation (internal Unity types)
        private static readonly HashSet<string> ExcludedTypes = new HashSet<string>
        {
            "Internal", "Editor", "Networking", "Experimental"
        };
        
        [MenuItem("Feather/Setup JS Dev Environment")]
        public static void GenerateDefinitions()
        {
            try
            {
                EnsureDirectoryExists();
                
                var unityDefinitions = GenerateUnityDefinitions();
                var featherDefinitions = GenerateFeatherDefinitions();
                
                WriteDefinitionFile(UNITY_DEFINITIONS_FILE, unityDefinitions);
                WriteDefinitionFile(FEATHER_DEFINITIONS_FILE, featherDefinitions);
                
                var jsConfig = GenerateJSConfig();
                WriteDefinitionFile(JSCONFIG_FILE, jsConfig);
                
                Debug.Log($"JavaScript development environment setup complete! Files generated at project root");
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to setup JavaScript development environment: {ex.Message}");
            }
        }
        
        private static string GenerateUnityDefinitions()
        {
            var sb = new StringBuilder();
            
            // Header
            sb.AppendLine("// Auto-generated Unity TypeScript definitions for Feather");
            sb.AppendLine("// These are for IntelliSense only - actual code runs as ES6 JavaScript in Jint");
            sb.AppendLine("// Generated at: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine();
            
            // Get the exact assemblies that Feather exposes
            var exposedAssemblies = GetExposedAssemblies();
            var processedTypes = new HashSet<Type>();
            
            sb.AppendLine("declare namespace Unity {");
            
            foreach (var assembly in exposedAssemblies)
            {
                var types = GetExportedUnityTypes(assembly);
                foreach (var type in types)
                {
                    if (ShouldIncludeType(type) && !processedTypes.Contains(type))
                    {
                        GenerateTypeDefinition(type, sb, 1, processedTypes);
                        processedTypes.Add(type);
                    }
                }
            }
            
            sb.AppendLine("}");
            
            // Generate global Unity namespace import
            sb.AppendLine();
            sb.AppendLine("// Global Unity namespace (available via importNamespace('UnityEngine'))");
            sb.AppendLine("declare var Unity: typeof Unity;");
            
            return sb.ToString();
        }
        
        private static Assembly[] GetExposedAssemblies()
        {
            // Mirror the exact assemblies that Runtime.cs exposes in cfg.AllowClr()
            return new[]
            {
                typeof(GameObject).Assembly,           // UnityEngine.CoreModule
                typeof(Rigidbody).Assembly,           // UnityEngine.PhysicsModule  
                typeof(AudioListener).Assembly,       // UnityEngine.AudioModule
                typeof(Input).Assembly,               // UnityEngine.InputLegacyModule
                typeof(Canvas).Assembly               // UnityEngine.UIModule
            };
        }
        
        private static IEnumerable<Type> GetExportedUnityTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetExportedTypes()
                    .Where(t => t.IsPublic && t.Namespace?.StartsWith("UnityEngine") == true)
                    .OrderBy(t => t.Name);
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null);
            }
        }
        
        private static bool ShouldIncludeType(Type type)
        {
            if (type == null || !type.IsPublic) return false;
            
            // Exclude internal Unity types
            if (ExcludedTypes.Any(excluded => type.FullName?.Contains(excluded) == true))
                return false;
                
            // Exclude generic type definitions
            if (type.IsGenericTypeDefinition) return false;
            
            // Include commonly used Unity types
            return IsCommonUnityType(type);
        }
        
        private static bool IsCommonUnityType(Type type)
        {
            var commonTypes = new[]
            {
                "GameObject", "Transform", "Component", "MonoBehaviour", "Behaviour",
                "Rigidbody", "Collider", "Renderer", "MeshRenderer", "Light", "Camera",
                "AudioSource", "AudioClip", "Input", "Debug", "Time", "Random",
                "Vector2", "Vector3", "Vector4", "Quaternion", "Color", "Rect",
                "KeyCode", "Space", "ForceMode", "CollisionDetection",
                "Canvas", "Text", "Button", "Image", "Slider", "Toggle"
            };
            
            return commonTypes.Contains(type.Name);
        }
        
        private static void GenerateTypeDefinition(Type type, StringBuilder sb, int indent, HashSet<Type> processedTypes)
        {
            var indentStr = new string(' ', indent * 4);
            
            if (type.IsEnum)
            {
                GenerateEnumDefinition(type, sb, indentStr);
            }
            else if (type.IsClass || type.IsValueType)
            {
                GenerateClassDefinition(type, sb, indentStr);
            }
        }
        
        private static void GenerateEnumDefinition(Type enumType, StringBuilder sb, string indent)
        {
            sb.AppendLine($"{indent}enum {enumType.Name} {{");
            
            var enumValues = Enum.GetValues(enumType);
            var enumNames = Enum.GetNames(enumType);
            
            for (int i = 0; i < enumNames.Length; i++)
            {
                var value = Convert.ToInt32(enumValues.GetValue(i));
                sb.AppendLine($"{indent}    {enumNames[i]} = {value},");
            }
            
            sb.AppendLine($"{indent}}}");
            sb.AppendLine();
        }
        
        private static void GenerateClassDefinition(Type type, StringBuilder sb, string indent)
        {
            var isStatic = type.IsSealed && type.IsAbstract;
            var classKeyword = type.IsValueType ? "interface" : "class";
            
            // Class declaration
            var inheritance = "";
            if (type.BaseType != null && type.BaseType != typeof(object) && 
                type.BaseType != typeof(ValueType) && IsCommonUnityType(type.BaseType))
            {
                inheritance = $" extends {type.BaseType.Name}";
            }
            
            sb.AppendLine($"{indent}{(isStatic ? "namespace" : classKeyword)} {type.Name}{inheritance} {{");
            
            // Generate commonly used properties and methods only
            GenerateCommonMembers(type, sb, indent + "    ", isStatic);
            
            sb.AppendLine($"{indent}}}");
            sb.AppendLine();
        }
        
        private static void GenerateCommonMembers(Type type, StringBuilder sb, string indent, bool isStatic)
        {
            // Get fields (Vector3.x, y, z are fields, not properties!)
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(f => IsCommonMember(f.Name))
                .Take(15);
                
            foreach (var field in fields)
            {
                GenerateFieldDefinition(field, sb, indent, isStatic);
            }
            
            // Only include the most commonly used properties to keep definitions clean
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(p => p.GetIndexParameters().Length == 0 && IsCommonMember(p.Name))
                .Take(15); // Increased limit for math types
                
            foreach (var prop in properties)
            {
                GeneratePropertyDefinition(prop, sb, indent, isStatic);
            }
            
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => !m.IsSpecialName && m.DeclaringType == type && IsCommonMember(m.Name))
                .GroupBy(m => m.Name)
                .Select(g => g.First())
                .Take(15); // Increased limit for math types
                
            foreach (var method in methods)
            {
                GenerateMethodDefinition(method, sb, indent, isStatic);
            }
        }
        
        private static bool IsCommonMember(string memberName)
        {
            var commonMembers = new[]
            {
                // GameObject/Transform
                "name", "tag", "layer", "active", "transform", "gameObject",
                "position", "rotation", "localPosition", "localRotation", "localScale",
                "GetComponent", "AddComponent", "GetComponentInChildren", "GetComponentInParent",
                "SetActive", "CompareTag", "Translate", "Rotate", "LookAt",
                
                // Component/MonoBehaviour
                "enabled", "isActiveAndEnabled",
                
                // Debug
                "Log", "LogWarning", "LogError",
                
                // Input
                "GetKey", "GetKeyDown", "GetKeyUp", "GetButton", "GetButtonDown", "GetButtonUp",
                "GetAxis", "GetAxisRaw", "mousePosition",
                
                // Time
                "time", "deltaTime", "fixedDeltaTime", "timeScale",
                
                // Physics
                "velocity", "angularVelocity", "mass", "useGravity", "isKinematic",
                "AddForce", "AddTorque",
                
                // Rendering
                "material", "materials", "bounds", "enabled",
                
                // UI
                "text", "color", "sprite", "onClick", "onValueChanged", "AddListener",
                
                // Vector3 properties and methods
                "x", "y", "z", "magnitude", "sqrMagnitude", "normalized",
                "Set", "Scale", "Normalize", "Cross", "Dot", "Lerp", "Slerp", "MoveTowards",
                "Distance", "Angle", "Project", "ProjectOnPlane", "Reflect",
                "zero", "one", "up", "down", "left", "right", "forward", "back",
                
                // Vector2 properties and methods  
                "magnitude", "sqrMagnitude", "normalized", "perpendicular",
                "Set", "Scale", "Normalize", "Dot", "Lerp", "MoveTowards", "Distance", "Angle",
                "zero", "one", "up", "down", "left", "right",
                
                // Quaternion properties and methods
                "w", "eulerAngles", "identity",
                "Set", "SetLookRotation", "ToAngleAxis", "FromToRotation", "LookRotation",
                "Lerp", "Slerp", "RotateTowards", "Inverse", "Angle", "Euler", "AngleAxis",
                
                // Color properties and methods
                "r", "g", "b", "a", "grayscale", "gamma", "linear",
                "red", "green", "blue", "white", "black", "yellow", "cyan", "magenta", "gray", "clear",
                "Lerp", "LerpUnclamped", "HSVToRGB", "RGBToHSV",
                
                // Rect properties
                "width", "height", "size", "min", "max", "center",
                "xMin", "xMax", "yMin", "yMax", "Contains", "Overlaps",
                
                // Random methods
                "Range", "value", "insideUnitSphere", "insideUnitCircle", "onUnitSphere", "rotation", "rotationUniform"
            };
            
            return commonMembers.Contains(memberName);
        }
        
        private static void GeneratePropertyDefinition(PropertyInfo prop, StringBuilder sb, string indent, bool isStatic)
        {
            var staticKeyword = isStatic || prop.IsStatic() ? "static " : "";
            var readOnly = !prop.CanWrite ? "readonly " : "";
            var typeName = GetTypeScriptTypeName(prop.PropertyType);
            
            sb.AppendLine($"{indent}{staticKeyword}{readOnly}{prop.Name}: {typeName};");
        }
        
        private static void GenerateFieldDefinition(FieldInfo field, StringBuilder sb, string indent, bool isStatic)
        {
            var staticKeyword = isStatic || field.IsStatic ? "static " : "";
            var readOnly = field.IsInitOnly ? "readonly " : "";
            var typeName = GetTypeScriptTypeName(field.FieldType);
            
            sb.AppendLine($"{indent}{staticKeyword}{readOnly}{field.Name}: {typeName};");
        }
        
        private static void GenerateMethodDefinition(MethodInfo method, StringBuilder sb, string indent, bool isStatic)
        {
            var staticKeyword = isStatic || method.IsStatic ? "static " : "";
            var parameters = string.Join(", ", method.GetParameters()
                .Select(p => $"{p.Name}: {GetTypeScriptTypeName(p.ParameterType)}"));
            var returnType = GetTypeScriptTypeName(method.ReturnType);
            
            sb.AppendLine($"{indent}{staticKeyword}{method.Name}({parameters}): {returnType};");
        }
        
        private static string GetTypeScriptTypeName(Type type)
        {
            if (type == typeof(void)) return "void";
            if (type == typeof(bool)) return "boolean";
            if (type == typeof(string)) return "string";
            if (type == typeof(int) || type == typeof(float) || type == typeof(double) || 
                type == typeof(long) || type == typeof(short) || type == typeof(byte)) return "number";
            
            if (type.IsArray)
            {
                var elementType = GetTypeScriptTypeName(type.GetElementType());
                return $"{elementType}[]";
            }
            
            // Check if it's a Unity type
            if (type.Namespace?.StartsWith("UnityEngine") == true)
            {
                return $"Unity.{type.Name}";
            }
            
            return "any"; // Fallback for complex types
        }
        
        private static string GenerateFeatherDefinitions()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("// Feather-specific TypeScript definitions");
            sb.AppendLine("// These provide IntelliSense for Feather's JavaScript runtime");
            sb.AppendLine("// Generated at: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine();
            
            // jsBehaviour base class
            sb.AppendLine("declare class jsBehaviour {");
            sb.AppendLine("    gameObject: Unity.GameObject;");
            sb.AppendLine("    transform: Unity.Transform;");
            sb.AppendLine();
            sb.AppendLine("    // Unity lifecycle methods (all optional)");
            sb.AppendLine("    Awake?(): void;");
            sb.AppendLine("    Start?(): void;");
            sb.AppendLine("    OnEnable?(): void;");
            sb.AppendLine("    OnDisable?(): void;");
            sb.AppendLine("    Update?(): void;");
            sb.AppendLine("    LateUpdate?(): void;");
            sb.AppendLine("    FixedUpdate?(): void;");
            sb.AppendLine("    OnDestroy?(): void;");
            sb.AppendLine();
            sb.AppendLine("    // Physics 3D methods");
            sb.AppendLine("    OnCollisionEnter?(collision: Unity.Collision): void;");
            sb.AppendLine("    OnCollisionStay?(collision: Unity.Collision): void;");
            sb.AppendLine("    OnCollisionExit?(collision: Unity.Collision): void;");
            sb.AppendLine("    OnTriggerEnter?(other: Unity.Collider): void;");
            sb.AppendLine("    OnTriggerStay?(other: Unity.Collider): void;");
            sb.AppendLine("    OnTriggerExit?(other: Unity.Collider): void;");
            sb.AppendLine();
            sb.AppendLine("    // Physics 2D methods");
            sb.AppendLine("    OnCollisionEnter2D?(collision: Unity.Collision2D): void;");
            sb.AppendLine("    OnCollisionStay2D?(collision: Unity.Collision2D): void;");
            sb.AppendLine("    OnCollisionExit2D?(collision: Unity.Collision2D): void;");
            sb.AppendLine("    OnTriggerEnter2D?(other: Unity.Collider2D): void;");
            sb.AppendLine("    OnTriggerStay2D?(other: Unity.Collider2D): void;");
            sb.AppendLine("    OnTriggerExit2D?(other: Unity.Collider2D): void;");
            sb.AppendLine();
            sb.AppendLine("    // Rendering methods");
            sb.AppendLine("    OnBecameVisible?(): void;");
            sb.AppendLine("    OnBecameInvisible?(): void;");
            sb.AppendLine("    OnWillRenderObject?(): void;");
            sb.AppendLine("    OnRenderObject?(): void;");
            sb.AppendLine();
            sb.AppendLine("    // Application methods");
            sb.AppendLine("    OnApplicationFocus?(hasFocus: boolean): void;");
            sb.AppendLine("    OnApplicationPause?(pauseStatus: boolean): void;");
            sb.AppendLine("    OnApplicationQuit?(): void;");
            sb.AppendLine();
            sb.AppendLine("    // GUI & Gizmo methods");
            sb.AppendLine("    OnGUI?(): void;");
            sb.AppendLine("    OnDrawGizmos?(): void;");
            sb.AppendLine("    OnDrawGizmosSelected?(): void;");
            sb.AppendLine();
            sb.AppendLine("    // Animation methods");
            sb.AppendLine("    OnAnimatorIK?(layerIndex: number): void;");
            sb.AppendLine("    OnAnimatorMove?(): void;");
            sb.AppendLine("}");
            sb.AppendLine();
            
            // Decorator declarations for Feather's property injection system
            sb.AppendLine("// Property decorators for Unity object injection");
            sb.AppendLine("// Usage: @GameObject light; or @Text myText;");
            sb.AppendLine("declare var GameObject: PropertyDecorator;");
            sb.AppendLine("declare var Transform: PropertyDecorator;");
            sb.AppendLine("declare var Rigidbody: PropertyDecorator;");
            sb.AppendLine("declare var Light: PropertyDecorator;");
            sb.AppendLine("declare var Camera: PropertyDecorator;");
            sb.AppendLine("declare var AudioSource: PropertyDecorator;");
            sb.AppendLine("declare var Text: PropertyDecorator;");
            sb.AppendLine("declare var Button: PropertyDecorator;");
            sb.AppendLine("declare var Image: PropertyDecorator;");
            sb.AppendLine("declare var Slider: PropertyDecorator;");
            sb.AppendLine("declare var Toggle: PropertyDecorator;");
            sb.AppendLine();
            
            // Global functions
            sb.AppendLine("// Global Feather functions");
            sb.AppendLine("declare function importNamespace(namespace: string): any;");
            sb.AppendLine();
            
            // Generic property decorator type
            sb.AppendLine("// TypeScript decorator type for property injection");
            sb.AppendLine("type PropertyDecorator = (target: any, propertyKey: string) => void;");
            
            return sb.ToString();
        }
        
        private static string GenerateJSConfig()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("{");
            sb.AppendLine("  \"compilerOptions\": {");
            sb.AppendLine("    \"target\": \"ES6\",");
            sb.AppendLine("    \"lib\": [\"ES6\"],");
            sb.AppendLine("    \"allowJs\": true,");
            sb.AppendLine("    \"checkJs\": false,");
            sb.AppendLine("    \"noEmit\": true,");
            sb.AppendLine("    \"skipLibCheck\": true,");
            sb.AppendLine("    \"moduleResolution\": \"node\",");
            sb.AppendLine("    \"experimentalDecorators\": true,");
            sb.AppendLine("    \"noImplicitAny\": false,");
            sb.AppendLine("    \"noImplicitReturns\": false,");
            sb.AppendLine("    \"noImplicitThis\": false");
            sb.AppendLine("  },");
            sb.AppendLine("  \"include\": [");
            sb.AppendLine("    \"*.d.ts\",");
            sb.AppendLine("    \"*.js\",");
            sb.AppendLine("    \"**/*.js\"");
            sb.AppendLine("  ],");
            sb.AppendLine("  \"exclude\": [");
            sb.AppendLine("    \"node_modules\",");
            sb.AppendLine("    \"Library/**/*\",");
            sb.AppendLine("    \"Logs/**/*\",");
            sb.AppendLine("    \"Temp/**/*\",");
            sb.AppendLine("    \"UserSettings/**/*\"");
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            
            return sb.ToString();
        }
        
        private static void EnsureDirectoryExists()
        {
            // Project root is one level above Assets folder
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var fullPath = Path.Combine(projectRoot, OUTPUT_PATH);
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }
        }
        
        private static void WriteDefinitionFile(string fileName, string content)
        {
            // Project root is one level above Assets folder
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var fullPath = Path.Combine(projectRoot, OUTPUT_PATH, fileName);
            File.WriteAllText(fullPath, content);
        }
    }
    
    // Extension methods for reflection
    public static class ReflectionExtensions
    {
        public static bool IsStatic(this PropertyInfo prop)
        {
            return prop.GetMethod?.IsStatic == true || prop.SetMethod?.IsStatic == true;
        }
    }
}
