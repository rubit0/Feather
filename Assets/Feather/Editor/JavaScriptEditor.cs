using UnityEditor;
using UnityEngine;

namespace Feather.Editor
{
    [CustomEditor(typeof(JavaScript))]
    public class JavaScriptEditor : UnityEditor.Editor
    {
        private Vector2 _scroll;
        private bool _preview;

        public override void OnInspectorGUI()
        {
            var asset = (JavaScript)target;

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Script", asset, typeof(JavaScript), false);
            }

            EditorGUILayout.Space(4);

            if (asset.HasError)
            {
                EditorGUILayout.HelpBox(asset.ImportError, MessageType.Error);
            }
            else if (!asset.ExtendsJsBehaviour)
            {
                EditorGUILayout.HelpBox("This file has no class extending jsBehaviour.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField("Class", string.IsNullOrEmpty(asset.ClassName) ? "(unknown)" : asset.ClassName);
                EditorGUILayout.LabelField("Base", "jsBehaviour");
            }

            EditorGUILayout.Space(6);
            if (GUILayout.Button("Open", GUILayout.Height(24)))
            {
                FeatherOpenAssetHandler.TryOpenJavaScript(asset);
            }

            if (asset.ExtendsJsBehaviour && Selection.activeGameObject != null)
            {
                if (GUILayout.Button("Add to Selected GameObject"))
                {
                    JavaScriptDragDropHandler.AddJavaScriptComponent(Selection.activeGameObject, asset);
                }
            }

            EditorGUILayout.Space(8);
            _preview = EditorGUILayout.Foldout(_preview, "Script Preview", true);
            if (_preview)
            {
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(160));
                EditorGUILayout.TextArea(asset.text ?? string.Empty, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
        }

        public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height)
        {
            return FeatherEditorIcons.CreateStaticPreview(width, height)
                   ?? base.RenderStaticPreview(assetPath, subAssets, width, height);
        }
    }
}
