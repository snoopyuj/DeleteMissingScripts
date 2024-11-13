/*
 * @author	Wayne Su
 * @date	2017/04/18
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

    public List<Object> missingObjList = new List<Object>();    // need to be public for FindProperty()

    private int curDeleteIdx = -1;
    private int missingScriptsCount = 0;
    private Vector2 scrollPos = Vector2.zero;
    private bool isAutoWalkThroughList = false;
    private double curScriptFixStartTime = 0f;

    [MenuItem("Window/bTools/Delete Missing Scripts", false)]
    public static void ShowWindow()
    {
        window = GetWindow(typeof(DeleteMissingScriptsWindow));
        window.titleContent.text = "Delete Missing Scripts";
    }

    private void Update()
    {
        if (missingMonoList.Count > 0)
            DestroyMissingMonos();

        if (!isAutoWalkThroughList)
            return;

        WalkThroughList();
    }

    private void OnDestroy()
    {
        ResetFindValue();
    }

    private void OnGUI()
    {
        if (window == null)
            window = GetWindow(typeof(DeleteMissingScriptsWindow));

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

                    List<string> pathList = new List<string>();
                    DirSearch(GetSelectedPathOrFallback(), pathList);
                    FindAllPrefabs(pathList.ToArray());
                }
                else if (GUILayout.Button("Search All Prefabs"))
                {
                    ResetFindValue();
                    FindAllPrefabs(AssetDatabase.GetAllAssetPaths());
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        if (missingObjList.Count > 0)
            ShowListInfo();
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
            DestroyImmediate(missingMonoList[i], true);

        missingMonoList.Clear();
    }

    private void DirSearch(string _sDir, List<string> _pathList)
    {
        try
        {
            foreach (string f in Directory.GetFiles(_sDir))
                _pathList.Add(f);

            foreach (string d in Directory.GetDirectories(_sDir))
                DirSearch(d, _pathList);
        }
        catch (System.Exception _excpt)
        {
            Debug.Log(_excpt.Message);
        }
    }

    private void ResetFindValue()
    {
        missingScriptsCount = 0;
        missingObjList.Clear();
        activeDeleteMissingScript = false;
        curDeleteIdx = -1;
        missingMonoList.Clear();
    }

    private void FindInSelected()
    {
        GameObject[] selectGoAry = Selection.gameObjects;

        for (var i = 0; i < selectGoAry.Length; ++i)
        {
            FindMissingAndAddToList(selectGoAry[i]);
        }
    }

    private void FindMissingAndAddToList(GameObject _go)
    {
        FindInGO(_go);
    }

    private bool FindInGO(GameObject _go, bool _isFindChild = true)
    {
        MonoBehaviour[] monos = _go.GetComponents<MonoBehaviour>();
        bool isMissingScriptsExist = false;

        for (int i = 0; i < monos.Length; i++)
        {
            if (monos[i] == null)
            {
                isMissingScriptsExist = true;
                ++missingScriptsCount;

                if (!missingObjList.Exists(x => x == _go))
                    missingObjList.Add(_go);
            }
        }

        if (_isFindChild)
        {
            foreach (Transform childT in _go.transform)
            {
                bool isChildMissingScriptsExist = FindInGO(childT.gameObject);

                if (isChildMissingScriptsExist && !isMissingScriptsExist)
                    isMissingScriptsExist = true;
            }
        }

        return isMissingScriptsExist;
    }

    private bool IsExistMissingInGO(GameObject _go, bool _isFindChild = true)
    {
        MonoBehaviour[] monos = _go.GetComponents<MonoBehaviour>();

        for (int i = 0; i < monos.Length; i++)
        {
            if (monos[i] == null)
                return true;
        }

        if (_isFindChild)
        {
            foreach (Transform childT in _go.transform)
            {
                if (IsExistMissingInGO(childT.gameObject))
                    return true;
            }
        }

        return false;
    }

    private void FindAllPrefabs(string[] _pathAry)
    {
        string p = null;

        for (var i = 0; i < _pathAry.Length; ++i)
        {
            p = _pathAry[i];
            if (p.Contains(".meta") || !p.Contains(".prefab"))
                continue;

            GameObject somePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (somePrefab == null)
            {
                Debug.LogWarning("Couldn't load the prefab: <color=yellow>" + p + "</color>. \t Please check it manually.");
            }
            else
            {
                FindMissingAndAddToList(somePrefab);
            }
        }
    }

    private void ShowListInfo()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Missing Scripts Count: " + missingScriptsCount);

        EditorGUILayout.BeginHorizontal();
        {
            if (GUILayout.Button("Delete Missing"))
            {
                Debug.Log("Start fixing");

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

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Width(window.position.width), GUILayout.Height(window.position.height - 150f));
        {
            ShowMissingObjList();
        }
        EditorGUILayout.EndScrollView();
    }

    private void ShowMissingObjList()
    {
        SerializedObject so = new SerializedObject(this);
        SerializedProperty missingListProperty = so.FindProperty("missingObjList");

        if (missingListProperty != null)
            EditorGUILayout.PropertyField(missingListProperty, includeChildren: true);
    }

    private string GetSelectedPathOrFallback()
    {
        string path = "Assets";

        foreach (Object obj in Selection.GetFiltered(typeof(Object), SelectionMode.Assets))
        {
            path = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                path = Path.GetDirectoryName(path);
                break;
            }
        }

        return path;
    }
}