using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class WindHelper
{
    public static void AddForceWithWind(Rigidbody rb, Vector3 baseForce, ForceMode mode = ForceMode.Impulse)
    {
        var windSystem = Object.FindObjectOfType<WindSystem>();
        if (windSystem == null)
        {
            Debug.LogError("WindSystem이 존재하지 않습니다");
            rb.AddForce(baseForce, mode); // 바람이 없으면 그냥 원래 힘만 적용
            return;
        }

        // WindData 가져오기
        WindData newData = windSystem.GetWind();

        // 최종 힘 = (원래 힘 * 방향) + (방향 * ((기존 힘 * 보정치(0.1~0.3))) -> 기존 벡터 + 벡터
        //0.1

        Vector3 windForce = newData.direction * (baseForce.magnitude * (float)newData.speed / 10);
        Debug.Log($"[WindHelper] - {baseForce.magnitude}");
        Debug.Log($"[WindHelper] - {baseForce.magnitude + windForce.magnitude}");
        rb.AddForce(baseForce + windForce, mode);
    }
}
