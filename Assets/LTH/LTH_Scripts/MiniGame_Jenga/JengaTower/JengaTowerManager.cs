using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DesignPattern;
using InputBlocker;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class JengaTowerManager : CombinedSingleton<JengaTowerManager>, IGameComponent, IInRoomCallbacks
{
    public enum BuildMode { Procedural, Prefab }

    [Header("타워 모드")]
    [SerializeField] private BuildMode buildMode = BuildMode.Procedural;

    [Header("프리팹 모드")]
    [SerializeField] private GameObject towerPrefab; // ← 완성된 타워 프리팹(자식들에 JengaBlock 붙어 있음)

    #region 타워 직접 생성 (사용 안함)
    [Header("타워 설정")]
    [SerializeField] private GameObject blockPrefab;
    [SerializeField] private int towerHeight = 13;
    #endregion

    [Header("배치(아레나 앵커)")]
    [Tooltip("개별 아레나의 TowerAnchor들을 등록 (Arena_i/TowerAnchor)")]
    [SerializeField] private List<Transform> arenaTowerAnchors = new(); // 인스펙터에 드래그 등록

    [Tooltip("Instantiate 시 타워를 앵커 하위로 붙일지 여부")]
    [SerializeField] private bool parentTowerUnderAnchor = true;

    [Header("카메라 앵커")]
    [SerializeField] private List<Transform> arenaCameraAnchors = new();

    [SerializeField] private string[] arenaLayerNames = { "Arena_0", "Arena_1", "Arena_2", "Arena_3" };

    [SerializeField] private Transform towersParent;

    private readonly Dictionary<int, JengaTower> _playerTowers = new(); // ActorNumber → Tower

    // 룸 커스텀 프로퍼티 키 (관전자/재접속 대비 슬롯 고정)
    private const string ROOMKEY_SLOTS = "JG_SLOTS";
    private const string PROXY_PREFAB_PATH = "Prefabs/Jenga/Proxy/TowerProxyLoader";

    // 네트워크 적용 중 이벤트 재브로드캐스트 방지 플래그
    private bool _suppressCollapseBroadcast;

    // 붕괴 연출 동안 전역 입력 올-락/해제
    private InputLockToken _collapseLock;
    private Coroutine _collapseReleaseCo;
    private int _collapseNesting = 0;

    #region JengaFace Layer 캐싱
    private static int _jengaFaceLayer = int.MinValue;
    private static int JengaFaceLayer
    {
        get
        {
            if (_jengaFaceLayer == int.MinValue)
                _jengaFaceLayer = LayerMask.NameToLayer("JengaFace");
            return _jengaFaceLayer;
        }
    }
    #endregion

    // 개별 타워별로 구독한 델리게이트를 보관(해제용)
    private readonly HashSet<int> _mutedActors = new();
    private readonly Dictionary<int, (Action on, Action off)> _towerMuteHandlers = new();

    public bool IsArenaMuted(int ownerActorNumber) => _mutedActors.Contains(ownerActorNumber);

    private bool _isCreatingProxies = false;

    public void WithSuppressedCollapse(Action action)
    {
        _suppressCollapseBroadcast = true;
        action?.Invoke();
        _suppressCollapseBroadcast = false;
    }

    public bool IsSuppressingCollapse => _suppressCollapseBroadcast;


    protected override void OnAwake()
    {
        base.isPersistent = false;

        // 부모 트랜스폼이 비어 있으면 안전하게 하나 만들어 둔다
        if (towersParent == null)
        {
            var go = new GameObject("JengaTowersRoot");
            towersParent = go.transform;
        }
    }

    public void Initialize()
    {
        if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(ForceCleanupAndReinitialize());
            return;
        }

        EnsureSlotMap();

        _playerTowers.Clear();

        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(WaitOneFrameThenEnsure());
        }
    }

    private IEnumerator ForceCleanupAndReinitialize()
    {
        if (_isCreatingProxies)
        {
            yield break;
        }

        _isCreatingProxies = true;

        // 1. PhotonView 기반으로 더 확실한 정리
        var allPhotonViews = FindObjectsOfType<PhotonView>()
            .Where(pv => pv.GetComponent<TowerProxyLoader>() != null)
            .ToArray();

        foreach (var pv in allPhotonViews)
        {
            var proxy = pv.GetComponent<TowerProxyLoader>();
            PhotonNetwork.Destroy(pv.gameObject);
        }

        // 2. 더 긴 대기 (네트워크 전파 시간 고려)
        yield return new WaitForSeconds(2.0f);

        // 3. 여러 방식으로 재확인
        var remainingProxies = FindObjectsOfType<TowerProxyLoader>(true);
        var remainingPhotonViews = FindObjectsOfType<PhotonView>()
            .Where(pv => pv.GetComponent<TowerProxyLoader>() != null)
            .ToArray();

        // 4. 아직 남아있다면 추가 정리 시도
        if (remainingPhotonViews.Length > 0)
        {
            foreach (var pv in remainingPhotonViews)
            {
                PhotonNetwork.Destroy(pv.gameObject);
            }
            yield return new WaitForSeconds(1.0f);
        }

        // 5. 슬롯맵 재설정 전 현재 방 상태 확인
        var currentPlayers = PhotonNetwork.PlayerList.Select(p => p.ActorNumber).OrderBy(x => x).ToArray();

        EnsureSlotMap();
        _playerTowers.Clear();

        // 6. ViewID Pool 정리를 위한 충분한 대기
        yield return new WaitForSeconds(1.5f);

        // 7. 최종 확인 후 생성
        var finalCheck = FindObjectsOfType<TowerProxyLoader>(true);

        EnsureProxiesForCurrentPlayers();
        _isCreatingProxies = false;
    }

    public void CleanupAllProxies()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        var existing = UnityEngine.Object.FindObjectsOfType<TowerProxyLoader>(true);

        foreach (var proxy in existing)
        {
            PhotonNetwork.Destroy(proxy.gameObject);
        }
    }

    private IEnumerator WaitOneFrameThenEnsure()
    {
        if (_isCreatingProxies)
        {
            yield break;
        }

        _isCreatingProxies = true;
        yield return null;
        EnsureProxiesForCurrentPlayers();
        _isCreatingProxies = false;
    }

    private void OnEnable()
    {
        if (JengaGameManager.Instance != null)
            JengaGameManager.Instance.OnGameStateChanged += HandleStateChanged;

        PhotonNetwork.AddCallbackTarget(this);
    }

    private void HandleStateChanged(JengaGameState state)
    {
        if (state == JengaGameState.Finished)
            ReleaseCollapseLockNow();
    }

    public void RegisterTower(int actorNumber, JengaTower tower, int slotIndex)
    {
        if (tower == null) return;

        _playerTowers[actorNumber] = tower;

        ApplyArenaLayer(tower.gameObject, slotIndex);
        tower.ConfigureTopProtection(allowTopRemoval: false, topSafeLayers: 1);

        // 붕괴 이벤트 → 네트워크 통지(마스터만)
        // (중복 구독 방지 위해 기존 핸들러 있으면 해제)
        if (_towerMuteHandlers.TryGetValue(actorNumber, out var prev))
        {
            tower.CollapseStarted -= prev.on;
            tower.CollapseFinished -= prev.off;
        }

        tower.OnTowerCollapsed += () =>
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (IsSuppressingCollapse) return;
            JengaNetworkManager.Instance.RequestTowerCollapse_MasterAuth(actorNumber);
        };

        Action on = () => { 
            MuteArena(actorNumber, true); 
            SetTowerInputEnabled(actorNumber, false);
            if (actorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
            {
                JengaUIManager.Instance.HideRotateButton();
            }
        };

        Action off = () => { 
            MuteArena(actorNumber, false); 
            SetTowerInputEnabled(actorNumber, true);

            // 로컬 플레이어가 무너졌다면 대기 UI 표시
            if (actorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
            {
                JengaUIManager.Instance?.ShowWaiting();
            }
        };
        tower.CollapseStarted += on;
        tower.CollapseFinished += off;
        _towerMuteHandlers[actorNumber] = (on, off);

        if (actorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
        {
            var cameraAnchor = GetCameraAnchor(slotIndex);
            var binder = FindFirstObjectByType<JengaLocalCameraBinder>(FindObjectsInactive.Include);
            if (binder)
            {
                binder.BindForLocal(actorNumber, slotIndex, cameraAnchor, tower.transform, arenaLayerNames);

                var overlay = FindFirstObjectByType<TowerFocusOverlay>(FindObjectsInactive.Include);
                if (overlay)
                {
                    var mask = GetArenaLayerMaskBySlot(slotIndex);
                    overlay.SetArenaMask(mask);

                    overlay.Bind(tower, null);

                    if (overlay.TowerCam != null)
                    {
                        overlay.TowerCam.Render();
                    }
                }
            }
        }
    }


    #region Camera Anchor 찾기
    private Transform GetCameraAnchor(int slotIndex)
    {
        // 병렬 리스트가 있으면 우선 사용
        if (arenaCameraAnchors != null && arenaCameraAnchors.Count > 0)
        {
            var idx = Mathf.Abs(slotIndex) % arenaCameraAnchors.Count;
            var t = arenaCameraAnchors[idx];
            if (t) return t;
        }

        // 2) TowerAnchor의 자식에서 "CameraAnchor" 이름으로 탐색
        var towerAnchor = GetPlayerTowerAnchor(slotIndex);
        if (towerAnchor)
        {
            var child = towerAnchor.Find("CameraAnchor");
            if (child) return child;

            child = FindDeepChild(towerAnchor, "CameraAnchor");
            if (child) return child;
        }

        return towerAnchor ? towerAnchor : towersParent;
    }

    private Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform c in parent)
        {
            if (c.name == name) return c;
            var r = FindDeepChild(c, name);
            if (r) return r;
        }
        return null;
    }

    #endregion

    #region Tower Proxy Management
    private void EnsureProxiesForCurrentPlayers()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        var slotMap = GetSlotMap(); // actorNumbers in slot order
        var aliveActors = new HashSet<int>(slotMap);

        var existing = UnityEngine.Object.FindObjectsOfType<TowerProxyLoader>(true);

        var byOwner = new Dictionary<int, List<TowerProxyLoader>>();
        foreach (var p in existing)
        {
            var key = p.OwnerActorNumber;
            if (!byOwner.TryGetValue(key, out var list))
            {
                list = new List<TowerProxyLoader>();
                byOwner[key] = list;
            }
            list.Add(p);
        }

        foreach (var kv in byOwner)
        {
            int actor = kv.Key;
            var list = kv.Value;

            if (actor != -1 && !aliveActors.Contains(actor))
            {
                foreach (var proxy in list)
                    PhotonNetwork.Destroy(proxy.gameObject);
            }
        }

        if (byOwner.TryGetValue(-1, out var zombieList))
        {
            foreach (var proxy in zombieList)
                PhotonNetwork.Destroy(proxy.gameObject);
        }

        foreach (var kv in byOwner)
        {
            int actor = kv.Key;
            var list = kv.Value;

            if (actor == -1) continue;
            if (!aliveActors.Contains(actor)) continue;

            if (list.Count > 1)
            {
                list.Sort((a, b) =>
                {
                    var va = a.GetComponent<PhotonView>();
                    var vb = b.GetComponent<PhotonView>();
                    int ia = va ? va.ViewID : int.MaxValue;
                    int ib = vb ? vb.ViewID : int.MaxValue;
                    return ia.CompareTo(ib);
                });

                for (int i = 1; i < list.Count; i++)
                    PhotonNetwork.Destroy(list[i].gameObject);
            }
        }

        StartCoroutine(DelayedProxyCreation(slotMap));
    }

    private IEnumerator DelayedProxyCreation(int[] slotMap)
    {
        yield return new WaitForEndOfFrame(); // 기존 프록시 파괴 완료 대기

        // 현재 남아있는 프록시들 재확인
        var remaining = UnityEngine.Object.FindObjectsOfType<TowerProxyLoader>(true);
        var have = new HashSet<int>(remaining.Select(p => p.OwnerActorNumber).Where(a => a != -1));

        for (int slot = 0; slot < slotMap.Length; slot++)
        {
            int actor = slotMap[slot];
            if (have.Contains(actor)) continue;

            var anchor = GetPlayerTowerAnchor(slot);
            var pos = anchor ? anchor.position : Vector3.zero;
            var rot = anchor ? anchor.rotation : Quaternion.identity;

            PhotonNetwork.InstantiateRoomObject(
                PROXY_PREFAB_PATH,
                pos, rot, 0,
                new object[] { actor, GetOwnerUidByActorSafe(actor), slot }
            );
        }

        yield return null;

        remaining = UnityEngine.Object.FindObjectsOfType<TowerProxyLoader>(true);
        foreach (var proxy in remaining)
        {
            if (proxy.OwnerActorNumber == -1) continue; // 아직 초기화 중

            int slot = GetSlotIndexOf(proxy.OwnerActorNumber);
            var anchor = GetPlayerTowerAnchor(slot);

            if (parentTowerUnderAnchor && anchor && proxy.transform.parent != anchor)
                proxy.transform.SetParent(anchor, true);
            else if (!parentTowerUnderAnchor && towersParent && proxy.transform.parent != towersParent)
                proxy.transform.SetParent(towersParent, true);

            if (anchor)
            {
                proxy.transform.position = anchor.position;
                proxy.transform.rotation = anchor.rotation;
            }
        }
    }

    #endregion

    #region Slot Map (Room Properties)
    private void EnsureSlotMap()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        var room = PhotonNetwork.CurrentRoom;
        if (room == null) return;


        // 기존 슬롯맵이 있는지 확인하고, 현재 플레이어와 맞지 않으면 갱신
        bool needsUpdate = false;

        if (room.CustomProperties.TryGetValue(ROOMKEY_SLOTS, out var existingObj))
        {
            var existingSlots = ConvertToIntArray(existingObj);
            var currentActors = PhotonNetwork.PlayerList.Select(p => p.ActorNumber).OrderBy(x => x).ToArray();

            // 기존 슬롯과 현재 플레이어가 다르면 업데이트 필요
            if (!existingSlots.OrderBy(x => x).SequenceEqual(currentActors))
            {
                needsUpdate = true;
            }
        }
        else
        {
            needsUpdate = true;
        }

        if (needsUpdate)
        {
            var ordered = PhotonNetwork.PlayerList
                .OrderBy(p => p.ActorNumber)
                .Select(p => p.ActorNumber)
                .ToArray();

            var h = new ExitGames.Client.Photon.Hashtable { [ROOMKEY_SLOTS] = ordered };
            room.SetCustomProperties(h);
            Debug.Log($"[JengaTowerManager] Updated slot map: [{string.Join(",", ordered)}]");
        }
    }

    private int[] ConvertToIntArray(object obj)
    {
        if (obj is int[] arrInt) return arrInt;
        if (obj is object[] arrObj) return arrObj.Select(o => Convert.ToInt32(o)).ToArray();
        return new int[0];
    }

    private int[] GetSlotMap()
    {
        var room = PhotonNetwork.CurrentRoom;
        if (room != null && room.CustomProperties.TryGetValue(ROOMKEY_SLOTS, out var obj))
        {
            if (obj is int[] arrInt) return arrInt;
            if (obj is object[] arrObj) return arrObj.Select(o => Convert.ToInt32(o)).ToArray();
        }

        return PhotonNetwork.PlayerList
            .OrderBy(p => p.ActorNumber)
            .Select(p => p.ActorNumber)
            .ToArray();
    }

    /// <summary>
    /// ActorNumber → 슬롯 인덱스
    /// </summary>
    public int GetSlotIndexOf(int actorNumber)
    {
        var map = GetSlotMap();
        for (int i = 0; i < map.Length; i++)
            if (map[i] == actorNumber) return i;
        return 0;
    }
    #endregion

    #region Anchors & Utilities
    private Transform GetPlayerTowerAnchor(int slotIndex)
    {
        if (arenaTowerAnchors != null && arenaTowerAnchors.Count > 0)
        {
            var idx = Mathf.Abs(slotIndex) % arenaTowerAnchors.Count;
            return arenaTowerAnchors[idx];
        }
        return null;
    }

    // Photon Player → UID 보조 조회 유틸 (없으면 null)
    private string GetOwnerUidByActorSafe(int actorNumber)
    {
        var p = PhotonNetwork.PlayerList.FirstOrDefault(x => x.ActorNumber == actorNumber);
        if (p != null && p.CustomProperties != null && p.CustomProperties.TryGetValue("uid", out var uidObj))
            return uidObj as string;
        return null;
    }

    #endregion

    #region Public API
    public JengaTower GetPlayerTower(int actorNumber) =>
        _playerTowers.TryGetValue(actorNumber, out var t) ? t : null;

    public string GetOwnerUidByActor(int actorNumber) =>
    _playerTowers.TryGetValue(actorNumber, out var t) ? t.ownerUid : null;

    public void RemovePlayerBlock(int actorNumber, int blockId, bool withAnimation = true)
    {
        GetPlayerTower(actorNumber)?.RemoveBlock(blockId, withAnimation);
    }

    public Dictionary<int, bool> CheckAllTowersStability()
    {
        var dict = new Dictionary<int, bool>(_playerTowers.Count);
        foreach (var kv in _playerTowers)
            dict[kv.Key] = kv.Value.IsStable();
        return dict;
    }

    public Dictionary<int, IReadOnlyCollection<int>> SnapshotRemovedBlocks()
    {
        var snap = new Dictionary<int, IReadOnlyCollection<int>>(_playerTowers.Count);
        foreach (var kv in _playerTowers)
            snap[kv.Key] = kv.Value.GetRemovedBlockIds().ToArray();
        return snap;
    }

    public void ApplySnapshot(Dictionary<int, IReadOnlyCollection<int>> snapshot)
    {
        foreach (var kv in snapshot)
            GetPlayerTower(kv.Key)?.ApplyRemovedBlocks(kv.Value, withAnimation: false);
    }

    public LayerMask GetArenaLayerMaskBySlot(int slotIndex)
    {
        var name = arenaLayerNames[Mathf.Abs(slotIndex) % arenaLayerNames.Length];
        int layer = LayerMask.NameToLayer(name);
        return (layer >= 0) ? (1 << layer) : 0;
    }

    public LayerMask GetArenaLayerMaskByActor(int actorNumber)
    {
        int slot = GetSlotIndexOf(actorNumber);
        return GetArenaLayerMaskBySlot(slot);
    }

    #endregion

    #region collapse input lock
    private void HandleAnyCollapseStarted()
    {
        // 첫 진입에서만 락 획득
        if (_collapseNesting++ == 0)
            AcquireCollapseLockNow();
    }

    private void HandleAnyCollapseFinished()
    {
        if (_collapseNesting > 0 && --_collapseNesting == 0)
            ReleaseCollapseLockNow();
    }

    private void AcquireCollapseLockNow()
    {
        if (_collapseLock == null)
            _collapseLock = InputManager.Instance?.Acquire(InputType.All);
    }

    private void ReleaseCollapseLockNow()
    {
        _collapseLock?.Dispose();
        _collapseLock = null;
    }

    private void MuteArena(int ownerActorNumber, bool on)
    {
        if (on) _mutedActors.Add(ownerActorNumber);
        else _mutedActors.Remove(ownerActorNumber);
    }

    private void SetTowerInputEnabled(int ownerActorNumber, bool enabled)
    {
        var tower = GetPlayerTower(ownerActorNumber);
        if (tower == null) return;

        //foreach (var b in tower.allBlocks)
        //{
        //    if (b && b.TryGetComponent<Collider>(out var col))
        //        col.enabled = enabled;
        //}
    }

    #endregion

    private void ApplyArenaLayer(GameObject root, int slotIndex)
    {
        var name = arenaLayerNames[slotIndex % arenaLayerNames.Length];
        int layer = LayerMask.NameToLayer(name);

        if (layer < 0) return;

        SetLayerRecursively(root, layer);
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        // 1) 클릭 면은 보존
        if (go.layer == JengaFaceLayer || go.GetComponent<FaceHitProxy>() != null)
        {
            foreach (Transform c in go.transform)
                SetLayerRecursively(c.gameObject, JengaFaceLayer);
            return;
        }

        // 2) 일반 오브젝트만 아레나 레이어 적용
        go.layer = layer;

        foreach (Transform c in go.transform)
            SetLayerRecursively(c.gameObject, layer);
    }

    #region 플레이어가 탈주했을 때


    // 플레이어 퇴장
    public void OnPlayerLeftRoom(Player other)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // 정책 1: 이탈 시 타워 붕괴 처리
        JengaNetworkManager.Instance?.RequestTowerCollapse_MasterAuth(other.ActorNumber);

        // 해당 플레이어의 프록시도 명시적으로 제거
        var leaverProxies = FindObjectsOfType<TowerProxyLoader>()
            .Where(p => p.OwnerActorNumber == other.ActorNumber);

        foreach (var proxy in leaverProxies)
        {
            PhotonNetwork.Destroy(proxy.gameObject);
        }
    }

    public void OnMasterClientSwitched(Player newMaster)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        StartCoroutine(WaitOneFrameThenEnsure());

        var gm = JengaGameManager.Instance;
        if (gm && gm.currentState == JengaGameState.Playing)
        {
            JengaNetworkManager.Instance.BroadcastGameState(gm.currentState);
            JengaNetworkManager.Instance.BroadcastTimeSync(gm.remainingTime);
        }
    }

    // 플레이어 입장
    public void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        StartCoroutine(WaitOneFrameThenEnsure());
    }

    // 룸 프로퍼티 변경
    public void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged) { }

    // 플레이어 프로퍼티 변경 (uid 등)
    public void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps) { }

    #endregion

    #region 강제 정리 (플레이어 1명이라도 이탈 시 호출)
    protected override void OnDestroy()
    {
        Debug.Log("[JengaTowerManager] OnDestroy - cleaning up resources");

        // 게임 매니저 이벤트 해제
        if (JengaGameManager.Instance != null)
            JengaGameManager.Instance.OnGameStateChanged -= HandleStateChanged;

        // 타워별 이벤트 핸들러 정리
        foreach (var kv in _towerMuteHandlers)
        {
            var actor = kv.Key;
            var pair = kv.Value;
            var tower = GetPlayerTower(actor);
            if (tower != null)
            {
                if (pair.on != null) tower.CollapseStarted -= pair.on;
                if (pair.off != null) tower.CollapseFinished -= pair.off;
            }
        }
        _towerMuteHandlers.Clear();

        // 입력 락 해제 및 상태 정리
        ReleaseCollapseLockNow();
        _mutedActors.Clear();

        // Photon 콜백 해제
        PhotonNetwork.RemoveCallbackTarget(this);

        // 마스터라면 프록시 정리
        if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
        {
            CleanupAllProxies();
        }

        base.OnDestroy();
    }

    private void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    #endregion

}