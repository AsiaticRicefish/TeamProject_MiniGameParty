using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

[DefaultExecutionOrder(-10000)]
public class JengaRoomBootstrap : MonoBehaviourPunCallbacks
{
    private const string PREFAB_PATH = "Prefabs/Jenga/RoomSingletons/JengaManagers"; // Resources 경로
    private const string ROOMKEY_READY = "JG_MANAGERS_READY";

    private void OnEnable()
    {
        StartCoroutine(CoEnsureAfterOneFrame()); // 씬 초기화 직후 한 프레임 쉬고 보장
    }

    private IEnumerator CoEnsureAfterOneFrame()
    {
        yield return null;
        TrySpawnOrWait();
    }

    public override void OnJoinedRoom()
    {
        TrySpawnOrWait();
    }

    public override void OnRoomPropertiesUpdate(Hashtable changedProps)
    {
        // 비마스터가 플래그를 감지했는데 아직 인스턴스가 없다면 다시 시도
        if (changedProps != null && changedProps.ContainsKey(ROOMKEY_READY))
            TrySpawnOrWait();
    }

    public override void OnMasterClientSwitched(Player newMaster)
    {
        // 새 마스터가 되었고 매니저가 없다면 즉시 생성 시도
        if (JengaNetworkManager.Instance == null) TrySpawnOrWait();
    }

    private void TrySpawnOrWait()
    {
        StopAllCoroutines();
        StartCoroutine(EnsureManagersCo());
    }

    private IEnumerator EnsureManagersCo()
    {
        // 한 프레임 양보: 동기화 중복 방지
        yield return null;

        if (JengaNetworkManager.Instance != null)
            yield break;

        if (PhotonNetwork.IsMasterClient)
        {
            var go = PhotonNetwork.InstantiateRoomObject(PREFAB_PATH, Vector3.zero, Quaternion.identity);

            // 생성된 오브젝트를 현재 활성 씬(미니 씬)으로 강제 이동 → Additive 언로드 시 자동 파괴
            SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());

            var h = new Hashtable { [ROOMKEY_READY] = true };
            PhotonNetwork.CurrentRoom.SetCustomProperties(h);

            // 로컬에서도 인스턴스가 준비될 때까지 대기
            yield return new WaitUntil(() => JengaNetworkManager.Instance != null);
        }
        else
        {
            // 비마스터: 인스턴스가 생길 때까지 대기
            yield return new WaitUntil(() => JengaNetworkManager.Instance != null);
        }
    }
}