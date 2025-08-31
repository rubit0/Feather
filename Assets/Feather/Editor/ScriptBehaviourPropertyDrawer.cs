using UnityEditor;
using UnityEngine;

namespace Feather.Editor
{
    [CustomPropertyDrawer(typeof(JavaScriptBehaviour.BridgeProperties))]
    public class BridgePropertiesDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            
            var nameProperty = property.FindPropertyRelative("name");
            var gameObjectProperty = property.FindPropertyRelative("gameObject");
            var componentProperty = property.FindPropertyRelative("component");
            
            var propertyName = nameProperty.stringValue;
            
            // Calculate rects
            var labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
            var fieldRect = new Rect(position.x + EditorGUIUtility.labelWidth, position.y, 
                position.width - EditorGUIUtility.labelWidth, position.height);
            
            // Draw label
            EditorGUI.LabelField(labelRect, new GUIContent(propertyName, $"JavaScript property: {propertyName}"));
            
            // Determine which field to show based on the decorator type
            var decoratorType = GetDecoratorFromProperty(property);
            
            if (decoratorType == "GameObject")
            {
                EditorGUI.PropertyField(fieldRect, gameObjectProperty, GUIContent.none);
            }
            else
            {
                EditorGUI.PropertyField(fieldRect, componentProperty, GUIContent.none);
            }
            
            EditorGUI.EndProperty();
        }
        
        private string GetDecoratorFromProperty(SerializedProperty property)
        {
            // Try to get the decorator type from the JavaScriptBehaviourEditor if available
            // This is a simplified version - in a real implementation you'd want to
            // get this from the analyzed script metadata
            return "Component"; // Default fallback
        }
    }
    
    // Custom property drawer for the script field to handle JavaScript files better
    [CustomPropertyDrawer(typeof(JavaScriptBehaviour), true)]
    public class JavaScriptBehaviourDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // This will be handled by the JavaScriptBehaviourEditor instead
            EditorGUI.PropertyField(position, property, label);
        }
    }
}
