/*
 * @author	Wayne Su
 * @date	2017/04/18
 */

using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
[CustomEditor(typeof(MonoBehaviour))]
public class DeleteMissingScriptsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (DeleteMissingScriptsWindow.window == null || !DeleteMissingScriptsWindow.activeDeleteMissingScript)
        {
            base.OnInspectorGUI();
            return;
        }

        var scriptProperty = serializedObject.FindProperty("m_Script");
        if (scriptProperty == null || scriptProperty.objectReferenceValue != null)
        {
            base.OnInspectorGUI();
            return;
        }

        if (!DeleteMissingScriptsWindow.missingMonoList.Exists(x => x == target))
        {
            DeleteMissingScriptsWindow.missingMonoList.Add(target);
        }
    }
}