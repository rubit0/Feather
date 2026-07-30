using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Feather.Editor
{
    [InitializeOnLoad]
    public static class JavaScriptDragDropHandler
    {
        static JavaScriptDragDropHandler()
        {
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += OnHierarchyGUI;
            SceneView.duringSceneGui += OnSceneGUI;
            DragAndDrop.AddDropHandlerV2(OnInspectorDrop);
        }

        private static void OnHierarchyGUI(EntityId instanceID, Rect selectionRect)
        {
            HandleHierarchyDragDrop(instanceID, selectionRect);
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            HandleSceneViewDragDrop();
        }

        private static DragAndDropVisualMode OnInspectorDrop(Object[] targets, bool perform)
        {
            var jsFiles = GetDraggedJavaScriptFiles();
            if (jsFiles.Length == 0)
                return DragAndDropVisualMode.None;

            var gameObjects = ResolveDropGameObjects(targets);
            if (gameObjects.Length == 0)
                return DragAndDropVisualMode.None;

            if (!perform)
                return DragAndDropVisualMode.Copy;

            foreach (var go in gameObjects)
            {
                foreach (var jsFile in jsFiles)
                    AddJavaScriptComponent(go, jsFile);
            }

            return DragAndDropVisualMode.Copy;
        }

        private static GameObject[] ResolveDropGameObjects(Object[] targets)
        {
            if (targets != null && targets.Length > 0)
            {
                var fromTargets = targets
                    .Select(t => t as GameObject ?? (t as Component)?.gameObject)
                    .Where(go => go != null)
                    .Distinct()
                    .ToArray();
                if (fromTargets.Length > 0)
                    return fromTargets;
            }

            return Selection.gameObjects ?? System.Array.Empty<GameObject>();
        }

        private static void HandleHierarchyDragDrop(EntityId instanceID, Rect selectionRect)
        {
            var currentEvent = Event.current;
            if (currentEvent == null) return;

            if (currentEvent.type == EventType.DragUpdated || currentEvent.type == EventType.DragPerform)
            {
                if (!selectionRect.Contains(currentEvent.mousePosition)) return;

                var gameObject = EditorUtility.EntityIdToObject(instanceID) as GameObject;
                if (gameObject == null) return;

                var jsFiles = GetDraggedJavaScriptFiles();
                if (jsFiles.Length == 0) return;

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (currentEvent.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var jsFile in jsFiles)
                        AddJavaScriptComponent(gameObject, jsFile);
                    currentEvent.Use();
                }
            }
        }

        private static void HandleSceneViewDragDrop()
        {
            var currentEvent = Event.current;
            if (currentEvent == null) return;

            if (currentEvent.type != EventType.DragUpdated && currentEvent.type != EventType.DragPerform)
                return;

            var jsFiles = GetDraggedJavaScriptFiles();
            if (jsFiles.Length == 0) return;

            var hoveredObject = HandleUtility.PickGameObject(currentEvent.mousePosition, false);
            if (hoveredObject == null) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (currentEvent.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (var jsFile in jsFiles)
                    AddJavaScriptComponent(hoveredObject, jsFile);
                currentEvent.Use();
            }
        }

        private static JavaScript[] GetDraggedJavaScriptFiles()
        {
            return DragAndDrop.objectReferences
                .OfType<JavaScript>()
                .Where(IsJavaScriptFile)
                .ToArray();
        }

        private static bool IsJavaScriptFile(JavaScript asset)
        {
            if (asset == null) return false;
            var assetPath = AssetDatabase.GetAssetPath(asset);
            return assetPath.EndsWith(".js") || assetPath.EndsWith(".jsu") || assetPath.EndsWith(".jsfeather");
        }

        public static void AddJavaScriptComponent(GameObject gameObject, JavaScript jsFile)
        {
            var existingBehaviours = gameObject.GetComponents<JavaScriptBehaviour>();
            foreach (var behaviour in existingBehaviours)
            {
                if (behaviour.script == jsFile)
                    return;
            }

            Undo.RegisterCreatedObjectUndo(gameObject, $"Add JavaScript '{jsFile.name}'");
            var scriptBehaviour = Undo.AddComponent<JavaScriptBehaviour>(gameObject);
            Undo.RecordObject(scriptBehaviour, "Set JavaScript File");
            scriptBehaviour.script = jsFile;
            ScriptFieldSync.Sync(scriptBehaviour, jsFile);

            EditorUtility.SetDirty(gameObject);
            EditorUtility.SetDirty(scriptBehaviour);
            Selection.activeGameObject = gameObject;
            FeatherSettings.LogComponentAdd($"Added '{jsFile.name}' to '{gameObject.name}'");
        }
    }
}
