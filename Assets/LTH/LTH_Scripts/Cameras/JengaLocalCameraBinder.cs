using Cinemachine;
using Photon.Pun;
using UnityEngine;
using UnityEngine.EventSystems;

public class JengaLocalCameraBinder : MonoBehaviour
{
    [Header("VCam 프리팹")]
    [SerializeField] private CinemachineVirtualCamera vcamPrefab;

    [Header("공용으로 항상 보이는 레이어")]
    [SerializeField] private LayerMask commonLayers;

    /// <summary>
    /// 로컬 플레이어 슬롯의 앵커에 vcam을 붙이고, 메인 카메라의 cullingMask를 슬롯 레이어로 제한.
    /// 타워가 생성된 직후(로컬 슬롯일 때) 호출.
    /// </summary>
    public void BindForLocal(int actorNumber, int slotIndex, Transform cameraAnchor, Transform lookTarget, string[] arenaLayerNames)
    {
        if (!vcamPrefab || !cameraAnchor || !lookTarget)
        {
            Debug.LogError("[JengaLocalCameraBinder] Prefab/Anchor/LookTarget 누락");
            return;
        }

        // vcam 생성 & 배치
        var vcam = Instantiate(vcamPrefab, cameraAnchor.position, cameraAnchor.rotation, cameraAnchor);
        vcam.Priority = 20;

        // 앵커 고정
        vcam.Follow = cameraAnchor;
        vcam.LookAt = null;
        vcam.m_Lens.FieldOfView = 60f;

        // 기존에 붙어 있을 수 있는 컴포넌트들 제거
        var framing = vcam.GetCinemachineComponent<CinemachineFramingTransposer>();
        if (framing) Destroy(framing);

        var composer = vcam.GetCinemachineComponent<CinemachineComposer>();
        if (composer) Destroy(composer);

        // Body = Transposer(WorldSpace, Offset=0, Damping=0) 로 고정
        var transposer = vcam.GetCinemachineComponent<CinemachineTransposer>();
        if (!transposer) transposer = vcam.AddCinemachineComponent<CinemachineTransposer>();
        transposer.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
        transposer.m_FollowOffset = Vector3.zero;
        transposer.m_XDamping = transposer.m_YDamping = transposer.m_ZDamping = 0f;

        // 메인 카메라 가져오기
        var main = Camera.main;
        if (!main)
        {
            Debug.LogError("[JengaLocalCameraBinder] MainCamera를 찾지 못했습니다.");
            return;
        }

        //// 레이어 커링: 내 슬롯 레이어 + 공용
        //int slot = JengaTowerManager.Instance.GetSlotIndexOf(actorNumber);
        //var layerName = arenaLayerNames[slot % arenaLayerNames.Length];
        //int layer = LayerMask.NameToLayer(layerName);
        //int slotMask = (layer >= 0 ? (1 << layer) : 0);

        // 전달받은 slotIndex 사용
        var layerName = arenaLayerNames[slotIndex % arenaLayerNames.Length];
        int layer = LayerMask.NameToLayer(layerName);

        // 레이어 미정의 시 폴백(모든 아레나 레이어 보기)
        int slotMask = 0;
        if (layer >= 0)
        {
            slotMask = 1 << layer;
        }
        else
        {
            Debug.LogError($"[JengaLocalCameraBinder] Layer '{layerName}'가 정의되어 있지 않습니다. 폴백으로 모든 아레나 레이어를 표시합니다.");
            foreach (var n in arenaLayerNames)
            {
                int l = LayerMask.NameToLayer(n);
                if (l >= 0) slotMask |= 1 << l;
            }
            // 그래도 없으면 최소한 Everything으로
            if (slotMask == 0) slotMask = ~ 0;
        }

        main.cullingMask = commonLayers.value | slotMask;

        // 클릭/터치용 Raycaster 보장
        if (!main.TryGetComponent(out PhysicsRaycaster pr))
            pr = main.gameObject.AddComponent<PhysicsRaycaster>();
        pr.eventMask = main.cullingMask;

        Debug.Log($"[JengaLocalCameraBinder] Bound → actor={actorNumber}, slot={slotIndex}, layer={layerName}({layer}), mask=0x{main.cullingMask:X}");
    }
}
