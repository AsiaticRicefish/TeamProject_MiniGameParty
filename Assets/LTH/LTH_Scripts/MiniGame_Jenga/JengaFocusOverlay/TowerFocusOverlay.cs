using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 젠가 타워 확대 모달 UI 컨트롤러 (월드 고정 정면 방식)
/// - Preview(RawImage)에 RenderTexture 표시
/// - RawImage 클릭 = 오버레이 내부 1차 선택
/// - OK = 2차 클릭(타이밍 시작) 대행
/// - 카메라는 항상 같은 '월드 정면'에서 타워를 본다
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

    [Header("World Front (고정 시선)")]
    [Tooltip("비워두면 월드 Vector3.forward 사용. 지정하면 그 Transform.forward를 '앞'으로 사용")]
    [SerializeField] private Transform worldFrontBasis;
    [Tooltip("앞/뒤가 반대로 보이면 체크")]
    [SerializeField] private bool invertWorldFront = false;

    // 상태
    private bool active;
    private JengaBlock primed;      // 월드에서 오버레이를 띄운 블록(즉시 롤백 대상)
    private JengaBlock hovered;
    private JengaBlock selected;    // 오버레이 내부에서 사용자가 고른 블록(OK 대상)
    private RawImageClickForwarder forwarder;

    // 캐시 (회전 시 지속적으로 사용)
    private JengaTower _boundTower;     // 지금 오버레이가 보고 있는 타워
    private float _targetY;             // 뷰의 높이(선택한 블록의 Y)
    private float _cachedBlockHeight;   // 지금 오버레이가 보고 있는 타워의 블록 높이

    // 현재 적용된 아레나 마스크
    [SerializeField] private LayerMask arenaMask;

    #region 타워 피벗 찾기 (카메라 타깃 중심만 잡는 용도)
    private Transform Basis => GetBasis(_boundTower);

    private Transform GetBasis(JengaTower tower)
    {
        if (tower == null) return null;
        var cur = tower.transform;

        // 부모들 중에 회전 컨트롤러가 붙은 트랜스폼을 찾는다
        while (cur != null)
        {
            if (cur.GetComponent<JengaRotateController>() != null)
                return cur;
            cur = cur.parent;
        }

        // 못 찾으면 타워 본체를 기준으로
        return tower.transform;
    }
    #endregion


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

    void OnEnable()
    {
        EnsureCameraAndTexture();
        if (arenaMask == 0 && JengaTowerManager.Instance != null)
        {
            var local = Photon.Pun.PhotonNetwork.LocalPlayer?.ActorNumber ?? 0;
            SetArenaMask(JengaTowerManager.Instance.GetArenaLayerMaskByActor(local));
        }
    }

    private void LateUpdate()
    {
        if (!active || _boundTower == null || towerCam == null) return;

        Reframe();
    }


    private void DetachAndSanitizeCamera()
    {
        var t = towerCam.transform;

        if (t.parent != null) t.SetParent(null, worldPositionStays: true);

        t.localScale = Vector3.one;
    }

    private void EnsureCameraAndTexture()
    {
        if (!towerCam) return;

        if (towerCam.targetTexture == null)
        {
            var rt = new RenderTexture(1024, 1024, 16, RenderTextureFormat.ARGB32);
            rt.name = "TowerFocus_RT";
            towerCam.targetTexture = rt;
            if (preview) preview.texture = rt;
        }
        else if (preview && preview.texture == null)
        {
            preview.texture = towerCam.targetTexture;
        }

        if (!towerCam.gameObject.activeSelf) towerCam.gameObject.SetActive(true);
        towerCam.enabled = true;

        DetachAndSanitizeCamera();
    }

    public void Bind(JengaTower targetTower, RenderTexture rt)
    {
        if (!preview) return;

        var tex = rt ?? (towerCam ? towerCam.targetTexture as RenderTexture : null);
        preview.texture = tex;

        if (tex != null && towerCam)
        {
            // 카메라에도 확실히 연결
            if (towerCam.targetTexture != tex) towerCam.targetTexture = tex;
            towerCam.aspect = (float)tex.width / tex.height;
        }
        else
        {
            EnsureCameraAndTexture();
        }
    }

    /// <summary>
    /// 아레나 레이어 마스크 동기 적용(카메라 + 포워더)
    /// </summary>
    public void SetArenaMask(LayerMask mask)
    {
        arenaMask = mask;
        if (towerCam) towerCam.cullingMask = mask;
        if (forwarder) forwarder.SetMask(LayerMask.GetMask("JengaFace"));
        //if (forwarder) forwarder.SetMask(mask);
    }

    /// <summary>
    /// 월드에서 블록이 선택되었을 때 호출: 고정 정면 프레이밍 + 오버레이 표시
    /// (면 판정 없이, 선택된 블록의 Y만 맞춤)
    /// </summary>
    public void ShowFacing(JengaBlock block)
    {
        if (!towerCam || !preview || !block) return;

        var tex = preview.texture as RenderTexture;
        if (tex != null)
        {
            towerCam.aspect = (float)tex.width / tex.height;
            towerCam.rect = new Rect(0, 0, 1, 1);
        }

        var mgr = JengaTowerManager.Instance;
        if (mgr != null) SetArenaMask(mgr.GetArenaLayerMaskByActor(block.OwnerActorNumber));

        var ownerTower = mgr?.GetPlayerTower(block.OwnerActorNumber);
        var rend = block.GetComponentInChildren<Renderer>();
        if (!ownerTower || !rend) return;

        var blockBounds = rend.bounds;
        _boundTower = ownerTower;
        _targetY = blockBounds.center.y;
        _cachedBlockHeight = blockBounds.size.y;

        Reframe();

        primed = block;
        NotifyHover(null);
        SetSelection(null);
        SetVisible(true);
        primed.ForceClearSelectionForOverlay();
    }


    /// <summary>
    /// 회전해도 카메라는 같은 월드 방향에서 본다.
    /// </summary>
    public void Reframe()
    {
        var tex = preview ? preview.texture as RenderTexture : null;
        float rtAspect = tex ? (float)tex.width / tex.height : 1f;

        var basis = Basis;
        if (basis == null || towerCam == null) return;

        // 월드 고정 방향 설정 (타워 회전과 무관하게 고정)
        Vector3 outward = worldFrontBasis ? worldFrontBasis.forward : Vector3.forward;
        if (invertWorldFront) outward = -outward;

        Vector3 flat = Vector3.ProjectOnPlane(outward, Vector3.up);
        if (flat.sqrMagnitude < 1e-6f) flat = Vector3.forward;
        flat.Normalize();

        float yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
        yaw = Mathf.Round(yaw / 90f) * 90f;
        Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        // 기본 카메라 회전 (월드 기준 수평)
        Quaternion baseLook = Quaternion.LookRotation(-dir, Vector3.up);

        // 타워의 물리적 기울어짐만 보정 (X,Z축만 - Y축 회전은 무시)
        Vector3 towerEuler = basis.rotation.eulerAngles;

        // X,Z축 기울어짐만 보정 (Y축은 0으로 설정하여 무시)
        Quaternion tiltCompensation = Quaternion.Euler(-towerEuler.x, 0, -towerEuler.z);

        // 최종 카메라 회전: 월드 고정 방향 + 기울어짐 보정
        Quaternion finalLook = baseLook * tiltCompensation;

        if (!Mathf.Approximately(slightTiltDeg, 0f))
            finalLook = finalLook * Quaternion.Euler(slightTiltDeg, 0f, 0f);

        Vector3 viewTarget = new Vector3(basis.position.x, _targetY, basis.position.z);
        float halfW = layerWidth * 0.5f * padding;
        float halfH = (_cachedBlockHeight * layerHeightMultiplier) * 0.5f * padding;

        if (useOrthographic)
        {
            towerCam.orthographic = true;
            float baseOrtho = Mathf.Max(halfH, halfW / rtAspect);
            towerCam.orthographicSize = baseOrtho * zoomFactor;

            towerCam.transform.SetPositionAndRotation(viewTarget + dir * orthoCamDist, finalLook);
            towerCam.nearClipPlane = 0.05f;
            towerCam.farClipPlane = 100f;
        }
        else
        {
            towerCam.orthographic = false;

            float vFov = towerCam.fieldOfView * Mathf.Deg2Rad;
            float hFov = 2f * Mathf.Atan(Mathf.Tan(vFov * 0.5f) * rtAspect);
            float distV = halfH / Mathf.Tan(vFov * 0.5f);
            float distH = halfW / Mathf.Tan(hFov * 0.5f);
            float dist = Mathf.Max(distV, distH) * zoomFactor;

            towerCam.transform.SetPositionAndRotation(viewTarget + dir * dist, finalLook);
            towerCam.nearClipPlane = 0.05f;
            towerCam.farClipPlane = useOrthographic ? 100f : Mathf.Max(100f, dist + 10f);
        }
    }

    public void Hide()
    {
        if (selected) selected.ForceClearSelectionForOverlay();
        if (primed && primed != selected) primed.ForceClearSelectionForOverlay();

        primed = null;
        selected = null;

        if (okButton) okButton.interactable = false;

        _boundTower = null;

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
            if (towerCam.gameObject.activeSelf != on)
                towerCam.gameObject.SetActive(on);
            towerCam.enabled = on;
        }

        if (on) EnsureCameraAndTexture();

        var ui = JengaUIManager.Instance;
        if (ui) ui.SetRotateButtonInteractable(!on);
    }

    public void SetWorldFrontBasis(Transform basis, bool invert = false)
    {
        worldFrontBasis = basis;
        invertWorldFront = invert;
    }

    public bool IsActive => active;
    public Camera TowerCam => towerCam;

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