using UnityEditor;
using UnityEngine;

namespace Feather.Editor
{
    public static class FeatherMenuItems
    {
        [MenuItem("Component/Feather/JavaScript Behaviour", false, 0)]
        public static void AddScriptBehaviour()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("No GameObject Selected", 
                    "Please select a GameObject in the hierarchy to add a JavaScript component.", "OK");
                return;
            }
            
            Undo.RegisterCreatedObjectUndo(selected, "Add JavaScript Behaviour");
            var scriptBehaviour = selected.AddComponent<ScriptBehaviour>();
            
            // Focus the inspector on the new component
            Selection.activeGameObject = selected;
            EditorGUIUtility.PingObject(scriptBehaviour);
            
            // Debug.Log($"Added JavaScript Behaviour to '{selected.name}'. Assign a .js file to get started.");
        }
        
        [MenuItem("Component/Feather/JavaScript Behaviour", true)]
        public static bool ValidateAddScriptBehaviour()
        {
            return Selection.activeGameObject != null;
        }
        
        [MenuItem("GameObject/Feather/Create JavaScript GameObject", false, 0)]
        public static void CreateJavaScriptGameObject()
        {
            var go = new GameObject("JavaScript GameObject");
            go.AddComponent<ScriptBehaviour>();
            
            // Position it in the scene
            if (SceneView.lastActiveSceneView?.camera != null)
            {
                var camera = SceneView.lastActiveSceneView.camera;
                go.transform.position = camera.transform.position + camera.transform.forward * 2f;
            }
            
            Undo.RegisterCreatedObjectUndo(go, "Create JavaScript GameObject");
            Selection.activeGameObject = go;
            
            // Debug.Log("Created new GameObject with JavaScript Behaviour. Assign a .js file to get started.");
        }
        
        [MenuItem("Assets/Create/Feather/JavaScript Behaviour", false, 80)]
        public static void CreateJavaScriptFile()
        {
            var path = EditorUtility.SaveFilePanel("Create JavaScript File", 
                "Assets", "NewBehaviour", "js");
                
            if (!string.IsNullOrEmpty(path))
            {
                // Make path relative to Assets folder
                if (path.StartsWith(Application.dataPath))
                {
                    path = "Assets" + path.Substring(Application.dataPath.Length);
                }
                
                var className = System.IO.Path.GetFileNameWithoutExtension(path);
                var template = CreateJavaScriptTemplate(className);
                
                System.IO.File.WriteAllText(path, template);
                AssetDatabase.Refresh();
                
                // Select and highlight the new file
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
                
                // Debug.Log($"Created JavaScript file: {path}");
            }
        }
        
        private static string CreateJavaScriptTemplate(string className)
        {
            return $@"// Feather JavaScript Behaviour
// Drop this file onto a GameObject or use Component > Feather > JavaScript Behaviour

class {className} extends jsBehaviour {{
    // Unity object properties (these will show in the inspector)
    // @Light
    // myLight;
    
    // @Text
    // statusText;
    
    // @Rigidbody
    // rigidBody;
    
    // Unity lifecycle methods
    Awake() {{
        Unity.Debug.Log('{className}: Awake called');
    }}

    Start() {{
        Unity.Debug.Log('{className}: Start called');
        // Initialize your component here
    }}

    Update() {{
        // Update logic goes here
        // Example: Check for input
        // if (Unity.Input.GetKeyDown(Unity.KeyCode.Space)) {{
        //     Unity.Debug.Log('Space key pressed!');
        // }}
    }}

    OnDestroy() {{
        Unity.Debug.Log('{className}: Destroyed');
    }}
    
    // Custom methods
    // ExampleMethod() {{
    //     Unity.Debug.Log('Custom method called');
    // }}
}}";
        }
    }
}
