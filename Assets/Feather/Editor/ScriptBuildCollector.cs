using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Feather.Editor
{
    public class ScriptBuildCollector : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        private const string ResourcesFolder = "Assets/Feather/Resources/Feather";
        private const string ScriptsFolder = ResourcesFolder + "/Scripts";
        private const string ManifestPath = ResourcesFolder + "/ScriptManifest.asset";

        public void OnPreprocessBuild(BuildReport report)
        {
            CollectScriptsForPlayer();
        }

        public static void CollectScriptsForPlayer()
        {
            EnsureFolders();

            var guids = AssetDatabase.FindAssets("t:JavaScript", new[] { "Assets" });
            var originals = new List<JavaScript>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(path.EndsWith(".js") || path.EndsWith(".jsu") || path.EndsWith(".jsfeather")))
                    continue;
                if (path.StartsWith(ScriptsFolder)) continue;

                var asset = AssetDatabase.LoadAssetAtPath<JavaScript>(path);
                if (asset == null || !asset.ExtendsJsBehaviour) continue;
                originals.Add(asset);

                // Also write a Resources copy as JavaScript for player loading by name
                var destPath = $"{ScriptsFolder}/{asset.name}.asset";
                var copy = ScriptableObject.CreateInstance<JavaScript>();
                copy.SetImportData(asset.text, asset.ClassName, asset.ExtendsJsBehaviour, asset.ImportError);
                copy.name = asset.name;

                var existing = AssetDatabase.LoadAssetAtPath<JavaScript>(destPath);
                if (existing != null)
                    AssetDatabase.DeleteAsset(destPath);

                AssetDatabase.CreateAsset(copy, destPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var collected = AssetDatabase.FindAssets("t:JavaScript", new[] { ScriptsFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<JavaScript>)
                .Where(a => a != null)
                .ToList();

            var manifest = AssetDatabase.LoadAssetAtPath<ScriptManifest>(ManifestPath);
            if (manifest == null)
            {
                manifest = ScriptableObject.CreateInstance<ScriptManifest>();
                AssetDatabase.CreateAsset(manifest, ManifestPath);
            }

            manifest.scripts = originals.Concat(collected).Distinct().ToArray();
            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Feather] Collected {manifest.scripts.Length} JavaScript assets for player builds → {ManifestPath}");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Feather"))
                AssetDatabase.CreateFolder("Assets", "Feather");
            if (!AssetDatabase.IsValidFolder("Assets/Feather/Resources"))
                AssetDatabase.CreateFolder("Assets/Feather", "Resources");
            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
                AssetDatabase.CreateFolder("Assets/Feather/Resources", "Feather");
            if (!AssetDatabase.IsValidFolder(ScriptsFolder))
                AssetDatabase.CreateFolder(ResourcesFolder, "Scripts");
        }
    }
}
