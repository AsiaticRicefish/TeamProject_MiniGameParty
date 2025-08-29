using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

public class ChargeController : MonoBehaviourPun
{
    public float chargeMax = 25f;
    public float chargePeriod = 4f;
    public Slider chargeSlider;

    private float pressStartTime;
    private float chargePower;
    private bool isCharging;

    public float ChargePower => chargePower;

    private void OnEnable()
    {
        StartCharge(); // 활성화될 때 차징 시작
    }

    private void OnDisable()
    {
        StopCharge();  // 비활성화될 때 차징 초기화
    }

    private void Update()
    {
        if (!isCharging) return;
        float t = Mathf.PingPong((Time.time - pressStartTime) / (chargePeriod / 2f), 1f);
        chargePower = t * chargeMax;
        if (chargeSlider != null) chargeSlider.value = chargePower / chargeMax;
    }

    public void StartCharge()
    {
        isCharging = true;
        pressStartTime = Time.time;
        chargePower = 0f;
        if (chargeSlider != null) chargeSlider.value = 0f;
    }

    public void StopCharge()
    {
        isCharging = false;
        chargePower = 0f;
        if (chargeSlider != null) chargeSlider.value = 0f;
    }
}
