using UnityEngine.UI;
using UnityEngine;

public class JengaUIOverlayController : MonoBehaviour
{
    [SerializeField] private TowerFocusOverlay overlay;       // 인스펙터에서 연결
    [SerializeField] private RenderTexture towerRT;           // TowerCam의 RT (연결해두면 됨)

    void OnEnable() => JengaBlock.OnAnyBlockSelected += HandleSelected;
    void OnDisable() => JengaBlock.OnAnyBlockSelected -= HandleSelected;

    private void HandleSelected(JengaBlock block)
    {
        if (block == null || overlay == null) return;

        // 오버레이가 떠 있는 상태에서 내부 탭으로 발생한 이벤트는 무시 (재초기화 방지)
        if (overlay.IsActive) return;

        // 내 블록만 - JengaBlock 내부에서도 가드하지만 한번 더
        if (block.OwnerActorNumber != Photon.Pun.PhotonNetwork.LocalPlayer.ActorNumber) return;

        var tower = JengaTowerManager.Instance?.GetPlayerTower(block.OwnerActorNumber);
        if (tower == null) return;

        // RenderTexture 바인딩 후 오버레이 오픈
        overlay.Bind(tower, towerRT);
        overlay.ShowFacing(block);
    }
}
