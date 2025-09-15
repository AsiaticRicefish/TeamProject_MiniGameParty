using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ClickFaceBuilder
{
    /// marginXRatio: 좌우 여백(가로 비율), marginYRatio: 위/아래 여백(세로 비율)
    public static void AddFacesFromBox(JengaBlock b,
        float marginXRatio = 0.02f,  // 2%
        float marginYRatio = 0.12f,  // 12%  ← 층 사이 밴드 막힘 정도
        float thickness = 0.002f,    // 클릭면 두께(아주 얇게)
        float surfaceEps = 0.0005f)  // 표면에서 살짝 띄우기(붙어 보이도록)
    {
        // 블록 로컬 기준 크기 확보
        var box = b.GetComponent<BoxCollider>();
        if (!box) box = b.gameObject.AddComponent<BoxCollider>();
        var size = box.size;
        var half = size * 0.5f;

        // 좌/우 면 (로컬 X)  → 가로는 블록의 길이(size.z)
        CreateFace(b, "Right", Vector3.right, half.x + surfaceEps,
            width: size.z, height: size.y,
            marginXRatio, marginYRatio, thickness);

        CreateFace(b, "Left", Vector3.left, half.x + surfaceEps,
            width: size.z, height: size.y,
            marginXRatio, marginYRatio, thickness);
    }


    private static void CreateFace(
      JengaBlock b, string name, Vector3 localForward, float localDist,
      float width, float height, float marginXRatio, float marginYRatio, float thickness)
    {
        var go = new GameObject("ClickFace_" + name);
        go.layer = LayerMask.NameToLayer("JengaFace");
        var t = go.transform;
        t.SetParent(b.transform, false);
        t.localRotation = Quaternion.LookRotation(localForward, Vector3.up);
        t.localPosition = localForward.normalized * localDist;

        // 여백 적용(비율 기반 → 에셋 크기 바뀌어도 자동 보정)
        float w = Mathf.Max(0.0001f, width * (1f - 2f * marginXRatio));
        float h = Mathf.Max(0.0001f, height * (1f - 2f * marginYRatio));

        var bc = go.AddComponent<BoxCollider>();
        bc.isTrigger = false;
        bc.size = new Vector3(w, h, thickness);

        var proxy = go.AddComponent<FaceHitProxy>();
        proxy.owner = b;
    }
}