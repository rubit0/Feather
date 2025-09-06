using UnityEditor;
using UnityEngine;

namespace Feather.Editor
{
    public static class FeatherMenuItems
    {
        [MenuItem("Component/Feather/JavaScript Behaviour", false, 0)]
        public static void AddJavaScriptBehaviour()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("No GameObject Selected", 
                    "Please select a GameObject in the hierarchy to add a JavaScript component.", "OK");
                return;
            }
            
            Undo.RegisterCreatedObjectUndo(selected, "Add JavaScript Behaviour");
            var scriptBehaviour = selected.AddComponent<JavaScriptBehaviour>();
            
            // Focus the inspector on the new component
            Selection.activeGameObject = selected;
            EditorGUIUtility.PingObject(scriptBehaviour);
            
            // Debug.Log($"Added JavaScript Behaviour to '{selected.name}'. Assign a .js file to get started.");
        }
        
        [MenuItem("Component/Feather/JavaScript Behaviour", true)]
        public static bool ValidateAddJavaScriptBehaviour()
        {
            return Selection.activeGameObject != null;
        }
        
        [MenuItem("GameObject/Feather/Create JavaScript GameObject", false, 0)]
        public static void CreateJavaScriptGameObject()
        {
            var go = new GameObject("JavaScript GameObject");
            go.AddComponent<JavaScriptBehaviour>();
            
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
        
        [MenuItem("Assets/Create/JavaScript Behaviour", false, 80)]
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
        
        private static void CreateJavaScriptFileInSelectedFolder(string defaultName)
        {
            // Get the selected folder path or default to Assets
            string folderPath = GetSelectedFolderPath();
            
            // Generate unique filename
            string fileName = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{defaultName}.js");
            
            // Extract class name from filename
            string className = System.IO.Path.GetFileNameWithoutExtension(fileName);
            
            // Create the file with template
            var template = CreateJavaScriptTemplate(className);
            System.IO.File.WriteAllText(fileName, template);
            
            // Refresh and select the new file
            AssetDatabase.Refresh();
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(fileName);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            
            // Start rename mode like Unity does for C# scripts
            EditorApplication.delayCall += () => {
                if (asset != null)
                {
                    var instanceID = asset.GetInstanceID();
                    EditorGUIUtility.PingObject(instanceID);
                    // This triggers the rename mode in the project window
                    Selection.activeInstanceID = instanceID;
                }
            };
        }
        
        private static string GetSelectedFolderPath()
        {
            // Check if a folder is selected in the project window
            foreach (var obj in Selection.GetFiltered<UnityEngine.Object>(SelectionMode.Assets))
            {
                var path = AssetDatabase.GetAssetPath(obj);
                if (System.IO.Directory.Exists(path))
                {
                    return path;
                }
                else if (System.IO.File.Exists(path))
                {
                    return System.IO.Path.GetDirectoryName(path);
                }
            }
            return "Assets"; // Default to Assets folder if nothing is selected
        }
        
        private static string CreateJavaScriptTemplate(string className)
        {
            return $@"class {className} extends jsBehaviour {{

    // Start is called before the first frame update
    Start() {{
        
    }}

    // Update is called once per frame
    Update() {{
        
    }}
}}";
        }
    }
}
