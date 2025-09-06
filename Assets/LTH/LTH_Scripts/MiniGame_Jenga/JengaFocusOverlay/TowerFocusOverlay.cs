using System.Collections.Generic;
using Photon.Pun.Demo.Procedural;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 젠가 타워 확대 모달 UI 컨트롤러
/// - Preview(RawImage)에 RenderTexture 표시
/// - RawImage 클릭 = 오버레이 내부 1차 선택
/// - OK = 2차 클릭(타이밍 시작) 대행
/// - 월드 1차 클릭으로 열린 경우, 즉시 롤백해 상태가 남지 않도록 처리
/// - 선택 박스는 카메라 뷰포트(0~1) → RawImage drawRect 로 매핑해 정확히 맞춤
/// </summary>

public class TowerFocusOverlay : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RawImage preview;                 // RT 표시
    [SerializeField] private Button okButton;
    [SerializeField] private Button backdrop;                  // 바깥 클릭 닫힘

    [Header("Camera")]
    [SerializeField] private Camera towerCam;                  // 전용 미니카메라 (RT 출력용)
    [SerializeField] private bool useOrthographic = true;      // 권장: 오소그래픽
    [SerializeField] private float padding = 1.30f;            // 면 가로/세로 여백 배수(>1)
    [SerializeField] private float orthoCamDist = 3.0f;        // 오소일 때 거리(near/far 여유용)
    [SerializeField] private float slightTiltDeg = 0f;         // 살짝 위에서 보고 싶으면 1~3도

    [Header("Framing Settings")]
    [SerializeField] private float layerWidth = 3.0f;               // 프레이밍 가로 크기
    [SerializeField] private float layerHeightMultiplier = 2.5f;    // 블록 높이 배수 (3층 기준)
    [SerializeField] private float zoomFactor = 1.0f;               // 줌 조정 (1.0 = 기본, 0.7 = 더 가깝게)

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    // 상태
    private bool active;
    private JengaBlock primed;      // 월드에서 오버레이를 띄운 블록(즉시 롤백 대상)
    private JengaBlock hovered;
    private JengaBlock selected;    // 오버레이 내부에서 사용자가 고른 블록(OK 대상)
    private RawImageClickForwarder forwarder;


    // 현재 적용된 아레나 마스크
    [SerializeField] private LayerMask arenaMask;

    // RawImage가 비율 유지(레터박스)라면 true
    [SerializeField] private bool compensateLetterbox = true;

    void Awake()
    {
        if (!group) group = GetComponent<CanvasGroup>();
        forwarder = preview ? preview.GetComponent<RawImageClickForwarder>() : null;

        if (okButton) okButton.onClick.AddListener(OnOk);
        if (backdrop) backdrop.onClick.AddListener(Hide);

        SetSelection(null);
        SetVisible(false);
    }

    void OnDestroy()
    {
        if (okButton) okButton.onClick.RemoveListener(OnOk);
        if (backdrop) backdrop.onClick.RemoveListener(Hide);
    }

    // 오버레이 내부에서 마우스가 가리키는 블록 임시 하이라이트
    public void NotifyHover(JengaBlock block)
    {
        if (!active) return;
        if (hovered == block) return;

        // 이전 호버 끄기(선택된 블록과 다를 때만)
        if (hovered && hovered != selected)
            hovered.Highlight(false);

        hovered = block;

        // 새 호버 켜기(선택된 블록과 다를 때만)
        if (hovered && hovered != selected)
            hovered.Highlight(true);
    }


    public void Bind(JengaTower targetTower, RenderTexture rt)
    {
        if (!preview) return;

        var tex = rt ?? (towerCam ? towerCam.targetTexture as RenderTexture : null);
        preview.texture = tex;

        if (tex != null && towerCam)
        {
            towerCam.aspect = (float)tex.width / tex.height;
        }
        else
        {
            Debug.LogWarning("[TowerFocusOverlay] Preview RenderTexture가 비어 있습니다.");
        }
    }

    /// <summary>
    /// 아레나 레이어 마스크 동기 적용(카메라 + 포워더)
    /// </summary>
    public void SetArenaMask(LayerMask mask)
    {
        arenaMask = mask;
        if (towerCam) towerCam.cullingMask = mask;
        if (forwarder) forwarder.SetMask(mask);
    }

    /// <summary>
    /// 월드에서 블록이 선택되었을 때 호출: 면 정면 프레이밍 + 오버레이 표시
    /// → 동시에 월드의 1차 선택 상태는 즉시 롤백(취소시 2차로 안 넘어가게)
    /// </summary>
    public void ShowFacing(JengaBlock block)
    {
        if (!towerCam || !preview || !block) return;

        // 카메라/RT 비율 동기화
        var tex = preview.texture as RenderTexture;
        if (tex != null)
        {
            float rtAspect = (float)tex.width / tex.height;
            towerCam.aspect = rtAspect;  
            towerCam.rect = new Rect(0, 0, 1, 1);
        }

        // 아레나 마스크 적용
        var mgr = JengaTowerManager.Instance;
        if (mgr != null)
        {
            SetArenaMask(mgr.GetArenaLayerMaskByActor(block.OwnerActorNumber));
        }

        var ownerTower = JengaTowerManager.Instance?.GetPlayerTower(block.OwnerActorNumber);
        var rend = block.GetComponentInChildren<Renderer>();
        if (!ownerTower || !rend) return;

        var blockBounds = rend.bounds;

        // 선택된 블록의 Y 좌표를 기준으로 3층 영역의 중심 계산
        float selectedBlockY = blockBounds.center.y;
        float blockHeight = blockBounds.size.y;

        // 뷰 타겟: 선택된 블록과 같은 Y 높이
        Vector3 viewTarget = new Vector3(
            ownerTower.transform.position.x,
            selectedBlockY,
            ownerTower.transform.position.z
        );

        // 카메라 방향 설정
        Vector3 normal = -ownerTower.transform.forward;

        float halfW = layerWidth * 0.5f * padding;
        float halfH = (blockHeight * layerHeightMultiplier) * 0.5f * padding;

        // ---- 카메라 프레이밍 ----

        float rtAspect2 = 1f;
        if (tex != null) rtAspect2 = (float)tex.width / tex.height;

        if (useOrthographic)
        {
            towerCam.orthographic = true;

            float baseOrthoSize = Mathf.Max(halfH, halfW / rtAspect2);
            towerCam.orthographicSize = baseOrthoSize * zoomFactor;

            towerCam.transform.position = viewTarget - normal * orthoCamDist;
            towerCam.transform.rotation = Quaternion.LookRotation(normal, Vector3.up)
                                        * Quaternion.Euler(slightTiltDeg, 0f, 0f);

            towerCam.nearClipPlane = 0.05f;
            towerCam.farClipPlane = 100f;
        }
        else
        {
            towerCam.orthographic = false;

            // 가로 FOV를 rtAspect 기준으로 계산
            float vFov = towerCam.fieldOfView * Mathf.Deg2Rad;
            float hFov = 2f * Mathf.Atan(Mathf.Tan(vFov * 0.5f) * rtAspect2);

            float distV = halfH / Mathf.Tan(vFov * 0.5f);
            float distH = halfW / Mathf.Tan(hFov * 0.5f);
            float baseDist = Mathf.Max(distV, distH);

            float dist = baseDist * zoomFactor;

            towerCam.transform.position = viewTarget - normal * dist;
            towerCam.transform.rotation = Quaternion.LookRotation(normal, Vector3.up)
                                        * Quaternion.Euler(slightTiltDeg, 0f, 0f);

            towerCam.nearClipPlane = 0.05f;
            towerCam.farClipPlane = Mathf.Max(100f, dist + 10f);
        }

        primed = block;
        NotifyHover(null);
        SetSelection(null);
        SetVisible(true);
        primed.ForceClearSelectionForOverlay();
    }

    public void Hide()
    {
        // 최소 하는 경우 오버레이 내부에서 선택된 블록이 타이밍 시작된 상태가 됨
        if (selected) selected.ForceClearSelectionForOverlay();

        if (primed && primed != selected) primed.ForceClearSelectionForOverlay();

        primed = null;
        selected = null;

        if (okButton) okButton.interactable = false;

        SetVisible(false);
    }

    private void SetVisible(bool on)
    {
        active = on;
        if (!group) return;

        group.alpha = on ? 1f : 0f;
        group.blocksRaycasts = on;
        group.interactable = on;

        if (towerCam)
        {
            // 카메라 GO 자체도 토글 (비활성 GO면 enabled만으론 렌더 안 됨)
            if (towerCam.gameObject.activeSelf != on)
                towerCam.gameObject.SetActive(on);
            towerCam.enabled = on;
        }
    }

    public bool IsActive => active;
    public Camera TowerCam => towerCam;

    /// <summary>
    /// RawImageClickForwarder가 호출: 1차 선택 처리
    /// </summary>
    public void NotifyBlockTapped(JengaBlock block)
    {
        if (!active || block == null) return;

        // 보호층은 미리 차단
        var tower = JengaTowerManager.Instance?.GetPlayerTower(block.OwnerActorNumber);
        if (tower == null || tower.IsLayerTopProtected(block.Layer))
        {
            SetSelection(null);
            return;
        }

        if (!block.IsCurrentlySelected)
        {
            // 1차 클릭(선택)만 대행
            var ped = new PointerEventData(EventSystem.current);
            block.OnPointerClick(ped);
        }

        // 실제로 1차 선택이 되었는지 확인
        if (block.IsCurrentlySelected)
        {
            // 기존 선택과 다르면 이전 선택 정리
            if (selected && selected != block)
                selected.ForceClearSelectionForOverlay();

            // 호버 오프 & 선택 고정
            NotifyHover(null);
            selected = block;
            selected.Highlight(true);

            if (okButton) okButton.interactable = true; // OK만 2차 클릭을 보냄
        }
        else
        {
            // 룰에 막힌 경우 등
            SetSelection(null);
        }
    }

    private void SetSelection(JengaBlock block)
    {
        if (selected && selected != block)
            selected.ForceClearSelectionForOverlay();

        selected = block;

        bool canOk = selected != null && selected.IsCurrentlySelected;
        if (okButton) okButton.interactable = canOk;

        if (!canOk || selected == null) return;

        selected.Highlight(true);
    }

    private void OnOk()
    {
        if (selected == null || !selected.IsCurrentlySelected) return;

        // 2차 클릭(타이밍 시작) 대행 → 여기서 OnAnyBlockTimingStart가 발행되어 타이밍 UI가 뜸
        var ped = new PointerEventData(EventSystem.current);
        selected.OnPointerClick(ped);

        // 프라임과 선택 상태 정리(중복 하이라이트 방지)
        if (primed && primed != selected) primed.ForceClearSelectionForOverlay();
        primed = null;
        selected = null;

        Hide();
    }
}