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

    private Transform _rotatePivot;

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

        // Addressable 실타워 생성
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

        // JengaTower 세팅
        var tower = _real.GetComponent<JengaTower>();
        if (tower == null) tower = _real.AddComponent<JengaTower>();

        tower.InitializeOwner(OwnerActorNumber, OwnerUid);
        tower.InitializeFromExistingHierarchy();

        // 회전용 피벗 구성(가로 중심에 빈 오브젝트 만들고 그걸 회전)
        _rotatePivot = EnsureRotatePivotAtCenter(_real.transform);

        if (OwnerActorNumber == Photon.Pun.PhotonNetwork.LocalPlayer.ActorNumber)
        {
            var rot = _rotatePivot.GetComponent<JengaRotateController>();
            if (rot == null) rot = _rotatePivot.gameObject.AddComponent<JengaRotateController>();
        }

        // 매니저 준비 대기 후 등록
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

    // 타워의 렌더러 바운즈로 가로 중심(XZ)을 계산해 피벗 생성
    private Transform EnsureRotatePivotAtCenter(Transform realRoot)
    {
        if (realRoot.parent != null && realRoot.parent.name == "TowerRotatePivot")
            return realRoot.parent;

        var rends = realRoot.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return realRoot;

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        // 가로 중심(XZ)만 사용(Y는 그대로)
        Vector3 pivotPos = new Vector3(b.center.x, realRoot.position.y, b.center.z);

        var pivotGO = new GameObject("TowerRotatePivot");
        pivotGO.transform.SetParent(realRoot.parent, worldPositionStays: true);
        pivotGO.transform.position = pivotPos;
        pivotGO.transform.rotation = realRoot.rotation;
        pivotGO.transform.localScale = Vector3.one;

        realRoot.SetParent(pivotGO.transform, worldPositionStays: true);

        return pivotGO.transform;
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