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

        var framing = vcam.GetCinemachineComponent<CinemachineFramingTransposer>();
        if (framing) Destroy(framing);

        var composer = vcam.GetCinemachineComponent<CinemachineComposer>();
        if (composer) Destroy(composer);

        var transposer = vcam.GetCinemachineComponent<CinemachineTransposer>();
        if (!transposer) transposer = vcam.AddCinemachineComponent<CinemachineTransposer>();
        transposer.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
        transposer.m_FollowOffset = Vector3.zero;
        transposer.m_XDamping = transposer.m_YDamping = transposer.m_ZDamping = 0f;

        var main = Camera.main;
        if (!main) return;

        var layerName = arenaLayerNames[slotIndex % arenaLayerNames.Length];
        int layer = LayerMask.NameToLayer(layerName);

        int slotMask = 0;
        if (layer >= 0)
        {
            slotMask = 1 << layer;
        }
        else
        {
            foreach (var n in arenaLayerNames)
            {
                int l = LayerMask.NameToLayer(n);
                if (l >= 0) slotMask |= 1 << l;
            }
            if (slotMask == 0) slotMask = ~ 0;
        }

        main.cullingMask = commonLayers.value | slotMask;

        if (!main.TryGetComponent(out PhysicsRaycaster pr))
            pr = main.gameObject.AddComponent<PhysicsRaycaster>();
        pr.eventMask = main.cullingMask;
    }
}
