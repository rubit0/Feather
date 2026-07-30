using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Esprima;
using Esprima.Ast;
using UnityEngine;

namespace Feather.Analysis
{
    public static class Analyzer
    {
        private static readonly HashSet<string> SkippedFieldNames = new HashSet<string>
        {
            "gameObject", "transform"
        };

        public static Script ParseScript(string program)
        {
            var parser = new JavaScriptParser();
            return parser.ParseScript(program);
        }

        public static bool IsScriptValid(Script script)
        {
            return script.Body.Any(b => b.Type == Nodes.ClassDeclaration);
        }

        public static bool HasJSBehaviour(ScriptMeta scriptMeta)
        {
            return scriptMeta?.Class != null && scriptMeta.Class.ExtendsJsBehaviour;
        }

        public static bool ClassNameMatchesAsset(ScriptMeta scriptMeta, string assetName)
        {
            if (scriptMeta?.Class == null || string.IsNullOrEmpty(assetName))
                return false;
            var fileName = assetName.Contains('.') ? assetName.Split('.')[0] : assetName;
            return string.Equals(scriptMeta.Class.Name, fileName, StringComparison.Ordinal);
        }

        public static ScriptMeta AnalyzeScript(string rawScript)
        {
            var script = ParseScript(rawScript);
            if (!IsScriptValid(script))
            {
                throw new Exception("Can't parse an invalid script");
            }

            return new ScriptMeta
            {
                Imports = GetImportsFromScript(script),
                Class = SelectJsBehaviourClass(script)
            };
        }

        public static ScriptMeta AnalyzeScript(Script script)
        {
            return new ScriptMeta
            {
                Imports = GetImportsFromScript(script),
                Class = SelectJsBehaviourClass(script)
            };
        }

        /// <summary>Prefer the first class that extends jsBehaviour (helpers may appear above it).</summary>
        private static ClassMeta SelectJsBehaviourClass(Script script)
        {
            var classes = GetClasses(script);
            return classes.FirstOrDefault(c => c.ExtendsJsBehaviour) ?? classes.First();
        }

        public static bool TryAnalyze(string rawScript, out ScriptMeta meta, out string error)
        {
            meta = null;
            error = null;
            try
            {
                var script = ParseScript(rawScript);
                if (!IsScriptValid(script))
                {
                    error = "No class declaration found";
                    return false;
                }

                meta = AnalyzeScript(script);
                return true;
            }
            catch (ParserException ex)
            {
                error = FormatParserException(ex);
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static string FormatParserException(ParserException ex)
        {
            if (ex == null) return "Parse error";
            var line = ex.LineNumber > 0 ? ex.LineNumber : 0;
            var col = ex.Column > 0 ? ex.Column : 0;
            if (line > 0)
                return $"line {line}, col {col}: {ex.Description ?? ex.Message}";
            return ex.Description ?? ex.Message;
        }

        private static List<string> GetImportsFromScript(Script script)
        {
            var imports = new List<string>();
            foreach (var bodyNode in script.Body)
            {
                if (bodyNode.Type != Nodes.VariableDeclaration) continue;
                var variables = bodyNode.As<VariableDeclaration>();
                if (variables == null) continue;

                foreach (var variableDeclarator in variables.Declarations)
                {
                    if (variableDeclarator.Init == null) continue;
                    var ident = variableDeclarator.Init.ChildNodes.FirstOrDefault(c => c is Identifier).As<Identifier>();
                    if (!(ident is { Name: "importNamespace" }))
                        continue;

                    var literal = variableDeclarator.Init.ChildNodes.FirstOrDefault(c => c is Literal).As<Literal>();
                    if (literal == null)
                        continue;

                    imports.Add(literal.Raw);
                }
            }

            return imports;
        }

        private static List<ClassMeta> GetClasses(Script script)
        {
            return script.Body
                .Where(b => b.Type == Nodes.ClassDeclaration)
                .Cast<ClassDeclaration>()
                .Select(classDeclaration =>
                    new ClassMeta
                    {
                        Name = classDeclaration.Id.Name,
                        ExtendsJsBehaviour = classDeclaration.SuperClass?.ToString() == "jsBehaviour",
                        Properties = GetClassProperties(classDeclaration.Body),
                        Methods = GetClassMethods(classDeclaration.Body)
                    })
                .ToList();
        }

        private static List<Property> GetClassProperties(ClassBody classBody)
        {
            var propertyDefinitions = classBody.Body
                .Where(cn => cn.Type == Nodes.PropertyDefinition)
                .Cast<PropertyDefinition>()
                .Where(pd => pd.Key is Identifier)
                .Where(pd => !pd.Static)
                .ToList();

            var result = new List<Property>();
            foreach (var classElement in propertyDefinitions)
            {
                var propertyName = ((Identifier)classElement.Key).Name;
                if (SkippedFieldNames.Contains(propertyName))
                    continue;

                // Opt-in: only @Public fields appear in the Inspector
                // (lowercase @public is a JS reserved word and will not parse)
                if (!HasPublicDecorator(classElement))
                    continue;

                var prop = new Property { Name = propertyName };

                if (TryApplyTypeDecorator(classElement, prop))
                {
                    // @MeshRenderer / @List(GameObject) alongside @Public
                }
                else if (TryInferUnityRefField(classElement, prop))
                {
                    // targetMesh = MeshRenderer
                }
                else if (TryInferJsBehaviourField(classElement, prop))
                {
                    // otherScript = scriptTest
                }
                else
                {
                    InferPrimitiveField(classElement, prop);
                    if (prop.Kind == FieldKind.Unknown)
                    {
                        Debug.LogWarning(
                            $"[Feather] @Public '{propertyName}' has no recognizable type or default — skipped. " +
                            "Use a marker (e.g. = MeshRenderer), a JS class (= Coin / List(Coin)), or a literal default.");
                        continue;
                    }
                }

                ApplyInspectorDecorators(classElement, prop);
                result.Add(prop);
            }

            return result;
        }

        private static readonly HashSet<string> InspectorMetaDecoratorNames = new HashSet<string>
        {
            "Public", "Range", "Tooltip", "Header", "Space", "TextArea", "Multiline",
            "Min", "Max", "Required", "Scene", "Assets", "Layer", "Tag", "ColorUsage"
        };

        /// <summary>Unity structs/classes that are not Inspector-bridged — must not become Object fields.</summary>
        private static readonly HashSet<string> NonBridgeValueTypeNames = new HashSet<string>
        {
            "Quaternion", "Rect", "RectInt", "Bounds", "BoundsInt", "Matrix4x4",
            "Ray", "Ray2D", "Plane", "Pose", "AnimationCurve", "Gradient",
            "Hash128", "Vector2Int", "Vector3Int"
        };

        private static readonly HashSet<string> BridgedValueTypeNames = new HashSet<string>
        {
            "Color", "Color32", "Vector2", "Vector3", "Vector4", "LayerMask"
        };

        private static readonly Dictionary<string, Type> UnityObjectTypeCache = new Dictionary<string, Type>();
        private static bool _unityObjectCacheBuilt;

        private static bool HasPublicDecorator(PropertyDefinition definition)
        {
            foreach (var decorator in definition.Decorators)
            {
                if (GetDecoratorCalleeName(decorator.Expression) == "Public")
                    return true;
            }
            return false;
        }

        /// <summary>Optional type decorator used with <c>@Public</c> (e.g. <c>@Public @MeshRenderer</c>).</summary>
        private static bool TryApplyTypeDecorator(PropertyDefinition definition, Property prop)
        {
            foreach (var decorator in definition.Decorators)
            {
                var decoratorText = decorator.Expression?.ToString();
                if (string.IsNullOrEmpty(decoratorText)) continue;

                var calleeName = GetDecoratorCalleeName(decorator.Expression);
                if (calleeName != null && InspectorMetaDecoratorNames.Contains(calleeName))
                    continue;

                var isArray = false;
                var actualDecorator = decoratorText;

                if (decoratorText.StartsWith("List(") && decoratorText.EndsWith(")"))
                {
                    isArray = true;
                    actualDecorator = decoratorText.Substring(5, decoratorText.Length - 6);
                }
                else if (decoratorText.EndsWith("()"))
                {
                    actualDecorator = decoratorText.Substring(0, decoratorText.Length - 2);
                }

                if (string.IsNullOrEmpty(actualDecorator) || InspectorMetaDecoratorNames.Contains(actualDecorator))
                    continue;

                if (ApplyUnityRef(prop, actualDecorator, isArray))
                    return true;
            }

            return false;
        }

        private static void ApplyInspectorDecorators(PropertyDefinition definition, Property prop)
        {
            foreach (var decorator in definition.Decorators)
            {
                var expr = decorator.Expression;
                if (expr == null) continue;

                if (expr is Identifier id)
                {
                    ApplyInspectorMeta(prop, id.Name, null);
                    continue;
                }

                if (expr is CallExpression call && call.Callee is Identifier callee)
                {
                    var args = new List<Expression>();
                    foreach (var arg in call.Arguments)
                        args.Add(arg);
                    ApplyInspectorMeta(prop, callee.Name, args);
                }
            }
        }

        private static void ApplyInspectorMeta(Property prop, string name, List<Expression> args)
        {
            switch (name)
            {
                case "Public":
                    break;
                case "Tooltip":
                    if (TryGetStringArg(args, 0, out var tip))
                        prop.Tooltip = tip;
                    break;
                case "Header":
                    if (TryGetStringArg(args, 0, out var header))
                        prop.Header = header;
                    break;
                case "Space":
                    prop.HasSpace = true;
                    if (TryGetNumberArg(args, 0, out var space))
                        prop.SpacePixels = space;
                    break;
                case "Range":
                    if (TryGetNumberArg(args, 0, out var rmin) && TryGetNumberArg(args, 1, out var rmax))
                    {
                        prop.HasRange = true;
                        prop.RangeMin = rmin;
                        prop.RangeMax = rmax;
                    }
                    break;
                case "Min":
                    if (TryGetNumberArg(args, 0, out var min))
                    {
                        prop.HasMin = true;
                        prop.MinValue = min;
                    }
                    break;
                case "Max":
                    if (TryGetNumberArg(args, 0, out var max))
                    {
                        prop.HasMax = true;
                        prop.MaxValue = max;
                    }
                    break;
                case "TextArea":
                    prop.TextArea = true;
                    break;
                case "Multiline":
                    prop.Multiline = true;
                    if (TryGetNumberArg(args, 0, out var lines) && lines >= 1)
                        prop.MultilineLines = (int)lines;
                    break;
                case "Required":
                    prop.Required = true;
                    break;
                case "Scene":
                    prop.SceneObjectsOnly = true;
                    prop.AssetsOnly = false;
                    break;
                case "Assets":
                    prop.AssetsOnly = true;
                    prop.SceneObjectsOnly = false;
                    break;
                case "Layer":
                    prop.LayerField = true;
                    break;
                case "Tag":
                    prop.TagField = true;
                    break;
                case "ColorUsage":
                    prop.HasColorUsage = true;
                    prop.ColorUsageHdr = TryGetBoolArg(args, 0, out var hdr) && hdr;
                    prop.ColorUsageShowAlpha = !TryGetBoolArg(args, 1, out var alpha) || alpha;
                    break;
            }
        }

        private static string GetDecoratorCalleeName(Expression expr)
        {
            if (expr is Identifier id) return id.Name;
            if (expr is CallExpression call && call.Callee is Identifier callee) return callee.Name;
            return null;
        }

        private static bool TryGetNumberArg(List<Expression> args, int index, out float value)
        {
            value = 0;
            if (args == null || index >= args.Count) return false;
            return TryGetNumber(args[index], out value);
        }

        private static bool TryGetStringArg(List<Expression> args, int index, out string value)
        {
            value = null;
            if (args == null || index >= args.Count) return false;
            return TryGetString(args[index], out value);
        }

        private static bool TryGetBoolArg(List<Expression> args, int index, out bool value)
        {
            value = false;
            if (args == null || index >= args.Count) return false;
            return TryGetBool(args[index], out value);
        }

        private static bool TryGetNumber(Expression expr, out float value)
        {
            value = 0;
            if (expr is Literal lit)
            {
                if (lit.Value is double d) { value = (float)d; return true; }
                if (lit.Value is int i) { value = i; return true; }
                if (lit.Value is long l) { value = l; return true; }
                if (lit.TokenType == TokenType.NumericLiteral && lit.Value != null)
                {
                    value = Convert.ToSingle(lit.Value, CultureInfo.InvariantCulture);
                    return true;
                }
            }
            if (expr is UnaryExpression unary && unary.Operator == UnaryOperator.Minus && TryGetNumber(unary.Argument, out var inner))
            {
                value = -inner;
                return true;
            }
            return false;
        }

        private static bool TryGetString(Expression expr, out string value)
        {
            value = null;
            if (expr is Literal lit && (lit.TokenType == TokenType.StringLiteral || lit.Value is string))
            {
                value = lit.Value as string ?? lit.Raw?.Trim('"') ?? "";
                return true;
            }
            return false;
        }

        private static bool TryGetBool(Expression expr, out bool value)
        {
            value = false;
            if (expr is Literal lit && (lit.TokenType == TokenType.BooleanLiteral || lit.Value is bool))
            {
                value = lit.Value is bool b && b;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Typed initializer markers: <c>targetMesh = MeshRenderer</c>, <c>enemies = List(GameObject)</c>,
        /// or <c>targetMesh = MeshRenderer()</c>. PascalCase identifiers only (avoids <c>x = y</c>).
        /// </summary>
        private static bool TryInferUnityRefField(PropertyDefinition definition, Property prop)
        {
            if (definition.Value == null)
                return false;

            var value = definition.Value;

            if (value is Identifier id && IsTypeMarkerName(id.Name))
                return ApplyUnityRef(prop, id.Name, isArray: false);

            if (value is CallExpression call)
            {
                if (call.Callee is Identifier callee)
                {
                    if (callee.Name == "List" && call.Arguments.Count == 1)
                    {
                        var itemName = GetMarkerName(call.Arguments[0]);
                        if (itemName != null)
                            return ApplyUnityRef(prop, itemName, isArray: true);
                    }

                    if (call.Arguments.Count == 0 && IsTypeMarkerName(callee.Name))
                        return ApplyUnityRef(prop, callee.Name, isArray: false);
                }
            }

            return false;
        }

        private static string GetMarkerName(Expression expr)
        {
            if (expr is Identifier id && IsTypeMarkerName(id.Name))
                return id.Name;
            if (expr is CallExpression call &&
                call.Arguments.Count == 0 &&
                call.Callee is Identifier callee &&
                IsTypeMarkerName(callee.Name))
                return callee.Name;
            return null;
        }

        private static bool IsTypeMarkerName(string name) =>
            !string.IsNullOrEmpty(name) && char.IsUpper(name[0]);

        /// <summary>
        /// Lean JS host refs: <c>other = Coin</c> or <c>others = List(Coin)</c> → Inspector slot(s)
        /// for JavaScriptBehaviour filtered to that class; runtime injects JS instance(s).
        /// </summary>
        private static bool TryInferJsBehaviourField(PropertyDefinition definition, Property prop)
        {
            string className = null;
            var isArray = false;

            if (definition.Value is Identifier id)
            {
                className = id.Name;
            }
            else if (definition.Value is CallExpression call &&
                     call.Callee is Identifier callee &&
                     callee.Name == "List" &&
                     call.Arguments.Count == 1 &&
                     call.Arguments[0] is Identifier itemId)
            {
                className = itemId.Name;
                isArray = true;
            }

            if (string.IsNullOrEmpty(className) || !char.IsLetter(className[0]))
                return false;
            if (InspectorMetaDecoratorNames.Contains(className) || className == "List" || className == "jsBehaviour")
                return false;
            if (BridgedValueTypeNames.Contains(className) || NonBridgeValueTypeNames.Contains(className))
                return false;
            if (IsUnityEngineObjectTypeName(className))
                return false;

            prop.Kind = FieldKind.UnityObject;
            prop.Decorator = "JavaScriptBehaviour";
            prop.JsBehaviourClass = className;
            prop.IsArray = isArray;
            return true;
        }

        /// <returns>False if the name must not become an Inspector field of this shape.</returns>
        private static bool ApplyUnityRef(Property prop, string decorator, bool isArray)
        {
            if (string.IsNullOrEmpty(decorator) || InspectorMetaDecoratorNames.Contains(decorator) || decorator == "List")
                return false;

            if (TryApplyValueTypeMarker(prop, decorator, isArray))
                return true;

            // Bridged value types cannot be List(...) yet; non-bridge structs must not look like Components
            if (BridgedValueTypeNames.Contains(decorator) || NonBridgeValueTypeNames.Contains(decorator))
                return false;

            if (decorator != "UnityEvent" && !IsUnityEngineObjectTypeName(decorator))
                return false;

            prop.Decorator = decorator;
            prop.IsArray = isArray;
            prop.Kind = decorator == "UnityEvent" ? FieldKind.UnityEvent : FieldKind.UnityObject;
            return true;
        }

        /// <summary>
        /// Lean markers like <c>tint = Color</c> / <c>offset = Vector3</c> / <c>mask = LayerMask</c>.
        /// </summary>
        private static bool TryApplyValueTypeMarker(Property prop, string name, bool isArray)
        {
            // Native bridge only stores single value-type slots (no Color[] / Vector3[] lists yet)
            if (isArray)
                return false;

            switch (name)
            {
                case "Color":
                case "Color32":
                    prop.Kind = FieldKind.Color;
                    prop.DefaultX = prop.DefaultY = prop.DefaultZ = prop.DefaultW = 1f;
                    prop.HasDefault = true;
                    prop.Decorator = null;
                    prop.IsArray = false;
                    return true;
                case "Vector2":
                    prop.Kind = FieldKind.Vector2;
                    prop.HasDefault = true;
                    prop.Decorator = null;
                    prop.IsArray = false;
                    return true;
                case "Vector3":
                    prop.Kind = FieldKind.Vector3;
                    prop.HasDefault = true;
                    prop.Decorator = null;
                    prop.IsArray = false;
                    return true;
                case "Vector4":
                    prop.Kind = FieldKind.Vector4;
                    prop.HasDefault = true;
                    prop.Decorator = null;
                    prop.IsArray = false;
                    return true;
                case "LayerMask":
                    // Serialized as int; draw with LayerField
                    prop.Kind = FieldKind.Int;
                    prop.LayerField = true;
                    prop.HasDefault = true;
                    prop.DefaultInt = 0;
                    prop.Decorator = null;
                    prop.IsArray = false;
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsUnityEngineObjectTypeName(string name)
        {
            if (UnityApiSurface.RefMarkerNames.Contains(name))
                return true;

            EnsureUnityObjectTypeCache();
            return UnityObjectTypeCache.ContainsKey(name);
        }

        private static void EnsureUnityObjectTypeCache()
        {
            if (_unityObjectCacheBuilt) return;
            _unityObjectCacheBuilt = true;
            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = assembly.GetTypes(); }
                    catch (ReflectionTypeLoadException ex) { types = ex.Types; }
                    if (types == null) continue;
                    foreach (var type in types)
                    {
                        if (type == null || !type.IsPublic || type.IsGenericTypeDefinition) continue;
                        if (!typeof(UnityEngine.Object).IsAssignableFrom(type)) continue;
                        if (!UnityObjectTypeCache.ContainsKey(type.Name))
                            UnityObjectTypeCache[type.Name] = type;
                    }
                }
            }
            catch
            {
                // Best-effort; RefMarkerNames still cover common cases
            }
        }

        private static void InferPrimitiveField(PropertyDefinition definition, Property prop)
        {
            if (definition.Value == null)
                return;

            var value = definition.Value;

            if (value is Literal literal)
            {
                InferFromLiteral(literal, prop);
                return;
            }

            // UnaryExpression: -5
            if (value is UnaryExpression unary && unary.Argument is Literal unaryLit)
            {
                InferFromLiteral(unaryLit, prop);
                if (prop.HasDefault && unary.Operator == UnaryOperator.Minus)
                {
                    if (prop.Kind == FieldKind.Float) prop.DefaultFloat = -prop.DefaultFloat;
                    if (prop.Kind == FieldKind.Int) prop.DefaultInt = -prop.DefaultInt;
                }
                return;
            }

            // Color.red / Vector3.up / Unity.Color.white
            if (TryGetTypeAndMember(value, out var typeName, out var memberName) &&
                TryInferFromStaticMember(typeName, memberName, prop))
                return;

            // new Unity.Vector3(...) / new Vector3(...)
            if (value is NewExpression newExpr)
                InferFromNewExpression(newExpr, prop);
        }

        private static bool TryGetTypeAndMember(Expression value, out string typeName, out string memberName)
        {
            typeName = null;
            memberName = null;
            if (value is not MemberExpression me || me.Property is not Identifier prop)
                return false;

            memberName = prop.Name;
            if (me.Object is Identifier id)
            {
                typeName = id.Name;
                return true;
            }

            // Unity.Color.red
            if (me.Object is MemberExpression parent && parent.Property is Identifier parentProp)
            {
                typeName = parentProp.Name;
                return true;
            }

            return false;
        }

        private static bool TryInferFromStaticMember(string typeName, string memberName, Property prop)
        {
            if (typeName == "Color" || typeName == "Color32")
            {
                if (TryGetStaticValue(typeof(Color), memberName, out Color c))
                {
                    ApplyColorDefaults(prop, c);
                    return true;
                }

                if (typeName == "Color32" && TryGetStaticValue(typeof(Color32), memberName, out Color32 c32))
                {
                    ApplyColorDefaults(prop, c32);
                    return true;
                }

                return false;
            }

            if (typeName == "Vector2" && TryGetStaticValue(typeof(Vector2), memberName, out Vector2 v2))
            {
                prop.Kind = FieldKind.Vector2;
                prop.DefaultX = v2.x;
                prop.DefaultY = v2.y;
                prop.HasDefault = true;
                return true;
            }

            if (typeName == "Vector3" && TryGetStaticValue(typeof(Vector3), memberName, out Vector3 v3))
            {
                prop.Kind = FieldKind.Vector3;
                prop.DefaultX = v3.x;
                prop.DefaultY = v3.y;
                prop.DefaultZ = v3.z;
                prop.HasDefault = true;
                return true;
            }

            if (typeName == "Vector4" && TryGetStaticValue(typeof(Vector4), memberName, out Vector4 v4))
            {
                prop.Kind = FieldKind.Vector4;
                prop.DefaultX = v4.x;
                prop.DefaultY = v4.y;
                prop.DefaultZ = v4.z;
                prop.DefaultW = v4.w;
                prop.HasDefault = true;
                return true;
            }

            return false;
        }

        private static bool TryGetStaticValue<T>(Type type, string memberName, out T value)
        {
            value = default;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase;
            var propInfo = type.GetProperty(memberName, flags);
            if (propInfo != null && propInfo.PropertyType == typeof(T))
            {
                value = (T)propInfo.GetValue(null);
                return true;
            }
            var fieldInfo = type.GetField(memberName, flags);
            if (fieldInfo != null && fieldInfo.FieldType == typeof(T))
            {
                value = (T)fieldInfo.GetValue(null);
                return true;
            }
            return false;
        }

        private static void ApplyColorDefaults(Property prop, Color c)
        {
            prop.Kind = FieldKind.Color;
            prop.DefaultX = c.r;
            prop.DefaultY = c.g;
            prop.DefaultZ = c.b;
            prop.DefaultW = c.a;
            prop.HasDefault = true;
            prop.Decorator = null;
        }

        private static void InferFromLiteral(Literal literal, Property prop)
        {
            if (literal.TokenType == TokenType.BooleanLiteral || literal.Value is bool b)
            {
                prop.Kind = FieldKind.Bool;
                prop.DefaultBool = literal.Value is bool bb && bb;
                prop.HasDefault = true;
                return;
            }

            if (literal.TokenType == TokenType.StringLiteral || literal.Value is string)
            {
                prop.Kind = FieldKind.String;
                prop.DefaultString = literal.Value?.ToString() ?? string.Empty;
                prop.HasDefault = true;
                return;
            }

            if (literal.TokenType == TokenType.NumericLiteral || literal.Value is double || literal.Value is int || literal.Value is long)
            {
                var num = Convert.ToDouble(literal.Value, CultureInfo.InvariantCulture);
                if (Math.Abs(num - Math.Round(num)) < 0.0000001 && !literal.Raw.Contains(".") && !literal.Raw.Contains("e") && !literal.Raw.Contains("E"))
                {
                    prop.Kind = FieldKind.Int;
                    prop.DefaultInt = (int)Math.Round(num);
                }
                else
                {
                    prop.Kind = FieldKind.Float;
                    prop.DefaultFloat = (float)num;
                }
                prop.HasDefault = true;
            }
        }

        private static void InferFromNewExpression(NewExpression newExpr, Property prop)
        {
            var typeName = GetNewExpressionTypeName(newExpr.Callee);
            if (string.IsNullOrEmpty(typeName))
                return;

            var args = new List<Expression>();
            foreach (var arg in newExpr.Arguments)
                args.Add(arg);

            float GetArg(int i)
            {
                if (i >= args.Count) return 0f;
                return EvaluateNumber(args[i]);
            }

            switch (typeName)
            {
                case "Vector2":
                    prop.Kind = FieldKind.Vector2;
                    prop.DefaultX = GetArg(0);
                    prop.DefaultY = GetArg(1);
                    prop.HasDefault = true;
                    break;
                case "Vector3":
                    prop.Kind = FieldKind.Vector3;
                    prop.DefaultX = GetArg(0);
                    prop.DefaultY = GetArg(1);
                    prop.DefaultZ = GetArg(2);
                    prop.HasDefault = true;
                    break;
                case "Vector4":
                    prop.Kind = FieldKind.Vector4;
                    prop.DefaultX = GetArg(0);
                    prop.DefaultY = GetArg(1);
                    prop.DefaultZ = GetArg(2);
                    prop.DefaultW = GetArg(3);
                    prop.HasDefault = true;
                    break;
                case "Color":
                    ApplyColorDefaults(prop, new Color(GetArg(0), GetArg(1), GetArg(2), args.Count > 3 ? GetArg(3) : 1f));
                    break;
                case "Color32":
                    // Color32 ctor uses 0–255 bytes
                    ApplyColorDefaults(prop, new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(GetArg(0)), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(GetArg(1)), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(GetArg(2)), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(args.Count > 3 ? GetArg(3) : 255f), 0, 255)));
                    break;
            }
        }

        private static string GetNewExpressionTypeName(Expression callee)
        {
            if (callee is Identifier id)
                return id.Name;
            if (callee is StaticMemberExpression sme && sme.Property is Identifier prop)
                return prop.Name;
            if (callee is MemberExpression me && me.Property is Identifier mprop)
                return mprop.Name;
            return callee?.ToString()?.Split('.').LastOrDefault();
        }

        private static float EvaluateNumber(Expression expr)
        {
            if (expr is Literal lit && lit.Value != null)
            {
                try { return Convert.ToSingle(lit.Value, CultureInfo.InvariantCulture); }
                catch { return 0f; }
            }
            if (expr is UnaryExpression unary && unary.Argument is Literal ulit && ulit.Value != null)
            {
                try
                {
                    var v = Convert.ToSingle(ulit.Value, CultureInfo.InvariantCulture);
                    return unary.Operator == UnaryOperator.Minus ? -v : v;
                }
                catch { return 0f; }
            }
            return 0f;
        }

        private static List<string> GetClassMethods(ClassBody classBody)
        {
            return classBody.Body
                .Where(n => n.Type == Nodes.MethodDefinition)
                .Cast<MethodDefinition>()
                .Where(md => md.Key is Identifier)
                .Select(md => ((Identifier)md.Key).Name)
                .ToList();
        }
    }
}
