using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Feather.Editor
{
    [ScriptedImporter(1, new[] { "js", "jsu", "jsfeather" }, overrideExts: new[] { "js" }, importQueueOffset: -1000)]
    public class ImportHandler : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var scriptContent = File.ReadAllText(ctx.assetPath);
            
            // Import as TextAsset - this allows the file to be used with JavaScriptBehaviour
            var textAsset = new TextAsset(scriptContent);
            ctx.AddObjectToAsset("main obj", textAsset);
            ctx.SetMainObject(textAsset);
            
            // Debug.Log($"Imported Feather JavaScript: {ctx.assetPath}");
            
            // Ensure development environment is setup for IntelliSense
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var unityDefinitionsPath = Path.Combine(projectRoot, "Unity.d.ts");
            var featherDefinitionsPath = Path.Combine(projectRoot, "Feather.d.ts");
            var jsconfigPath = Path.Combine(projectRoot, "jsconfig.json");
            
            if (!File.Exists(unityDefinitionsPath) || 
                !File.Exists(featherDefinitionsPath) ||
                !File.Exists(jsconfigPath))
            {
                TypeScriptDefinitionGenerator.GenerateDefinitions();
            }
            
            // Trigger hot reload if Runtime exists and we're in play mode
            if (Application.isPlaying && Runtime.Instance != null)
            {
                // Use EditorApplication.delayCall to ensure the asset is fully imported first
                EditorApplication.delayCall += () => {
                    if (Runtime.Instance != null)
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(ctx.assetPath);
                        if (asset != null)
                        {
                            Runtime.Instance.ReloadScript(asset);
                        }
                    }
                };
            }
        }
    }
}
