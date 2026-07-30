using System.Collections.Generic;
using System.Linq;
using Feather.Analysis;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Feather.Editor
{
    [CustomEditor(typeof(JavaScriptBehaviour))]
    public class JavaScriptBehaviourEditor : UnityEditor.Editor
    {
        private ScriptMeta _cachedScriptMeta;
        private string _cachedHash;
        private string _analyzeError;
        private bool _nameMismatch;

        public override void OnInspectorGUI()
        {
            var behaviour = (JavaScriptBehaviour)target;

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Script", behaviour.script, typeof(JavaScript), false);
            }

            if (behaviour.script == null)
            {
                EditorGUILayout.HelpBox("No JavaScript file assigned. Drag a .js file onto this GameObject, or use Component → Feather → Add JavaScript…", MessageType.Info);
                return;
            }

            EnsureCache(behaviour);

            if (!string.IsNullOrEmpty(_analyzeError))
            {
                EditorGUILayout.HelpBox(_analyzeError, MessageType.Error);
                return;
            }

            if (_nameMismatch)
            {
                EditorGUILayout.HelpBox(
                    $"Class name '{_cachedScriptMeta?.Class?.Name}' does not match file name '{behaviour.script.name}'. Prefer matching names (C# convention).",
                    MessageType.Warning);
            }

            if (_cachedScriptMeta != null && behaviour.properties != null)
            {
                DrawNativePropertyFields(behaviour);
            }

            if (GUI.changed)
                EditorUtility.SetDirty(target);
        }

        private void EnsureCache(JavaScriptBehaviour behaviour)
        {
            var hash = $"{behaviour.script.GetEntityId()}:{behaviour.script.text.GetHashCode()}";
            if (_cachedScriptMeta != null && _cachedHash == hash)
                return;

            _cachedHash = hash;
            _analyzeError = null;
            _nameMismatch = false;

            if (!Analyzer.TryAnalyze(behaviour.script.text, out _cachedScriptMeta, out _analyzeError))
            {
                _cachedScriptMeta = null;
                return;
            }

            if (!Analyzer.HasJSBehaviour(_cachedScriptMeta))
            {
                _analyzeError = "Class must extend jsBehaviour";
                return;
            }

            _nameMismatch = !Analyzer.ClassNameMatchesAsset(_cachedScriptMeta, behaviour.script.name);

            // Sync if properties changed
            if (HasPropertiesChanged(_cachedScriptMeta, behaviour))
            {
                Undo.RecordObject(behaviour, "Sync script properties");
                ScriptFieldSync.Sync(behaviour, behaviour.script, applyDefaults: true);
            }
        }

        private static bool HasPropertiesChanged(ScriptMeta meta, JavaScriptBehaviour behaviour)
        {
            var expected = meta.Class.Properties;
            var current = behaviour.properties ?? System.Array.Empty<JavaScriptBehaviour.BridgeProperties>();
            if (expected.Count != current.Length) return true;
            for (var i = 0; i < expected.Count; i++)
            {
                var e = expected[i];
                var c = current.FirstOrDefault(p => p.name == e.Name);
                if (c == null) return true;
                if (c.isList != e.IsArray) return true;
                if (c.kind != JavaScriptBehaviour.KindFromAnalysis(e)) return true;
            }
            return false;
        }

        private void DrawNativePropertyFields(JavaScriptBehaviour behaviour)
        {
            var so = serializedObject;
            so.Update();
            var propertiesProperty = so.FindProperty("properties");
            if (propertiesProperty == null || propertiesProperty.arraySize == 0)
                return;

            for (var i = 0; i < propertiesProperty.arraySize; i++)
            {
                var element = propertiesProperty.GetArrayElementAtIndex(i);
                var name = element.FindPropertyRelative("name").stringValue;
                var kind = (JavaScriptBehaviour.BridgeKind)element.FindPropertyRelative("kind").enumValueIndex;
                var isList = element.FindPropertyRelative("isList").boolValue;
                var meta = GetPropertyMeta(name);
                var display = MakeDisplayName(name);
                var label = new GUIContent(display, meta?.Tooltip);
                var decorator = meta?.Decorator ?? GetDecorator(name);

                if (meta != null)
                {
                    if (meta.HasSpace)
                        EditorGUILayout.Space(meta.SpacePixels);
                    if (!string.IsNullOrEmpty(meta.Header))
                        EditorGUILayout.LabelField(meta.Header, EditorStyles.boldLabel);
                }

                EditorGUI.BeginChangeCheck();

                if (isList)
                {
                    DrawListPropertyField(element, label, decorator, meta);
                }
                else
                {
                    switch (kind)
                    {
                        case JavaScriptBehaviour.BridgeKind.Float:
                            DrawFloatField(element, label, meta);
                            break;
                        case JavaScriptBehaviour.BridgeKind.Int:
                            DrawIntField(element, label, meta);
                            break;
                        case JavaScriptBehaviour.BridgeKind.Bool:
                            element.FindPropertyRelative("boolValue").boolValue =
                                EditorGUILayout.Toggle(label, element.FindPropertyRelative("boolValue").boolValue);
                            break;
                        case JavaScriptBehaviour.BridgeKind.String:
                            DrawStringField(element, label, meta);
                            break;
                        case JavaScriptBehaviour.BridgeKind.Vector2:
                            element.FindPropertyRelative("vector2Value").vector2Value =
                                EditorGUILayout.Vector2Field(label, element.FindPropertyRelative("vector2Value").vector2Value);
                            break;
                        case JavaScriptBehaviour.BridgeKind.Vector3:
                            element.FindPropertyRelative("vector3Value").vector3Value =
                                EditorGUILayout.Vector3Field(label, element.FindPropertyRelative("vector3Value").vector3Value);
                            break;
                        case JavaScriptBehaviour.BridgeKind.Vector4:
                            element.FindPropertyRelative("vector4Value").vector4Value =
                                EditorGUILayout.Vector4Field(label, element.FindPropertyRelative("vector4Value").vector4Value);
                            break;
                        case JavaScriptBehaviour.BridgeKind.Color:
                            DrawColorField(element, label, meta);
                            break;
                        case JavaScriptBehaviour.BridgeKind.UnityEvent:
                            EditorGUILayout.PropertyField(element.FindPropertyRelative("unityEvent"), label);
                            EditorGUILayout.HelpBox(
                                "To call a JS method: add this JavaScriptBehaviour, choose CallJsMethod (string), and pass the method name — or use InvokeJs0–3 mapped to OnJsEvent/OnJsEvent1–3.",
                                MessageType.None);
                            break;
                        default:
                            DrawUnityObjectField(element, label, decorator, meta);
                            break;
                    }
                }

                if (EditorGUI.EndChangeCheck())
                {
                    element.FindPropertyRelative("hasSerializedValue").boolValue = true;
                }

                if (meta is { Required: true } && IsRequiredMissing(element, kind, isList, decorator))
                {
                    EditorGUILayout.HelpBox($"{display} is required.", MessageType.Warning);
                }
            }

            so.ApplyModifiedProperties();
        }

        private void DrawFloatField(SerializedProperty element, GUIContent label, Analysis.Property meta)
        {
            var prop = element.FindPropertyRelative("floatValue");
            var value = prop.floatValue;
            if (meta is { HasRange: true })
                value = EditorGUILayout.Slider(label, value, meta.RangeMin, meta.RangeMax);
            else
                value = EditorGUILayout.FloatField(label, value);
            prop.floatValue = ClampFloat(value, meta);
        }

        private void DrawIntField(SerializedProperty element, GUIContent label, Analysis.Property meta)
        {
            var prop = element.FindPropertyRelative("intValue");
            var value = prop.intValue;
            if (meta is { LayerField: true })
                value = EditorGUILayout.LayerField(label, value);
            else if (meta is { HasRange: true })
                value = EditorGUILayout.IntSlider(label, value, Mathf.RoundToInt(meta.RangeMin), Mathf.RoundToInt(meta.RangeMax));
            else
                value = EditorGUILayout.IntField(label, value);
            prop.intValue = ClampInt(value, meta);
        }

        private void DrawStringField(SerializedProperty element, GUIContent label, Analysis.Property meta)
        {
            var prop = element.FindPropertyRelative("stringValue");
            if (meta is { TagField: true })
            {
                prop.stringValue = EditorGUILayout.TagField(label, prop.stringValue);
                return;
            }

            if (meta is { TextArea: true })
            {
                EditorGUILayout.PrefixLabel(label);
                prop.stringValue = EditorGUILayout.TextArea(prop.stringValue, GUILayout.MinHeight(60));
                return;
            }

            if (meta is { Multiline: true })
            {
                var lines = Mathf.Max(2, meta.MultilineLines);
                EditorGUILayout.PrefixLabel(label);
                prop.stringValue = EditorGUILayout.TextArea(prop.stringValue, GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * lines));
                return;
            }

            prop.stringValue = EditorGUILayout.TextField(label, prop.stringValue);
        }

        private void DrawColorField(SerializedProperty element, GUIContent label, Analysis.Property meta)
        {
            var prop = element.FindPropertyRelative("colorValue");
            if (meta is { HasColorUsage: true })
                prop.colorValue = EditorGUILayout.ColorField(label, prop.colorValue, true, meta.ColorUsageShowAlpha, meta.ColorUsageHdr);
            else
                prop.colorValue = EditorGUILayout.ColorField(label, prop.colorValue);
        }

        private void DrawUnityObjectField(SerializedProperty element, GUIContent label, string decorator, Analysis.Property meta)
        {
            var allowScene = meta == null || !meta.AssetsOnly;
            if (decorator == "GameObject")
            {
                var goProp = element.FindPropertyRelative("gameObject");
                var next = EditorGUILayout.ObjectField(label, goProp.objectReferenceValue, typeof(GameObject), allowScene);
                if (meta is { SceneObjectsOnly: true } && next != null && EditorUtility.IsPersistent(next))
                    next = null;
                goProp.objectReferenceValue = next;
                return;
            }

            if (decorator == "JavaScriptBehaviour" || !string.IsNullOrEmpty(meta?.JsBehaviourClass))
            {
                DrawJsBehaviourField(element, label, meta, allowScene);
                return;
            }

            var expectedType = GetUnityTypeFromDecorator(decorator) ?? typeof(Component);
            if (typeof(Component).IsAssignableFrom(expectedType))
            {
                var compProp = element.FindPropertyRelative("component");
                var current = compProp.objectReferenceValue as Component;
                var next = EditorGUILayout.ObjectField(label, current, expectedType, allowScene) as Component;
                if (meta is { SceneObjectsOnly: true } && next != null && EditorUtility.IsPersistent(next))
                    next = null;
                compProp.objectReferenceValue = next;
            }
            else
            {
                var objProp = element.FindPropertyRelative("gameObject");
                // Non-component assets are project assets; Scene-only does not apply meaningfully
                var assetAllowScene = meta is { SceneObjectsOnly: true };
                var next = EditorGUILayout.ObjectField(label, objProp.objectReferenceValue, expectedType, assetAllowScene);
                if (meta is { AssetsOnly: true } && next != null && !EditorUtility.IsPersistent(next))
                    next = null;
                objProp.objectReferenceValue = next;
            }
        }

        private static void DrawJsBehaviourField(
            SerializedProperty element, GUIContent label, Analysis.Property meta, bool allowScene)
        {
            var classFilter = meta?.JsBehaviourClass;
            var fieldLabel = string.IsNullOrEmpty(classFilter)
                ? label
                : new GUIContent(label.text, string.IsNullOrEmpty(label.tooltip)
                    ? $"JavaScriptBehaviour ({classFilter})"
                    : label.tooltip);

            var compProp = element.FindPropertyRelative("component");
            var current = compProp.objectReferenceValue as JavaScriptBehaviour;
            var next = EditorGUILayout.ObjectField(fieldLabel, current, typeof(JavaScriptBehaviour), allowScene)
                as JavaScriptBehaviour;

            if (meta is { SceneObjectsOnly: true } && next != null && EditorUtility.IsPersistent(next))
                next = null;

            if (next != null && !string.IsNullOrEmpty(classFilter) && !next.MatchesJsClass(classFilter))
            {
                Debug.LogWarning(
                    $"[Feather] '{label.text}' expects JS class '{classFilter}', " +
                    $"got '{next.JsClassName ?? next.name}'. Assignment cleared.");
                next = null;
            }

            compProp.objectReferenceValue = next;
        }

        private void DrawListPropertyField(SerializedProperty propertyElement, GUIContent label, string decoratorType, Analysis.Property meta)
        {
            var expectedType = GetUnityTypeFromDecorator(decoratorType) ?? typeof(UnityEngine.Object);
            var allowScene = meta == null || !meta.AssetsOnly;
            SerializedProperty listProperty;
            if (decoratorType == "GameObject")
                listProperty = propertyElement.FindPropertyRelative("gameObjectList");
            else if (decoratorType == "UnityEvent")
                listProperty = propertyElement.FindPropertyRelative("unityEventList");
            else if (!typeof(Component).IsAssignableFrom(expectedType))
                listProperty = propertyElement.FindPropertyRelative("gameObjectList");
            else
                listProperty = propertyElement.FindPropertyRelative("componentList");

            listProperty.isExpanded = EditorGUILayout.Foldout(listProperty.isExpanded, label, true);
            if (!listProperty.isExpanded) return;

            EditorGUI.indentLevel++;
            var newSize = EditorGUILayout.IntField("Size", listProperty.arraySize);
            if (newSize != listProperty.arraySize)
                listProperty.arraySize = newSize;

            for (var j = 0; j < listProperty.arraySize; j++)
            {
                var element = listProperty.GetArrayElementAtIndex(j);
                if (decoratorType == "UnityEvent")
                {
                    EditorGUILayout.PropertyField(element, new GUIContent($"Element {j}"));
                }
                else
                {
                    var next = EditorGUILayout.ObjectField(
                        $"Element {j}", element.objectReferenceValue, expectedType, allowScene);
                    if (meta is { SceneObjectsOnly: true } && next != null && EditorUtility.IsPersistent(next))
                        next = null;
                    if (meta is { AssetsOnly: true } && next != null && !EditorUtility.IsPersistent(next))
                        next = null;
                    element.objectReferenceValue = next;
                }
            }
            EditorGUI.indentLevel--;
        }

        private static float ClampFloat(float value, Analysis.Property meta)
        {
            if (meta == null) return value;
            if (meta.HasMin) value = Mathf.Max(meta.MinValue, value);
            if (meta.HasMax) value = Mathf.Min(meta.MaxValue, value);
            return value;
        }

        private static int ClampInt(int value, Analysis.Property meta)
        {
            if (meta == null) return value;
            if (meta.HasMin) value = Mathf.Max(Mathf.RoundToInt(meta.MinValue), value);
            if (meta.HasMax) value = Mathf.Min(Mathf.RoundToInt(meta.MaxValue), value);
            return value;
        }

        private static bool IsRequiredMissing(SerializedProperty element, JavaScriptBehaviour.BridgeKind kind, bool isList, string decorator)
        {
            if (isList)
            {
                // Required on lists: at least one assigned element
                var list = decorator == "GameObject" || !IsComponentDecorator(decorator)
                    ? element.FindPropertyRelative("gameObjectList")
                    : element.FindPropertyRelative("componentList");
                if (decorator == "UnityEvent")
                    list = element.FindPropertyRelative("unityEventList");
                if (list == null || list.arraySize == 0) return true;
                for (var i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue != null)
                        return false;
                }
                return true;
            }

            if (kind != JavaScriptBehaviour.BridgeKind.UnityObject)
                return false;

            if (decorator == "GameObject")
                return element.FindPropertyRelative("gameObject").objectReferenceValue == null;

            var expected = GetUnityTypeFromDecorator(decorator);
            if (expected != null && typeof(Component).IsAssignableFrom(expected))
                return element.FindPropertyRelative("component").objectReferenceValue == null;
            return element.FindPropertyRelative("gameObject").objectReferenceValue == null;
        }

        private static bool IsComponentDecorator(string decorator)
        {
            var t = GetUnityTypeFromDecorator(decorator);
            return t != null && typeof(Component).IsAssignableFrom(t);
        }

        private Analysis.Property GetPropertyMeta(string propertyName)
        {
            return _cachedScriptMeta?.Class?.Properties?.FirstOrDefault(p => p.Name == propertyName);
        }

        private string GetDecorator(string propertyName)
        {
            var prop = GetPropertyMeta(propertyName);
            return prop?.Decorator ?? "Component";
        }

        private static string MakeDisplayName(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName)) return propertyName;
            var result = System.Text.RegularExpressions.Regex.Replace(propertyName, @"(\B[A-Z])", " $1");
            return char.ToUpper(result[0]) + result.Substring(1);
        }

        private static System.Type GetUnityTypeFromDecorator(string decorator)
        {
            var builtIn = decorator switch
            {
                "GameObject" => typeof(GameObject),
                "Transform" => typeof(Transform),
                "Rigidbody" => typeof(Rigidbody),
                "Light" => typeof(Light),
                "Camera" => typeof(Camera),
                "AudioSource" => typeof(AudioSource),
                "AudioClip" => typeof(AudioClip),
                "Text" => typeof(Text),
                "Button" => typeof(Button),
                "Image" => typeof(Image),
                "Slider" => typeof(Slider),
                "Toggle" => typeof(Toggle),
                "Renderer" => typeof(Renderer),
                "MeshRenderer" => typeof(MeshRenderer),
                "Collider" => typeof(Collider),
                "BoxCollider" => typeof(BoxCollider),
                "SphereCollider" => typeof(SphereCollider),
                "CapsuleCollider" => typeof(CapsuleCollider),
                "MeshCollider" => typeof(MeshCollider),
                "UnityEvent" => typeof(UnityEvent),
                "JavaScriptBehaviour" => typeof(JavaScriptBehaviour),
                "Texture2D" => typeof(Texture2D),
                "Texture" => typeof(Texture),
                "Material" => typeof(Material),
                "Mesh" => typeof(Mesh),
                "Sprite" => typeof(Sprite),
                _ => null
            };
            if (builtIn != null) return builtIn;

            try
            {
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = assembly.GetType(decorator);
                    if (type != null && typeof(UnityEngine.Object).IsAssignableFrom(type))
                        return type;
                    var types = assembly.GetTypes().Where(t =>
                        t.Name == decorator && typeof(UnityEngine.Object).IsAssignableFrom(t)).ToArray();
                    if (types.Length > 0) return types[0];
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Feather] Could not resolve type '{decorator}': {ex.Message}");
            }

            return typeof(Component);
        }
    }
}
