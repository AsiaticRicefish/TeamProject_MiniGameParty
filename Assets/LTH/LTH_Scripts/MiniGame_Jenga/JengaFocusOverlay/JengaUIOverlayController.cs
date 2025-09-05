using UnityEngine.UI;
using UnityEngine;

public class JengaUIOverlayController : MonoBehaviour
{
    [SerializeField] private TowerFocusOverlay overlay;
    [SerializeField] private RenderTexture towerRT;

    void OnEnable() => JengaBlock.OnAnyBlockSelected += HandleSelected;
    void OnDisable() => JengaBlock.OnAnyBlockSelected -= HandleSelected;

    private void HandleSelected(JengaBlock block)
    {
        if (block == null || overlay == null) return;

        if (overlay.IsActive) return;

        if (block.OwnerActorNumber != Photon.Pun.PhotonNetwork.LocalPlayer.ActorNumber) return;

        var tower = JengaTowerManager.Instance?.GetPlayerTower(block.OwnerActorNumber);
        if (tower == null) return;

        overlay.Bind(tower, towerRT);
        overlay.ShowFacing(block);
    }
}
