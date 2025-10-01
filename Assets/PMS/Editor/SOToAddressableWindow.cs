using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

public class SOToAddressableWindow : EditorWindow
{
    private MonoScript selectedScript;   // ScriptableObject 타입 선택
    private int selectedGroupIndex = 0;  // 그룹 선택 인덱스
    private string newGroupName = "NewGroup"; // 새 그룹 이름 입력
    private bool strictMode = false; // true면 같은 이름 그룹 있으면 return

    [MenuItem("Tools/SO → Addressables (Advanced)")]
    public static void ShowWindow()
    {
        GetWindow<SOToAddressableWindow>("SO To Addressables");
    }

    private void OnGUI()
    {
        GUILayout.Label("Register ScriptableObjects to Addressables", EditorStyles.boldLabel);

        // SO 타입 선택
        selectedScript = (MonoScript)EditorGUILayout.ObjectField("SO Script", selectedScript, typeof(MonoScript), false);

        // Addressables Settings 가져오기
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        if (settings == null)
        {
            EditorGUILayout.HelpBox("AddressableAssetSettings not found.", MessageType.Error);
            return;
        }

        // 그룹 리스트 가져오기
        List<string> groupNames = new List<string>();
        foreach (var g in settings.groups)
            groupNames.Add(g.Name);

        groupNames.Add("➕ Create New Group"); // 새 그룹 옵션

        // 그룹 선택 드롭다운
        selectedGroupIndex = EditorGUILayout.Popup("Target Group", selectedGroupIndex, groupNames.ToArray());

        if (selectedGroupIndex == groupNames.Count - 1) // "Create New Group"
        {
            newGroupName = EditorGUILayout.TextField("New Group Name", newGroupName);
            strictMode = EditorGUILayout.Toggle("Strict Mode", strictMode);
        }

        // 등록 버튼
        if (GUILayout.Button("Register"))
        {
            RegisterSO(settings, groupNames);
        }
    }

    private void RegisterSO(AddressableAssetSettings settings, List<string> groupNames)
    {
        if (selectedScript == null)
        {
            Debug.LogError("Please select a ScriptableObject script.");
            return;
        }

        Type soType = selectedScript.GetClass();
        if (soType == null || !soType.IsSubclassOf(typeof(ScriptableObject)))
        {
            Debug.LogError("Selected script is not a valid ScriptableObject type.");
            return;
        }

        string typeName = soType.Name;

        // 그룹 결정
        AddressableAssetGroup group = null;
        if (selectedGroupIndex == groupNames.Count - 1) // 새 그룹 생성 옵션 선택
        {
            var existingGroup = settings.FindGroup(newGroupName);
            if (existingGroup != null)
            {
                if (strictMode)
                {
                    Debug.LogWarning($"⚠️ Group '{newGroupName}' already exists. StrictMode enabled → 작업 중단.");
                    return;
                }
                else
                {
                    Debug.Log($"Group '{newGroupName}' already exists. Using existing group.");
                    group = existingGroup;
                }
            }
            else
            {
                group = settings.CreateGroup(
                    newGroupName,
                    false, false, false,
                    null,
                    typeof(BundledAssetGroupSchema),
                    typeof(ContentUpdateGroupSchema)
                );
                Debug.Log($"Created new Addressables group: {newGroupName}");
            }
        }
        else
        {
            group = settings.FindGroup(groupNames[selectedGroupIndex]);
        }

        if (group == null)
        {
            Debug.LogError("Target group not found or could not be created.");
            return;
        }

        // SO 타입 검색
        string[] guids = AssetDatabase.FindAssets($"t:{typeName}");
        if (guids == null || guids.Length == 0)
        {
            Debug.LogWarning($"No {typeName} assets found.");
            return;
        }

        int count = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = System.IO.Path.GetFileNameWithoutExtension(path);
            count++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Registered {count} {typeName} assets into group '{group.Name}'.");
    }
}