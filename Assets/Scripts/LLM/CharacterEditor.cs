using UnityEngine;
using UnityEditor;
using LLMUnity;

[CustomEditor(typeof(Character))]
public class CharacterEditor : LLMCharacterEditor
{
    public override void OnInspectorGUI()
    {
        // Ensure all fields from LLMCharacterEditor are drawn
        base.OnInspectorGUI();

        // Draw fields specific to Character
        SerializedObject characterSO = new SerializedObject(target);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Character-Specific Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(characterSO.FindProperty("characterKnowledge"));
        EditorGUILayout.PropertyField(characterSO.FindProperty("characterImage"));
        EditorGUILayout.PropertyField(characterSO.FindProperty("characterName"));

        // Apply changes
        characterSO.ApplyModifiedProperties();
    }
}