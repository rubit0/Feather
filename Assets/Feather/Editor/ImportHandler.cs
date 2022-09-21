using UnityEditor.AssetImporters;
using UnityEngine;

namespace Feather.Editor
{
    [ScriptedImporter(2, "jsu")]
    public class ImportHandler : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            Debug.Log(ctx.assetPath);
            
        }
    }
}
