using UnityEditor;
using UnityEngine;

namespace Feather.Editor
{
    [InitializeOnLoad]
    public static class TypeScriptDefinitionProcessor
    {
        static TypeScriptDefinitionProcessor()
        {
            // Auto-generate on script reload if definitions don't exist
            EditorApplication.delayCall += () =>
            {
                if (ShouldRegenerateDefinitions())
                {
                    // Debug.Log("Generating TypeScript definitions for Feather...");
                    TypeScriptDefinitionGenerator.GenerateDefinitions();
                }
            };
        }
        
        private static bool ShouldRegenerateDefinitions()
        {
            var unityDefinitionsPath = "Assets/Feather/Editor/Generated/Unity.d.ts";
            var featherDefinitionsPath = "Assets/Feather/Editor/Generated/Feather.d.ts";
            
            // Check if definitions exist
            return !System.IO.File.Exists(unityDefinitionsPath) || 
                   !System.IO.File.Exists(featherDefinitionsPath);
        }
    }
}
