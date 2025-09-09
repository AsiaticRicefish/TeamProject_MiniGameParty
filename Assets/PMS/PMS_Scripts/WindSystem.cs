using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using DesignPattern;

public class WindSystem : PunSingleton<WindSystem>
{
    [Header("현재 바람 상태")]
    public WindData currentWind = new WindData(Vector3.zero, 0);

    [Header("설정값")]
    [SerializeField] private int minWindSpeed = 0;
    [SerializeField] private int maxWindSpeed = 4;

    /// <summary>
    /// 바람 방향과 풍속을 무작위로 갱신
    /// </summary>
    public void UpdateWind()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // 방향 뽑기
        int randomDir = Random.Range(0, 4);
        WindDirection dir = (WindDirection)randomDir;

        // 속도 뽑기
        int speed = Random.Range(minWindSpeed, maxWindSpeed);

        // 구조체 갱신
        currentWind = new WindData(DirectionEnumToVector(dir), speed);

        Debug.Log($"바람 변경 → 방향: {dir}, 속도: {speed:F1}");

        // 모든 클라이언트에 동기화
        photonView.RPC("RPC_UpdateWind", RpcTarget.Others, dir, speed);
    }

    [PunRPC]
    private void RPC_UpdateWind(WindDirection dir, int speed)
    {
        currentWind = new WindData(DirectionEnumToVector(dir), speed);
    }

    /// <summary>
    /// enum → Vector 변환
    /// </summary>
    private Vector3 DirectionEnumToVector(WindDirection dir)
    {
        switch (dir)
        {
            case WindDirection.Up: return new Vector3(0, 0, 1);
            case WindDirection.Down: return new Vector3(0, 0, -1);
            case WindDirection.Left: return new Vector3(-1, 0, 0);
            case WindDirection.Right: return new Vector3(1, 0, 0);
            default: return Vector3.zero;
        }
    }

    /// <summary>
    /// 현재 바람 데이터 반환
    /// </summary>
    public WindData GetWind()
    {
        return currentWind;
    }
}
