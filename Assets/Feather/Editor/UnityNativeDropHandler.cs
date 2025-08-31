using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using System.Linq;

namespace Feather.Editor
{
    [InitializeOnLoad]
    public static class UnityNativeDropHandler
    {
        static UnityNativeDropHandler()
        {
            // Hook into hierarchy and scene view for GameObject drag & drop
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }
        
        private static void OnHierarchyGUI(int instanceID, Rect selectionRect)
        {
            // Handle hierarchy drops - drag onto GameObject names in hierarchy
            HandleHierarchyDragDrop(instanceID, selectionRect);
        }
        
        private static void OnSceneGUI(SceneView sceneView)
        {
            // Handle scene view drops - drag onto GameObjects in 3D scene
            HandleSceneViewDragDrop();
        }
        
        private static void HandleHierarchyDragDrop(int instanceID, Rect selectionRect)
        {
            var currentEvent = Event.current;
            if (currentEvent == null) return;
            
            if (currentEvent.type == EventType.DragUpdated || currentEvent.type == EventType.DragPerform)
            {
                if (selectionRect.Contains(currentEvent.mousePosition))
                {
                    var gameObject = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
                    if (gameObject != null)
                    {
                        var jsFiles = DragAndDrop.objectReferences
                            .OfType<TextAsset>()
                            .Where(IsJavaScriptFile)
                            .ToArray();
                            
                        if (jsFiles.Length > 0)
                        {
                            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                            
                            if (currentEvent.type == EventType.DragPerform)
                            {
                                DragAndDrop.AcceptDrag();
                                
                                foreach (var jsFile in jsFiles)
                                {
                                    AddJavaScriptComponent(gameObject, jsFile);
                                }
                                
                                currentEvent.Use();
                            }
                        }
                    }
                }
            }
        }
        
        private static void HandleSceneViewDragDrop()
        {
            var currentEvent = Event.current;
            if (currentEvent == null) return;
            
            if (currentEvent.type == EventType.DragUpdated || currentEvent.type == EventType.DragPerform)
            {
                var jsFiles = DragAndDrop.objectReferences
                    .OfType<TextAsset>()
                    .Where(IsJavaScriptFile)
                    .ToArray();
                    
                if (jsFiles.Length > 0)
                {
                    var hoveredObject = HandleUtility.PickGameObject(currentEvent.mousePosition, false);
                    if (hoveredObject != null)
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                        
                        if (currentEvent.type == EventType.DragPerform)
                        {
                            DragAndDrop.AcceptDrag();
                            
                            foreach (var jsFile in jsFiles)
                            {
                                AddJavaScriptComponent(hoveredObject, jsFile);
                            }
                            
                            currentEvent.Use();
                        }
                    }
                }
            }
        }
        
        private static bool IsJavaScriptFile(TextAsset textAsset)
        {
            if (textAsset == null) return false;
            
            var assetPath = AssetDatabase.GetAssetPath(textAsset);
            return assetPath.EndsWith(".js") || assetPath.EndsWith(".jsu") || assetPath.EndsWith(".jsfeather");
        }
        
        private static void AddJavaScriptComponent(GameObject gameObject, TextAsset jsFile)
        {
            // Check for duplicates
            var existingBehaviours = gameObject.GetComponents<ScriptBehaviour>();
            foreach (var behaviour in existingBehaviours)
            {
                if (behaviour.script == jsFile)
                {
                    // Debug.LogWarning($"GameObject '{gameObject.name}' already has script '{jsFile.name}' attached.");
                    return;
                }
            }
            
            // Add the component
            Undo.RegisterCreatedObjectUndo(gameObject, $"Add JavaScript Component '{jsFile.name}'");
            var scriptBehaviour = gameObject.AddComponent<ScriptBehaviour>();
            
            Undo.RecordObject(scriptBehaviour, "Set JavaScript File");
            scriptBehaviour.script = jsFile;
            
            // Auto-detect properties
            AutoDetectProperties(scriptBehaviour, jsFile);
            
            // Mark as dirty and refresh
            EditorUtility.SetDirty(gameObject);
            EditorUtility.SetDirty(scriptBehaviour);
            
            // Select the GameObject to show the new component
            Selection.activeGameObject = gameObject;
            
            // Debug.Log($"✅ Added JavaScript component '{jsFile.name}' to '{gameObject.name}'");
        }
        
        private static void AutoDetectProperties(ScriptBehaviour scriptBehaviour, TextAsset jsFile)
        {
            try
            {
                var script = Feather.Analysis.Analyzer.ParseScript(jsFile.text);
                if (!Feather.Analysis.Analyzer.IsScriptValid(script))
                {
                    return;
                }
                
                var scriptMeta = Feather.Analysis.Analyzer.AnalyzeScript(script);
                if (!Feather.Analysis.Analyzer.HasJSBehaviour(scriptMeta))
                {
                    return;
                }
                
                // Auto-create property bindings for decorated properties
                var decoratedProperties = scriptMeta.Class.Properties
                    .Where(p => !string.IsNullOrEmpty(p.Decorator))
                    .ToList();
                    
                if (decoratedProperties.Any())
                {
                    var propertyBindings = new System.Collections.Generic.List<ScriptBehaviour.BridgeProperties>();
                    
                    foreach (var prop in decoratedProperties)
                    {
                        propertyBindings.Add(new ScriptBehaviour.BridgeProperties
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
                    
                    scriptBehaviour.properties = propertyBindings.ToArray();
                    // Debug.Log($"Auto-detected {decoratedProperties.Count} property binding(s) for {jsFile.name}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Could not auto-analyze script {jsFile.name}: {ex.Message}");
            }
        }
    }
}
