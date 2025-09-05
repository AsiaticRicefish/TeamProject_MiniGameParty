using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using UnityEngine.AddressableAssets;

public class TowerProxyLoader : MonoBehaviour, IPunInstantiateMagicCallback
{
    [SerializeField] private AssetReferenceGameObject towerRef;

    private GameObject _real;                 // 실타워 인스턴스
    public int OwnerActorNumber { get; private set; }
    public string OwnerUid { get; private set; }
    public int Slot { get; private set; }

    private bool _registered;                 // 중복 등록 방지 플래그
    private bool _destroyed;                  // 파괴 플래그 (비동기 중단용)

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        var data = info.photonView?.InstantiationData;
        OwnerActorNumber = (data != null && data.Length > 0) ? (int)data[0] : -1;

        OwnerUid = (data != null && data.Length > 1) ? (string)data[1] : null;
        Slot = (data != null && data.Length > 2) ? (int)data[2] : 0;

        name = $"TowerProxy_{OwnerActorNumber}";
        StartCoroutine(CoLoadAndRegister());
    }

    private IEnumerator CoLoadAndRegister()
    {
        if (towerRef == null || !towerRef.RuntimeKeyIsValid())
        {
            yield break;
        }

        var handle = towerRef.InstantiateAsync(transform.position, transform.rotation, transform);
        yield return handle;

        if (_destroyed) yield break; // 파괴되면 중단
        if (!handle.IsValid() || handle.Result == null)
        {
            yield break;
        }

        _real = handle.Result;
        _real.transform.localPosition = Vector3.zero;
        _real.transform.localRotation = Quaternion.identity;

        var tower = _real.GetComponent<JengaTower>();
        if (tower == null) tower = _real.AddComponent<JengaTower>();

        tower.InitializeOwner(OwnerActorNumber, OwnerUid);
        tower.InitializeFromExistingHierarchy();

        int guard = 0;
        while (JengaTowerManager.Instance == null && guard < 300)
        {
            if (_destroyed) yield break;
            guard++;
            yield return null;
        }

        if (JengaTowerManager.Instance == null)
        {
            yield break;
        }

        if (!_registered && JengaTowerManager.Instance.GetPlayerTower(OwnerActorNumber) == null)
        {
            JengaTowerManager.Instance.RegisterTower(OwnerActorNumber, tower, Slot);
            _registered = true;
        }
    }

    private void OnDestroy()
    {
        _destroyed = true;
        if (_real != null)
        {
            Addressables.ReleaseInstance(_real);
            _real = null;
        }
    }
}