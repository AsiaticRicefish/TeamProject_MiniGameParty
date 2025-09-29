#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class ShaderUsageFinder : EditorWindow
{
    private Shader targetShader;

    [MenuItem("Tools/Shader Usage Finder")]
    static void OpenWindow() => GetWindow<ShaderUsageFinder>("Shader Usage Finder");

    void OnGUI()
    {
        targetShader = (Shader)EditorGUILayout.ObjectField("Target Shader", targetShader, typeof(Shader), false);

        if (targetShader != null && GUILayout.Button("Find Materials"))
            FindMaterials(targetShader);
    }

    static void FindMaterials(Shader shader)
    {
        var guids = AssetDatabase.FindAssets("t:Material");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null && mat.shader == shader)
                Debug.Log($"[Found] {mat.name} at {path}", mat);
        }
    }
}
#endif
