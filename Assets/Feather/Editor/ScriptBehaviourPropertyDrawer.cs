using UnityEditor;
using UnityEngine;

namespace Feather.Editor
{
    // Legacy drawer kept for BridgeProperties when drawn as default array;
    // primary UI is JavaScriptBehaviourEditor.
    [CustomPropertyDrawer(typeof(JavaScriptBehaviour.BridgeProperties))]
    public class BridgePropertiesDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var nameProperty = property.FindPropertyRelative("name");
            EditorGUI.LabelField(position, nameProperty != null ? nameProperty.stringValue : label.text);
        }
    }
}
