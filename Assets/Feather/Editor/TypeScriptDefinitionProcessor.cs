using UnityEditor;
using UnityEngine;

namespace Feather.Editor
{
    [InitializeOnLoad]
    public static class TypeScriptDefinitionProcessor
    {
        static TypeScriptDefinitionProcessor()
        {
            // Auto-setup development environment on script reload if files don't exist
            EditorApplication.delayCall += () =>
            {
                if (ShouldRegenerateDefinitions())
                {
                    // Debug.Log("Setting up Feather JavaScript development environment...");
                    TypeScriptDefinitionGenerator.GenerateDefinitions();
                }
            };
        }
        
        private static bool ShouldRegenerateDefinitions()
        {
            // Project root is one level above Assets folder
            var projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
            var unityDefinitionsPath = System.IO.Path.Combine(projectRoot, "Unity.d.ts");
            var featherDefinitionsPath = System.IO.Path.Combine(projectRoot, "Feather.d.ts");
            var jsconfigPath = System.IO.Path.Combine(projectRoot, "jsconfig.json");
            
            // Check if definitions exist
            return !System.IO.File.Exists(unityDefinitionsPath) || 
                   !System.IO.File.Exists(featherDefinitionsPath) ||
                   !System.IO.File.Exists(jsconfigPath);
        }
    }
}
