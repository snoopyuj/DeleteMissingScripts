/*
 * Author: bwaynesu
 * Date: 2017-04-18
 * GitHub: https://github.com/snoopyuj
 * Description: This script is used to delete missing scripts in the scene and project.
 *
 * Change Log:
 *   - 1.0.0 (2024-01-14) - First version.
 *   - 1.1.0 (2024-11-14) - Add the ability to delete missing ScriptableObjects.
 */

using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
[CustomEditor(typeof(MonoBehaviour))]
public class DeleteMissingScriptsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (DeleteMissingScriptsWindow.window == null || !DeleteMissingScriptsWindow.activeDeleteMissingScript)
        {
            return;
        }

        var scriptProperty = serializedObject.FindProperty("m_Script");
        if (scriptProperty == null || scriptProperty.objectReferenceValue != null)
        {
            return;
        }

        if (!DeleteMissingScriptsWindow.missingMonoList.Exists(x => x == target))
        {
            DeleteMissingScriptsWindow.missingMonoList.Add(target);
        }
    }
}