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

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

[ExecuteInEditMode]
public class DeleteMissingScriptsWindow : EditorWindow
{
    private readonly float FixTimePerScript = 1.5f;

    public static EditorWindow window = null;
    public static bool activeDeleteMissingScript = false;
    public static List<Object> missingMonoList = new List<Object>();

    /// <summary>
    /// need to be public for FindProperty()
    /// </summary>
    public List<Object> missingObjList = new List<Object>();

    /// <summary>
    /// need to be public for FindProperty()
    /// </summary>
    public List<string> missingScriptableObjNameList = new List<string>();

    private int curDeleteIdx = -1;
    private int missingScriptsCount = 0;
    private Vector2 scrollPos = Vector2.zero;
    private bool isAutoWalkThroughList = false;
    private double curScriptFixStartTime = 0f;
    private bool isSearchIncludeScriptableObj = true;
    private List<string> missingScriptableObjPathList = new List<string>();

    [MenuItem("Window/bTools/Delete Missing Scripts", false)]
    public static void ShowWindow()
    {
        window = GetWindow(typeof(DeleteMissingScriptsWindow));
        window.titleContent.text = "Delete Missing Scripts";
    }

    private void Update()
    {
        if (missingMonoList.Count > 0)
        {
            DestroyMissingMonos();
        }

        if (!isAutoWalkThroughList)
        {
            return;
        }

        WalkThroughList();
    }

    private void OnDestroy()
    {
        ResetFindValue();
    }

    private void OnGUI()
    {
        if (window == null)
        {
            window = GetWindow(typeof(DeleteMissingScriptsWindow));
        }

        if (!isAutoWalkThroughList)
        {
            EditorGUILayout.LabelField("In Scene");
            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("Search Selected GameObjects"))
                {
                    ResetFindValue();
                    FindInSelected();
                }
                else if (GUILayout.Button("Search All GameObjects"))
                {
                    var oriSelectedObjs = Selection.objects;

                    Selection.objects = SceneManager.GetActiveScene().GetRootGameObjects();

                    ResetFindValue();
                    FindInSelected();

                    Selection.objects = oriSelectedObjs;
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("In Project");
            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("Search Selected Folder"))
                {
                    ResetFindValue();

                    var selectedPathList = GetSelectedPathsOrFallback();
                    var searchPathList = new List<string>();

                    for (var i = 0; i < selectedPathList.Count; ++i)
                    {
                        DirSearch(selectedPathList[i], ref searchPathList);
                    }

                    FindAllPrefabs(searchPathList.ToArray());
                }
                else if (GUILayout.Button("Search All Prefabs"))
                {
                    ResetFindValue();
                    FindAllPrefabs(AssetDatabase.GetAllAssetPaths());
                }

                isSearchIncludeScriptableObj = EditorGUILayout.Toggle("Include ScriptableObjects", isSearchIncludeScriptableObj);
            }
            EditorGUILayout.EndHorizontal();
        }

        if (missingObjList.Count > 0 || missingScriptableObjPathList.Count > 0)
        {
            ShowListInfo();
        }
    }

    private void WalkThroughList()
    {
        if (curDeleteIdx < 0 || curDeleteIdx >= missingObjList.Count)
        {
            Selection.activeObject = null;
            curDeleteIdx = -1;
            isAutoWalkThroughList = false;
            activeDeleteMissingScript = false;

            missingObjList.RemoveAll(x => x == null);
            AssetDatabase.SaveAssets();

            Repaint();
            Debug.Log("<color=green>End fixing</color>");

            return;
        }

        Selection.activeObject = missingObjList[curDeleteIdx];
        InternalEditorUtility.RepaintAllViews();

        var isFixTimeArrived = (EditorApplication.timeSinceStartup - curScriptFixStartTime) >= FixTimePerScript;
        var missingObj = (GameObject)missingObjList[curDeleteIdx];

        if (isFixTimeArrived || !IsExistMissingInGO(missingObj, false))
        {
            if (isFixTimeArrived)
            {
                Debug.LogWarning("Couldn't delete missing: [" + curDeleteIdx + "] <color=yellow>" + missingObj.name + "</color>");
            }
            else
            {
                Debug.Log("[" + curDeleteIdx + "] Fix Missing: " + missingObj.name);

                EditorUtility.SetDirty(missingObj);

                missingObjList[curDeleteIdx] = null;
            }

            ++curDeleteIdx;
            curScriptFixStartTime = EditorApplication.timeSinceStartup;
        }
    }

    private void DestroyMissingMonos()
    {
        missingScriptsCount -= missingMonoList.Count;

        for (var i = 0; i < missingMonoList.Count; ++i)
        {
            DestroyImmediate(missingMonoList[i], true);
        }

        missingMonoList.Clear();
    }

    private void DirSearch(string _sDir, ref List<string> _pathList)
    {
        try
        {
            foreach (var fileWithPath in Directory.GetFiles(_sDir))
            {
                var fileWithPathCorrect = fileWithPath.Replace("\\", "/");
                if (_pathList.Contains(fileWithPathCorrect))
                {
                    continue;
                }

                _pathList.Add(fileWithPathCorrect);
            }

            foreach (var dir in Directory.GetDirectories(_sDir))
            {
                DirSearch(dir, ref _pathList);
            }
        }
        catch (System.Exception e)
        {
            Debug.Log(e.Message);
        }
    }

    private void ResetFindValue()
    {
        missingScriptsCount = 0;
        activeDeleteMissingScript = false;
        curDeleteIdx = -1;

        missingMonoList.Clear();
        missingObjList.Clear();
        missingScriptableObjPathList.Clear();
        missingScriptableObjNameList.Clear();
    }

    private void FindInSelected()
    {
        var selectGoAry = Selection.gameObjects;

        for (var i = 0; i < selectGoAry.Length; ++i)
        {
            FindMissingAndAddToList(selectGoAry[i]);
        }
    }

    private bool FindMissingAndAddToList(GameObject _go, bool _isFindChild = true)
    {
        var monos = _go.GetComponents<MonoBehaviour>();
        var isMissingScriptsExist = false;

        for (var i = 0; i < monos.Length; i++)
        {
            if (monos[i] != null)
            {
                continue;
            }

            isMissingScriptsExist = true;

            ++missingScriptsCount;

            if (!missingObjList.Exists(x => x == _go))
            {
                missingObjList.Add(_go);
            }
        }

        if (!_isFindChild)
        {
            return isMissingScriptsExist;
        }

        foreach (Transform childTrans in _go.transform)
        {
            var isChildMissingScriptsExist = FindMissingAndAddToList(childTrans.gameObject, false);

            isMissingScriptsExist = isMissingScriptsExist || isChildMissingScriptsExist;
        }

        return isMissingScriptsExist;
    }

    private bool IsExistMissingInGO(GameObject _go, bool _isFindChild = true)
    {
        var monos = _go.GetComponents<MonoBehaviour>();

        for (var i = 0; i < monos.Length; i++)
        {
            if (monos[i] == null)
            {
                return true;
            }
        }

        if (!_isFindChild)
        {
            return false;
        }

        foreach (Transform childT in _go.transform)
        {
            if (IsExistMissingInGO(childT.gameObject))
            {
                return true;
            }
        }

        return false;
    }

    private void FindAllPrefabs(string[] _pathAry)
    {
        for (var i = 0; i < _pathAry.Length; ++i)
        {
            var path = _pathAry[i];
            var ext = Path.GetExtension(path);

            if (isSearchIncludeScriptableObj && ext == ".asset")
            {
                var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (asset == null && !missingScriptableObjPathList.Contains(path))
                {
                    missingScriptableObjPathList.Add(path);
                    missingScriptableObjNameList.Add(Path.GetFileNameWithoutExtension(path));
                }

                continue;
            }

            if (ext == ".meta" || ext != ".prefab")
            {
                continue;
            }

            var somePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (somePrefab == null)
            {
                Debug.LogWarning("Couldn't load the prefab: <color=yellow>" + path + "</color>. \t Please check it manually.");
                continue;
            }

            FindMissingAndAddToList(somePrefab);
        }
    }

    private void ShowListInfo()
    {
        EditorGUILayout.Space();
        EditorGUILayout.Space();

        if (missingObjList.Count > 0)
        {
            EditorGUILayout.LabelField("Missing Scripts Count: " + missingScriptsCount);
        }

        if (missingScriptableObjNameList.Count > 0)
        {
            EditorGUILayout.LabelField("Missing ScriptableObjects Count: " + missingScriptableObjNameList.Count);
        }

        EditorGUILayout.BeginHorizontal();
        {
            if (GUILayout.Button("Delete Missing"))
            {
                Debug.Log("<color=green>Start fixing</color>");

                DeleteMissingScriptableObjects();

                isAutoWalkThroughList = true;
                curDeleteIdx = 0;
                activeDeleteMissingScript = true;
                curScriptFixStartTime = EditorApplication.timeSinceStartup;
            }
            else if (isAutoWalkThroughList && GUILayout.Button("Pause Fixing"))
            {
                Debug.Log("Pause fixing");

                isAutoWalkThroughList = false;
                activeDeleteMissingScript = false;
            }
            else if (!isAutoWalkThroughList && curDeleteIdx >= 0 && GUILayout.Button("Resume Fixing"))
            {
                Debug.Log("Resume fixing");

                isAutoWalkThroughList = true;
                activeDeleteMissingScript = true;
                curScriptFixStartTime = EditorApplication.timeSinceStartup;
            }
            else if (GUILayout.Button("Clear List"))
            {
                ResetFindValue();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Width(window.position.width), GUILayout.Height(window.position.height - 180f));
        {
            ShowMissingObjList();
            ShowMissingScriptableObjNameList();
        }
        EditorGUILayout.EndScrollView();
    }

    private void ShowMissingObjList()
    {
        if (missingObjList.Count == 0)
        {
            return;
        }

        var so = new SerializedObject(this);
        var missingListProperty = so.FindProperty("missingObjList");

        if (missingListProperty == null)
        {
            return;
        }

        GUI.enabled = false;
        EditorGUILayout.PropertyField(missingListProperty, includeChildren: true);
        GUI.enabled = true;
    }

    private void ShowMissingScriptableObjNameList()
    {
        if (missingScriptableObjNameList.Count == 0)
        {
            return;
        }

        var so = new SerializedObject(this);
        var missingListProperty = so.FindProperty("missingScriptableObjNameList");

        if (missingListProperty == null)
        {
            return;
        }

        GUI.enabled = false;
        EditorGUILayout.PropertyField(missingListProperty, includeChildren: true);
        GUI.enabled = true;
    }

    private List<string> GetSelectedPathsOrFallback()
    {
        var pathList = new List<string>();

        foreach (var obj in Selection.GetFiltered(typeof(Object), SelectionMode.Assets))
        {
            var path = AssetDatabase.GetAssetPath(obj);

            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                path = Path.GetDirectoryName(path);
            }

            pathList.Add(path);
        }

        if (pathList.Count == 0)
        {
            pathList.Add("Assets");
        }

        return pathList;
    }

    private void DeleteMissingScriptableObjects()
    {
        var deleteFailedPathList = new List<string>();
        var deleteFailedNameList = new List<string>();

        AssetDatabase.StartAssetEditing();
        for (var i = 0; i < missingScriptableObjPathList.Count; ++i)
        {
            var path = missingScriptableObjPathList[i];
            var name = missingScriptableObjNameList[i];

            if (AssetDatabase.MoveAssetToTrash(path))
            {
                Debug.Log("[" + i + "] Delete Missing ScriptableObject: " + name);
            }
            else
            {
                Debug.LogWarning(
                    "Couldn't delete the ScriptableObject: <color=yellow>"
                    + name
                    + "</color>. Please check it manually. (<color=yellow>"
                    + path
                    + "</color>)");

                deleteFailedPathList.Add(path);
                deleteFailedNameList.Add(name);
            }
        }
        AssetDatabase.StopAssetEditing();

        missingScriptableObjPathList.Clear();
        missingScriptableObjNameList.Clear();

        missingScriptableObjPathList.AddRange(deleteFailedPathList);
        missingScriptableObjNameList.AddRange(deleteFailedNameList);
    }
}