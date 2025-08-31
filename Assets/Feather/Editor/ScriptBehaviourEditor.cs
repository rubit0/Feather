using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Linq;
using System.Collections.Generic;
using Feather.Analysis;

namespace Feather.Editor
{
    [CustomEditor(typeof(ScriptBehaviour))]
    public class ScriptBehaviourEditor : UnityEditor.Editor
    {
        private ScriptMeta _cachedScriptMeta;
        
        public override void OnInspectorGUI()
        {
            var scriptBehaviour = (ScriptBehaviour)target;
            
            // Draw script field exactly like MonoBehaviour
            EditorGUI.BeginChangeCheck();
            var newScript = EditorGUILayout.ObjectField("Script", scriptBehaviour.script, typeof(TextAsset), false) as TextAsset;
            
            if (EditorGUI.EndChangeCheck() && newScript != scriptBehaviour.script)
            {
                // Validate that it's a JavaScript file
                if (newScript != null && !IsJavaScriptFile(newScript))
                {
                    EditorUtility.DisplayDialog("Invalid File Type", 
                        "Please select a JavaScript file (.js, .jsu, or .jsfeather)", "OK");
                    return;
                }
                
                Undo.RecordObject(scriptBehaviour, "Change JavaScript File");
                scriptBehaviour.script = newScript;
                
                if (newScript != null)
                {
                    AutoDetectAndCreateProperties(scriptBehaviour, newScript);
                }
            }
            
            // If no script is assigned, show message and stop
            if (scriptBehaviour.script == null)
            {
                EditorGUILayout.HelpBox("No JavaScript file assigned", MessageType.Warning);
                return;
            }
            
            // Cache script metadata and check for changes
            var scriptName = scriptBehaviour.script.name.Split('.')[0];
            var needsUpdate = false;
            
            if (_cachedScriptMeta == null || _cachedScriptMeta.Class.Name != scriptName)
            {
                CacheScriptMetadata(scriptBehaviour);
                needsUpdate = true;
            }
            else
            {
                // Check if the script file has been modified
                var currentScript = Analyzer.ParseScript(scriptBehaviour.script.text);
                if (Analyzer.IsScriptValid(currentScript))
                {
                    var currentMeta = Analyzer.AnalyzeScript(currentScript);
                    if (HasPropertiesChanged(currentMeta, _cachedScriptMeta))
                    {
                        _cachedScriptMeta = currentMeta;
                        needsUpdate = true;
                    }
                }
            }
            
            // Update properties if needed
            if (needsUpdate && _cachedScriptMeta != null)
            {
                var decoratedProperties = _cachedScriptMeta.Class.Properties
                    .Where(p => !string.IsNullOrEmpty(p.Decorator))
                    .ToList();
                SyncPropertiesWithScript(scriptBehaviour, decoratedProperties);
            }
            
            // Draw property fields exactly like MonoBehaviour
            if (_cachedScriptMeta != null && scriptBehaviour.properties != null)
            {
                DrawNativePropertyFields(scriptBehaviour);
            }
            
            if (GUI.changed)
            {
                EditorUtility.SetDirty(target);
            }
        }
        
        private void CacheScriptMetadata(ScriptBehaviour scriptBehaviour)
        {
            try
            {
                var script = Analyzer.ParseScript(scriptBehaviour.script.text);
                if (Analyzer.IsScriptValid(script))
                {
                    _cachedScriptMeta = Analyzer.AnalyzeScript(script);
                }
            }
            catch
            {
                _cachedScriptMeta = null;
            }
        }
        
        private void DrawNativePropertyFields(ScriptBehaviour scriptBehaviour)
        {
            var serializedObject = new SerializedObject(scriptBehaviour);
            var propertiesProperty = serializedObject.FindProperty("properties");
            
            if (propertiesProperty == null || propertiesProperty.arraySize == 0)
                return;
                
            // Draw each property with correct type
            for (int i = 0; i < propertiesProperty.arraySize; i++)
            {
                var propertyElement = propertiesProperty.GetArrayElementAtIndex(i);
                var nameProperty = propertyElement.FindPropertyRelative("name");
                var gameObjectProperty = propertyElement.FindPropertyRelative("gameObject");
                var componentProperty = propertyElement.FindPropertyRelative("component");
                var unityEventProperty = propertyElement.FindPropertyRelative("unityEvent");
                
                var propertyName = nameProperty.stringValue;
                var decoratorType = GetDecoratorTypeForProperty(propertyName);
                var isList = GetIsListForProperty(propertyName);
                var expectedType = GetUnityTypeFromDecorator(decoratorType);
                
                if (isList)
                {
                    // Show as reorderable list (Unity handles this automatically with List<T>)
                    DrawListPropertyField(propertyElement, propertyName, decoratorType);
                }
                else if (decoratorType == "GameObject")
                {
                    // Show as GameObject field
                    EditorGUILayout.PropertyField(gameObjectProperty, new GUIContent(MakeDisplayName(propertyName)));
                }
                else if (decoratorType == "UnityEvent")
                {
                    // Show as UnityEvent field
                    EditorGUILayout.PropertyField(unityEventProperty, new GUIContent(MakeDisplayName(propertyName)));
                }
                else
                {
                    // Show as specific component type
                    DrawTypedComponentField(componentProperty, propertyName, expectedType);
                }
            }
            
            serializedObject.ApplyModifiedProperties();
        }
        
        private void DrawTypedComponentField(SerializedProperty componentProperty, string propertyName, System.Type expectedType)
        {
            var displayName = MakeDisplayName(propertyName);
            var currentComponent = componentProperty.objectReferenceValue as Component;
            
            EditorGUI.BeginChangeCheck();
            var newComponent = EditorGUILayout.ObjectField(displayName, currentComponent, expectedType, true) as Component;
            
            if (EditorGUI.EndChangeCheck())
            {
                componentProperty.objectReferenceValue = newComponent;
            }
        }
        
        private string MakeDisplayName(string propertyName)
        {
            // Convert camelCase to Title Case
            if (string.IsNullOrEmpty(propertyName))
                return propertyName;
                
            var result = System.Text.RegularExpressions.Regex.Replace(
                propertyName, 
                @"(\B[A-Z])", 
                " $1");
            
            return char.ToUpper(result[0]) + result.Substring(1);
        }
        
        private System.Type GetUnityTypeFromDecorator(string decorator)
        {
            // First check built-in Unity types
            var builtInType = decorator switch
            {
                "GameObject" => typeof(GameObject),
                "Transform" => typeof(Transform),
                "Rigidbody" => typeof(Rigidbody),
                "Light" => typeof(Light),
                "Camera" => typeof(Camera),
                "AudioSource" => typeof(AudioSource),
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
                _ => null
            };
            
            if (builtInType != null)
                return builtInType;
            
            // Try to find custom MonoBehaviour types by name
            try
            {
                // Search through all loaded assemblies for the type
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = assembly.GetType(decorator);
                    if (type != null && typeof(Component).IsAssignableFrom(type))
                    {
                        return type;
                    }
                    
                    // Also try to find by simple name (without namespace)
                    var types = assembly.GetTypes().Where(t => 
                        t.Name == decorator && 
                        typeof(Component).IsAssignableFrom(t)).ToArray();
                    
                    if (types.Length > 0)
                    {
                        return types[0]; // Return first match
                    }
                }
            }
            catch (System.Exception ex)
            {
                // If reflection fails, log warning but continue
                Debug.LogWarning($"Could not resolve custom component type '{decorator}': {ex.Message}");
            }
            
            // Default to generic Component if no specific type found
            return typeof(Component);
        }
        
        private bool IsJavaScriptFile(TextAsset textAsset)
        {
            if (textAsset == null) return false;
            
            var assetPath = AssetDatabase.GetAssetPath(textAsset);
            return assetPath.EndsWith(".js") || assetPath.EndsWith(".jsu") || assetPath.EndsWith(".jsfeather");
        }
        
        private void AutoDetectAndCreateProperties(ScriptBehaviour scriptBehaviour, TextAsset scriptAsset)
        {
            try
            {
                var script = Analyzer.ParseScript(scriptAsset.text);
                if (!Analyzer.IsScriptValid(script))
                {
                    EditorUtility.DisplayDialog("Invalid Script", 
                        "The JavaScript file does not contain a valid class.", "OK");
                    return;
                }
                
                var scriptMeta = Analyzer.AnalyzeScript(script);
                if (!Analyzer.HasJSBehaviour(scriptMeta))
                {
                    EditorUtility.DisplayDialog("Invalid Script", 
                        "The JavaScript class must extend 'jsBehaviour'.", "OK");
                    return;
                }
                
                _cachedScriptMeta = scriptMeta;
                
                // Auto-create property bindings for decorated properties
                var decoratedProperties = scriptMeta.Class.Properties
                    .Where(p => !string.IsNullOrEmpty(p.Decorator))
                    .ToList();
                    
                SyncPropertiesWithScript(scriptBehaviour, decoratedProperties);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Could not auto-analyze script {scriptAsset.name}: {ex.Message}");
            }
        }
        
        private void SyncPropertiesWithScript(ScriptBehaviour scriptBehaviour, System.Collections.Generic.List<Property> decoratedProperties)
        {
            Undo.RecordObject(scriptBehaviour, "Sync script properties");
            
            var existingProperties = scriptBehaviour.properties?.ToList() ?? new System.Collections.Generic.List<ScriptBehaviour.BridgeProperties>();
            var updatedProperties = new System.Collections.Generic.List<ScriptBehaviour.BridgeProperties>();
            
            // Add or update properties from script
            foreach (var prop in decoratedProperties)
            {
                var existing = existingProperties.FirstOrDefault(ep => ep.name == prop.Name);
                if (existing != null)
                {
                    // Update existing property (in case list status changed)
                    existing.isList = prop.IsArray;
                    if (prop.IsArray && existing.gameObjectList == null && existing.componentList == null && existing.unityEventList == null)
                    {
                        existing.gameObjectList = new System.Collections.Generic.List<UnityEngine.Object>();
                        existing.componentList = new System.Collections.Generic.List<Component>();
                        existing.unityEventList = new System.Collections.Generic.List<UnityEvent>();
                    }
                    updatedProperties.Add(existing);
                }
                else
                {
                    // Create new property
                    updatedProperties.Add(new ScriptBehaviour.BridgeProperties
                    {
                        name = prop.Name,
                        isList = prop.IsArray,
                        gameObject = null,
                        component = null,
                        unityEvent = null,
                        gameObjectList = prop.IsArray ? new System.Collections.Generic.List<UnityEngine.Object>() : null,
                        componentList = prop.IsArray ? new System.Collections.Generic.List<Component>() : null,
                        unityEventList = prop.IsArray ? new System.Collections.Generic.List<UnityEvent>() : null
                    });
                }
            }
            
            scriptBehaviour.properties = updatedProperties.ToArray();
            EditorUtility.SetDirty(scriptBehaviour);
        }
        
        private void DrawListPropertyField(SerializedProperty propertyElement, string propertyName, string decoratorType)
        {
            var displayName = MakeDisplayName(propertyName);
            var expectedType = GetUnityTypeFromDecorator(decoratorType);
            
            // Always use the same interface for consistency
            SerializedProperty listProperty;
            if (decoratorType == "GameObject")
            {
                listProperty = propertyElement.FindPropertyRelative("gameObjectList");
            }
            else if (decoratorType == "UnityEvent")
            {
                listProperty = propertyElement.FindPropertyRelative("unityEventList");
            }
            else
            {
                listProperty = propertyElement.FindPropertyRelative("componentList");
            }
            
            // Consistent list drawing for all types
            EditorGUILayout.BeginVertical();
            
            // List header with size control
            EditorGUILayout.BeginHorizontal();
            listProperty.isExpanded = EditorGUILayout.Foldout(listProperty.isExpanded, displayName, true);
            EditorGUILayout.EndHorizontal();
            
            if (listProperty.isExpanded)
            {
                EditorGUI.indentLevel++;
                
                // Size field
                var newSize = EditorGUILayout.IntField("Size", listProperty.arraySize);
                if (newSize != listProperty.arraySize)
                {
                    listProperty.arraySize = newSize;
                }
                
                // Draw each element with proper type validation
                for (int j = 0; j < listProperty.arraySize; j++)
                {
                    var element = listProperty.GetArrayElementAtIndex(j);
                    var currentObject = element.objectReferenceValue;
                    
                    EditorGUI.BeginChangeCheck();
                    var newObject = EditorGUILayout.ObjectField(
                        $"Element {j}", 
                        currentObject, 
                        expectedType, 
                        true);
                    
                    if (EditorGUI.EndChangeCheck())
                    {
                        element.objectReferenceValue = newObject;
                    }
                }
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndVertical();
        }
        

        
        private bool GetIsListForProperty(string propertyName)
        {
            if (_cachedScriptMeta?.Class.Properties != null)
            {
                var prop = _cachedScriptMeta.Class.Properties.FirstOrDefault(p => p.Name == propertyName);
                return prop?.IsArray ?? false;
            }
            return false;
        }
        
        private bool HasPropertiesChanged(ScriptMeta newMeta, ScriptMeta oldMeta)
        {
            if (newMeta.Class.Properties.Count != oldMeta.Class.Properties.Count)
                return true;
                
            for (int i = 0; i < newMeta.Class.Properties.Count; i++)
            {
                var newProp = newMeta.Class.Properties[i];
                var oldProp = oldMeta.Class.Properties.FirstOrDefault(p => p.Name == newProp.Name);
                
                if (oldProp == null || 
                    oldProp.Decorator != newProp.Decorator || 
                    oldProp.IsArray != newProp.IsArray)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        private string GetDecoratorTypeForProperty(string propertyName)
        {
            if (_cachedScriptMeta?.Class.Properties != null)
            {
                var prop = _cachedScriptMeta.Class.Properties.FirstOrDefault(p => p.Name == propertyName);
                return prop?.Decorator ?? "Unknown";
            }
            return "Unknown";
        }
    }
}