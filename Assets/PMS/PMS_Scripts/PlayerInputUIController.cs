using System.Collections;
using System.Collections.Generic;
using ShootingScene.ShootingGame;
using UnityEngine;
using UnityEngine.UI;

public class PlayerInputUIController : MonoBehaviour
{
    [SerializeField] private DirectionUIArrow arrow;
    [SerializeField] private GameObject arrowRangeImage;
    [SerializeField] public GameObject PlayerMarker;

    //슈팅 UI 매니저 ui
    [SerializeField]private ChargeController charger;

    private void Awake()
    {
        charger = ShootingUIManager.Instance.GetChargingUI().GetComponent<ChargeController>();
    }

    #region 차징 UI 관련
    // 화살표 표시/숨기기
    public void ShowArrow(bool show)
    {
        if (arrow != null) arrow.gameObject.SetActive(show);
    }

    public void FreezeArrow() => arrow?.Freeze();
    public Vector3 GetArrowDir() => arrow != null ? arrow.CurrentDir : Vector3.zero;
    #endregion

    #region 화살 UI 반경 표시 
    public void ShowArrowRange(bool show)
    {
        if (arrowRangeImage != null) arrowRangeImage.SetActive(show);
    }
    #endregion

    #region 차징 UI 관련
    // 차징 표시/숨기기
    public void ShowCharger(bool show)
    {
        if (charger != null && charger.chargeSlider != null)
            ShootingUIManager.Instance.ShowChargingUI();//charger.chargeSlider.gameObject.SetActive(show);
    }


    public void StartCharge() => charger?.StartCharge();
    public void StopCharge() => charger?.StopCharge();
    public float GetChargePower() => charger != null ? charger.ChargePower : 0f;
    #endregion

    #region 플레이어 마커 UI 관련
    public void UpdatePlayerMarker(string playerUID)
    {
        // 특정 플레이어의 색깔만 가져오기
        Color MarkerColor = ShootingScene.ShootingGame.ShootingUIManager.Instance.GetPlayerColor(playerUID);

        // 네임태그나 다른 UI 요소에 색깔 적용
        PlayerMarker.GetComponent<Renderer>().material.color = MarkerColor;
    }
    #endregion
    // 초기화
    public void InitializeUI()
    {
        arrow?.Initialize();
        charger?.Initialize();
        ShowArrow(false);
    }
}
