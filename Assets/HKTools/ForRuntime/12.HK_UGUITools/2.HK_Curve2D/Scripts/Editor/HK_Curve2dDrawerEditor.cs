using UnityEditor;

namespace HKTools
{
    [CustomEditor(typeof(HK_Curve2dDrawer))]
    public class HK_Curve2dDrawerEditor : Editor
    {
        SerializedProperty curve;
        SerializedProperty color;
        SerializedProperty sprite;
        SerializedProperty lineWidth;
        SerializedProperty accuracy;
        SerializedProperty textureOffset;

        void OnEnable()
        {
            curve = serializedObject.FindProperty("curve");
            sprite = serializedObject.FindProperty("m_Sprite");
            color = serializedObject.FindProperty("m_Color");
            lineWidth = serializedObject.FindProperty("lineWidth");
            accuracy = serializedObject.FindProperty("accuracy");
            textureOffset = serializedObject.FindProperty("textureOffset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(curve);

            EditorGUILayout.PropertyField(color);
            EditorGUILayout.PropertyField(sprite);

            EditorGUILayout.PropertyField(lineWidth);
            EditorGUILayout.PropertyField(accuracy);
            EditorGUILayout.PropertyField(textureOffset);

            serializedObject.ApplyModifiedProperties();
        }
    }
}