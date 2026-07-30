using System.IO;
using Feather.Analysis;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Feather.Editor
{
    [ScriptedImporter(4, new[] { "js", "jsu", "jsfeather" }, overrideExts: new[] { "js" }, importQueueOffset: -1000)]
    public class JavaScriptImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var scriptContent = File.ReadAllText(ctx.assetPath);
            var jsAsset = ScriptableObject.CreateInstance<JavaScript>();

            string className = Path.GetFileNameWithoutExtension(ctx.assetPath);
            var extends = false;
            string importError = null;

            if (!Analyzer.TryAnalyze(scriptContent, out var meta, out var error))
            {
                importError = error;
                Debug.LogWarning($"[Feather] {ctx.assetPath}: {error}");
            }
            else if (!Analyzer.HasJSBehaviour(meta))
            {
                importError = "Class should extend jsBehaviour to be usable as a component.";
                className = meta.Class?.Name ?? className;
                Debug.LogWarning($"[Feather] {ctx.assetPath}: {importError}");
            }
            else
            {
                className = meta.Class.Name;
                extends = true;
                var fileName = Path.GetFileNameWithoutExtension(ctx.assetPath);
                if (!Analyzer.ClassNameMatchesAsset(meta, fileName))
                {
                    Debug.LogWarning(
                        $"[Feather] {ctx.assetPath}: class '{meta.Class.Name}' does not match file name '{fileName}'.");
                }
            }

            jsAsset.SetImportData(scriptContent, className, extends, importError);
            jsAsset.name = Path.GetFileNameWithoutExtension(ctx.assetPath);

            var icon = FeatherEditorIcons.JsScriptIcon;
            ctx.AddObjectToAsset("JavaScript", jsAsset, icon);
            ctx.SetMainObject(jsAsset);

            if (Application.isPlaying && Runtime.Instance != null)
            {
                var path = ctx.assetPath;
                EditorApplication.delayCall += () =>
                {
                    if (Runtime.Instance == null) return;
                    var asset = AssetDatabase.LoadAssetAtPath<JavaScript>(path);
                    if (asset != null)
                        Runtime.Instance.ReloadScript(asset);
                };
            }
        }
    }
}
