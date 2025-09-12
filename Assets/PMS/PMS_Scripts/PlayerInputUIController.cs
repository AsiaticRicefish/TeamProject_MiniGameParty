using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInputUIController : MonoBehaviour
{
    [SerializeField] private DirectionUIArrow arrow;
    [SerializeField] private GameObject arrowRangeImage;
    [SerializeField] private ChargeController charger;

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
            charger.chargeSlider.gameObject.SetActive(show);
    }


    public void StartCharge() => charger?.StartCharge();
    public void StopCharge() => charger?.StopCharge();
    public float GetChargePower() => charger != null ? charger.ChargePower : 0f;
    #endregion 

    // 초기화
    public void InitializeUI()
    {
        arrow?.Initialize();
        charger?.Initialize();
        ShowArrow(false);
        ShowCharger(false);
    }
}
