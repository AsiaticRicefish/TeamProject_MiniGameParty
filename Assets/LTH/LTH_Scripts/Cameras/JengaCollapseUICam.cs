using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class JengaCollapseUICam : MonoBehaviour
{
    [Header("Camera / RT")]
    [SerializeField] private Camera cam;
    [SerializeField] private RenderTexture targetRT;
    [SerializeField] private Vector2Int rtSize = new(1024, 1024);

    [Header("Framing")]
    [SerializeField] private Vector3 offset = new(0f, 2.2f, -3.0f);
    [SerializeField] private float fov = 40f;
    [SerializeField] private float lookUpOffset = 0.3f;   // 살짝 위를 보게

    void Reset() => EnsureDefaults();
    void Awake() => EnsureDefaults();

    private void EnsureDefaults()
    {
        if (!cam) cam = GetComponent<Camera>();
        if (!cam) cam = gameObject.AddComponent<Camera>();

        // 카메라 기본 세팅(빌트인/URP 공용)
        cam.enabled = false;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0); // 투명
        cam.allowHDR = false;
        cam.allowMSAA = false;
        cam.fieldOfView = fov;

        // RT 보정
        if (!targetRT || targetRT.width != rtSize.x || targetRT.height != rtSize.y)
        {
            if (targetRT) targetRT.Release();
            targetRT = new RenderTexture(rtSize.x, rtSize.y, 16, RenderTextureFormat.ARGB32)
            {
                useMipMap = false,
                autoGenerateMips = false,
                antiAliasing = 1
            };
            targetRT.Create();
        }
    }

    /// <summary>
    /// 한 프레임 켰다가 끄는 방식으로 캡처
    /// settleDelay: 붕괴 애니가 끝난 뒤 살짝 기다렸다가 찍고 싶을 때(예: 0.05 ~ 0.2)
    /// </summary>
    public IEnumerator CaptureCo(GameObject towerRoot, LayerMask arenaMask, float settleDelay, Action<RenderTexture> onDone)
    {
        if (settleDelay > 0f) yield return new WaitForSeconds(settleDelay);

        if (!PrepareFraming(towerRoot, arenaMask))
        {
            onDone?.Invoke(null);
            yield break;
        }

        var prev = cam.targetTexture;
        cam.targetTexture = targetRT;

        // SRP/URP에서도 확실히 그 프레임에 렌더되도록
        cam.enabled = true;
        // 렌더 큐에 올라가도록 프레임 끝까지 대기
        yield return new WaitForEndOfFrame();
        cam.enabled = false;

        cam.targetTexture = prev;
        onDone?.Invoke(targetRT);
    }

    private bool PrepareFraming(GameObject towerRoot, LayerMask arenaMask)
    {
        if (!cam || !towerRoot) return false;

        // 내 아레나만 찍기
        cam.cullingMask = arenaMask;

        // 타워 바운딩 계산
        var rends = towerRoot.GetComponentsInChildren<Renderer>(includeInactive: true);
        if (rends == null || rends.Length == 0) return false;

        var b = new Bounds(rends[0].bounds.center, Vector3.zero);
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        var look = b.center + Vector3.up * lookUpOffset;

        // 좌표 배치
        cam.transform.position = look + offset;
        cam.transform.LookAt(look);
        cam.fieldOfView = fov;

        // RT 사이즈 보증
        if (!targetRT || !targetRT.IsCreated())
        {
            EnsureDefaults();
        }
        return true;
    }

    public static bool IsSRPActive() => GraphicsSettings.currentRenderPipeline != null;
}
