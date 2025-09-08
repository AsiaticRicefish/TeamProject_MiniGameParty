using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public class BlockOutlineURP : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] Color edgeColor = new Color(0f, 0.9f, 1f, 1f);
    [SerializeField] float edgeThickness = 0.02f;   // 라인 굵기(로컬 단위)
    [SerializeField] float outwardOffset = 0.001f;  // 본체와의 간격(지퍼현상 방지)
    [SerializeField] Material sharedEdgeMaterial;   // 비워두면 URP/Unlit 생성

    Transform root;
    static Material _fallbackMat;

    void OnEnable()
    {
        BuildIfNeeded();
        // 기본은 꺼두자(Highlight 요청 시만 보이도록)
        if (root) root.gameObject.SetActive(false);
    }

    void OnDisable()
    {
        // 에디터/런타임 안전 삭제
        if (root)
        {
            if (Application.isEditor) DestroyImmediate(root.gameObject);
            else Destroy(root.gameObject);
            root = null;
        }
    }

    void OnValidate()
    {
        if (root)
        {
            // 색/두께 변경 반영을 위해 재생성
            Rebuild();
        }
    }

    public void Show()
    {
        BuildIfNeeded();
        if (root) root.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (root) root.gameObject.SetActive(false);
    }

    public void SetColor(Color c)
    {
        edgeColor = c;
        var mat = GetOrCreateMat();
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", edgeColor);
    }

    public void Rebuild()
    {
        if (root)
        {
            if (Application.isEditor) DestroyImmediate(root.gameObject);
            else Destroy(root.gameObject);
            root = null;
        }
        BuildIfNeeded();
    }

    void BuildIfNeeded()
    {
        if (root != null) return;

        var mf = GetComponentInChildren<MeshFilter>();
        if (!mf || !mf.sharedMesh) return;

        Bounds lb = mf.sharedMesh.bounds;
        Vector3 ext = lb.extents + Vector3.one * outwardOffset;

        root = new GameObject("__Edges").transform;
        root.SetParent(mf.transform, false);

        var mat = GetOrCreateMat();

        // 12개 모서리 생성
        // X방향 4개 (y=±, z=±)
        CreateEdge(new Vector3(0, +ext.y, +ext.z), new Vector3(ext.x * 2, edgeThickness, edgeThickness), mat);
        CreateEdge(new Vector3(0, +ext.y, -ext.z), new Vector3(ext.x * 2, edgeThickness, edgeThickness), mat);
        CreateEdge(new Vector3(0, -ext.y, +ext.z), new Vector3(ext.x * 2, edgeThickness, edgeThickness), mat);
        CreateEdge(new Vector3(0, -ext.y, -ext.z), new Vector3(ext.x * 2, edgeThickness, edgeThickness), mat);

        // Y방향 4개 (x=±, z=±)
        CreateEdge(new Vector3(+ext.x, 0, +ext.z), new Vector3(edgeThickness, ext.y * 2, edgeThickness), mat);
        CreateEdge(new Vector3(+ext.x, 0, -ext.z), new Vector3(edgeThickness, ext.y * 2, edgeThickness), mat);
        CreateEdge(new Vector3(-ext.x, 0, +ext.z), new Vector3(edgeThickness, ext.y * 2, edgeThickness), mat);
        CreateEdge(new Vector3(-ext.x, 0, -ext.z), new Vector3(edgeThickness, ext.y * 2, edgeThickness), mat);

        // Z방향 4개 (x=±, y=±)
        CreateEdge(new Vector3(+ext.x, +ext.y, 0), new Vector3(edgeThickness, edgeThickness, ext.z * 2), mat);
        CreateEdge(new Vector3(+ext.x, -ext.y, 0), new Vector3(edgeThickness, edgeThickness, ext.z * 2), mat);
        CreateEdge(new Vector3(-ext.x, +ext.y, 0), new Vector3(edgeThickness, edgeThickness, ext.z * 2), mat);
        CreateEdge(new Vector3(-ext.x, -ext.y, 0), new Vector3(edgeThickness, edgeThickness, ext.z * 2), mat);

        // 기본 렌더 설정
        foreach (Transform t in root)
        {
            var mr = t.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.allowOcclusionWhenDynamic = false;
            mr.renderingLayerMask = 1;
        }

        root.gameObject.SetActive(false);
    }

    Material GetOrCreateMat()
    {
        if (sharedEdgeMaterial) return sharedEdgeMaterial;

        if (_fallbackMat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            _fallbackMat = new Material(shader) { enableInstancing = true };
        }
        if (_fallbackMat.HasProperty("_BaseColor"))
        {
            _fallbackMat.SetColor("_BaseColor", edgeColor);
        }

        return _fallbackMat;
    }

    void CreateEdge(Vector3 localPos, Vector3 localScale, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "edge";
        go.transform.SetParent(root, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = localScale;

        var col = go.GetComponent<Collider>(); 
        if (Application.isEditor) DestroyImmediate(col); else Destroy(col);
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = m;
        go.layer = gameObject.layer;
    }
}