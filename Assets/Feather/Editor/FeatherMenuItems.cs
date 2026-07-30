using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Feather.Editor
{
    public static class FeatherMenuItems
    {
        [MenuItem("Component/Feather/Add JavaScript…", false, 0)]
        public static void OpenAddJavaScriptPicker()
        {
            AddJavaScriptPickerWindow.ShowWindow();
        }

        [MenuItem("Component/Feather/JavaScript Behaviour (empty)", false, 100)]
        public static void AddEmptyJavaScriptBehaviour()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("No GameObject Selected",
                    "Select a GameObject, or use Component → Feather → Add JavaScript…", "OK");
                return;
            }

            Undo.AddComponent<JavaScriptBehaviour>(selected);
            Selection.activeGameObject = selected;
        }

        [MenuItem("Component/Feather/JavaScript Behaviour (empty)", true)]
        public static bool ValidateAddEmpty() => Selection.activeGameObject != null;

        // Unity 6 Create menu: keep this top-level; low priority so it sits near Scripting/Shader, not at the bottom (81).
        [MenuItem("Assets/Create/JavaScript Behaviour", false, 1)]
        public static void CreateJavaScriptFileInPlace()
        {
            var templatePath = FeatherPackageUtil.EditorFolder + "/Templates/JavaScriptBehaviour.js.txt";
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(templatePath, "JavaScriptBehaviour.js");
        }
    }

    public class AddJavaScriptPickerWindow : EditorWindow
    {
        private Vector2 _scroll;
        private string _filter = "";
        private (string path, JavaScript asset, string className)[] _entries;

        public static void ShowWindow()
        {
            var wnd = GetWindow<AddJavaScriptPickerWindow>(true, "Add JavaScript", true);
            wnd.minSize = new Vector2(360, 280);
            wnd.Refresh();
            wnd.ShowUtility();
        }

        private void OnEnable() => Refresh();

        private void Refresh()
        {
            var guids = AssetDatabase.FindAssets("t:JavaScript", new[] { "Assets" });
            _entries = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".js") || p.EndsWith(".jsu") || p.EndsWith(".jsfeather"))
                .Select(p =>
                {
                    var asset = AssetDatabase.LoadAssetAtPath<JavaScript>(p);
                    if (asset == null || !asset.ExtendsJsBehaviour)
                        return (path: p, asset: (JavaScript)null, className: (string)null);
                    var className = !string.IsNullOrEmpty(asset.ClassName) ? asset.ClassName : asset.name;
                    return (path: p, asset: asset, className: className);
                })
                .Where(e => e.className != null && e.asset != null)
                .OrderBy(e => e.className)
                .ToArray();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Select a JavaScript behaviour to add", EditorStyles.boldLabel);
            _filter = EditorGUILayout.TextField("Search", _filter);

            var go = Selection.activeGameObject;
            if (go == null)
            {
                EditorGUILayout.HelpBox("Select a GameObject in the Hierarchy first.", MessageType.Warning);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var entry in _entries)
            {
                if (!string.IsNullOrEmpty(_filter) &&
                    entry.className.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) < 0 &&
                    entry.path.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(entry.className, GUILayout.MinWidth(120));
                EditorGUILayout.LabelField(entry.path, EditorStyles.miniLabel);
                using (new EditorGUI.DisabledScope(go == null))
                {
                    if (GUILayout.Button("Add", GUILayout.Width(48)))
                    {
                        JavaScriptDragDropHandler.AddJavaScriptComponent(go, entry.asset);
                        Close();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Refresh"))
                Refresh();
        }
    }
}
