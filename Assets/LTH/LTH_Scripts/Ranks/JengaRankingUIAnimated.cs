using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

/// <summary>
/// 젠가 랭킹 패널 컨트롤러 (DOTween 전용)
/// - 게임 종료: Show(finalRanks)
/// - 실시간 갱신: OpenForLive() + UpdateLiveRanks(ranks)
/// - Addressables로 Row 프리팹 인스턴스화
/// - VerticalLayout 없이 직접 위치 제어
/// </summary>
[DisallowMultipleComponent]
public class JengaRankingUIAnimated : MonoBehaviour
{
    [Header("Root & Prefabs")]
    [SerializeField] private GameObject root;             // 랭킹 패널 루트
    [SerializeField] private RectTransform content;

    [Header("Addressables")]
    [Tooltip("Addressables에 등록한 RankingRow 프리팹의 Address(키)")]
    [SerializeField] private string rowAddress = "RowPrefab";

    [Header("Layout (Unity 레이아웃 사용)")]
    [SerializeField] private float rowHeight = 58f;
    [SerializeField] private float spacing = 8f;
    [SerializeField] private int paddingLeft = 8, paddingRight = 8, paddingTop = 8, paddingBottom = 8;

    [Header("Animation")]
    [SerializeField] private float moveDuration = 0.45f;
    [SerializeField] private bool emphasizeFirst = true;
    [SerializeField] private bool fadeInRoot = true;
    [SerializeField] private float panelFadeIn = 0.25f;

    [Header("Live Update")]
    [Tooltip("실시간 순위 반영 최소 간격(초)")]
    [SerializeField] private float minUpdateInterval = 0.08f;

    private readonly Dictionary<string, RankingRow> _rows = new();
    private readonly Dictionary<string, AsyncOperationHandle<GameObject>> _rowHandles = new();
    private readonly Dictionary<string, int> _lastRanks = new();

    // 이동 트윈 관리 & 동시 재정렬 방어
    private readonly Dictionary<RectTransform, Tween> _moveTweens = new();
    private bool _reordering = false;
    private bool _pendingAfterReorder = false;

    // 라이브 업데이트 디바운싱/큐잉
    private float _lastUpdateTime = -999f;
    private bool _updateQueued = false;
    private Dictionary<string, int> _pendingRanks;

    // 화면 상태
    private CanvasGroup _rootCg;
    private Tween _rootFadeTween;
    private bool _liveOpened = false;
    private bool _finalShown = false;

    // 레이아웃 컴포넌트
    private VerticalLayoutGroup _vlg;
    private ContentSizeFitter _csf;

    // 중복 인스턴스 생성 방지
    private readonly HashSet<string> _creating = new();

    private void Awake()
    {
        if (!root) Debug.LogWarning("[JengaRankingUIAnimated] Root not set.");
        if (root)
        {
            _rootCg = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();
            root.SetActive(false);
        }

        if (content)
        {
            var rt = content;
            rt.pivot = new Vector2(0f, 1f);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.anchoredPosition = Vector2.zero;

            // 레이아웃 세팅(없으면 부착)
            _vlg = content.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
            _csf = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();

            _vlg.childControlWidth = true;
            _vlg.childControlHeight = true;
            _vlg.childForceExpandWidth = true;
            _vlg.childForceExpandHeight = false;
            _vlg.spacing = spacing;
            _vlg.padding = new RectOffset(paddingLeft, paddingRight, paddingTop, paddingBottom);
            _vlg.childAlignment = TextAnchor.UpperCenter;

            _csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            _csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    // ======================= 최종 결과 표시 =======================
    public void Show(Dictionary<string, int> uidToRank)
    {
        _finalShown = true;
        _liveOpened = false;
        _updateQueued = false;
        StartCoroutine(ShowRoutine(uidToRank));
    }

    private IEnumerator ShowRoutine(Dictionary<string, int> uidToRank)
    {
        OpenPanel();
        var ordered = uidToRank.OrderBy(kv => kv.Value).ToList();
        yield return EnsureRowsRoutine(ordered);
        SmoothReorderByLayout(CollectRowsInOrder(ordered), moveDuration);

        if (emphasizeFirst && ordered.Count > 0 && _rows.TryGetValue(ordered[0].Key, out var firstRow))
            firstRow.EmphasizeFirstPlace();

        SnapshotRanks(uidToRank);
    }

    public void Hide() => ClosePanel();

    public void ClearRows()
    {
        foreach (var kv in _rowHandles)
            if (kv.Value.IsValid()) Addressables.ReleaseInstance(kv.Value);
        _rowHandles.Clear();
        _rows.Clear();
        _lastRanks.Clear();
        _moveTweens.Clear();
        _creating.Clear();
    }

    // ======================= 실시간 갱신 =======================
    public void OpenForLive()
    {
        if (_liveOpened) return;
        _liveOpened = true;
        OpenPanel();
    }

    public void UpdateLiveRanks(Dictionary<string, int> uidToRank)
    {
        if (!root || !content) return;
        if (!_liveOpened) OpenForLive();

        // 재정렬 중이면 마지막 것만 보관하고 리턴
        if (_reordering)
        {
            _pendingRanks = uidToRank;
            _pendingAfterReorder = true;
            return;
        }

        _pendingRanks = uidToRank;
        float elapsed = Time.unscaledTime - _lastUpdateTime;
        if (elapsed >= minUpdateInterval)
        {
            _lastUpdateTime = Time.unscaledTime;
            StartCoroutine(UpdateLiveRoutine(_pendingRanks));
            _updateQueued = false;
        }
        else if (!_updateQueued)
        {
            _updateQueued = true;
            StartCoroutine(DelayedLiveUpdate());
        }
    }

    private IEnumerator DelayedLiveUpdate()
    {
        float wait = Mathf.Max(0f, minUpdateInterval - (Time.unscaledTime - _lastUpdateTime));
        yield return new WaitForSeconds(wait);
        _lastUpdateTime = Time.unscaledTime;
        if (_pendingRanks != null)
            StartCoroutine(UpdateLiveRoutine(_pendingRanks));
        _updateQueued = false;
    }

    private IEnumerator UpdateLiveRoutine(Dictionary<string, int> newRanks)
    {
        var ordered = newRanks.OrderBy(kv => kv.Value).ToList();

        // 새/빠진 행 처리 + 텍스트 갱신
        yield return EnsureRowsRoutine(ordered);

        // FX & 숫자 플립
        foreach (var kv in ordered)
        {
            var uid = kv.Key; int rank = kv.Value;
            if (!_rows.TryGetValue(uid, out var row)) continue;

            int oldRank = _lastRanks.TryGetValue(uid, out var r) ? r : int.MaxValue;
            int delta = oldRank - rank;

            row.SetName(ResolveNickname(uid));
            if (delta != 0) row.SetRankAnimated(rank);
            else row.SetContent(rank, row.CurrentNickname);

            if (delta != 0)
            {
                row.ShowDeltaIcon(delta);
                row.PlayDeltaFx(delta, rank);
            }
        }

        // 레이아웃 기반으로 부드럽게 재배치
        SmoothReorderByLayout(CollectRowsInOrder(ordered), moveDuration);

        SnapshotRanks(newRanks);
    }


    // ======================= 행 생성/정리 =======================
    private IEnumerator EnsureRowsRoutine(List<KeyValuePair<string, int>> ordered)
    {
        if (content == null)
        {
            Debug.LogError("[RankingUI] 'content' is NULL. Inspector에서 Content(RectTransform)를 연결하세요.");
            yield break;
        }

        foreach (var kv in ordered)
        {
            var uid = kv.Key;
            if (_rows.ContainsKey(uid) || _creating.Contains(uid)) continue;

            _creating.Add(uid);
            var handle = Addressables.InstantiateAsync(rowAddress, content);
            yield return handle;
            _creating.Remove(uid);

            if (handle.Status != AsyncOperationStatus.Succeeded || !handle.Result)
            {
                Debug.LogError($"[JengaRankingUIAnimated] Addressables instantiate fail: {rowAddress}");
                continue;
            }

            var go = handle.Result;
            go.name = $"RankingRow_{uid}";

            var row = go.GetComponent<RankingRow>();
            var rt = (RectTransform)go.transform;

            if (!row)
            {
                Debug.LogError("[JengaRankingUIAnimated] RankingRow component not found on prefab.");
                Addressables.ReleaseInstance(handle);
                continue;
            }

            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.flexibleHeight = 0f;

            // 초기 내용
            row.SetContent(kv.Value, ResolveNickname(uid));

            _rows[uid] = row;
            _rowHandles[uid] = handle;

            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        // 빠진 UID 제거
        var toRemove = _rows.Keys.Where(uid => !ordered.Any(p => p.Key == uid)).ToList();
        foreach (var uid in toRemove)
        {
            if (_rowHandles.TryGetValue(uid, out var h) && h.IsValid())
                Addressables.ReleaseInstance(h);
            _rowHandles.Remove(uid);
            _rows.Remove(uid);
        }

        // 레이아웃 한 번 강제 갱신
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    // 원하는 순서대로 siblingIndex 세팅 후, DOTween으로 부드럽게 따라가게 만드는 핵심 함수
    private void SmoothReorderByLayout(List<RankingRow> rowsInOrder, float duration)
    {
        // 한 번에 하나만 재정렬
        if (_reordering)
        {
            // 1) 스냅샷으로 안전하게 복사
            var prevTweens = _moveTweens.Values.ToArray();

            // 2) 콜백(특히 OnComplete) 실행 없이 종료
            foreach (var tw in prevTweens)
            {
                if (tw != null && tw.IsActive()) tw.Kill(complete: false);
            }

            // 3) 맵 정리
            _moveTweens.Clear();
        }
        _reordering = true;

        // 1) 현재 위치 스냅샷
        var oldPos = new Dictionary<RectTransform, Vector2>(rowsInOrder.Count);
        foreach (var row in rowsInOrder)
            oldPos[(RectTransform)row.transform] = ((RectTransform)row.transform).anchoredPosition;

        // 2) 타겟 순서로 siblingIndex 적용
        for (int i = 0; i < rowsInOrder.Count; i++)
            rowsInOrder[i].transform.SetSiblingIndex(i);

        // 3) 레이아웃 강제 계산 → 타겟 위치 스냅샷
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        var targetPos = new Dictionary<RectTransform, Vector2>(rowsInOrder.Count);
        foreach (var row in rowsInOrder)
            targetPos[(RectTransform)row.transform] = ((RectTransform)row.transform).anchoredPosition;

        int stillMoving = rowsInOrder.Count;

        // 4) 각 행을 이전 위치로 되돌리고 ignoreLayout 켜서 DOTween 이동
        foreach (var row in rowsInOrder)
        {
            var rt = (RectTransform)row.transform;
            var le = row.GetComponent<LayoutElement>() ?? row.gameObject.AddComponent<LayoutElement>();

            rt.anchoredPosition = oldPos[rt];
            le.ignoreLayout = true;

            if (_moveTweens.TryGetValue(rt, out var oldTw) && oldTw.IsActive()) oldTw.Kill();

            var tw = rt.DOAnchorPos(targetPos[rt], duration).SetEase(Ease.OutCubic).OnComplete(() =>
            {
                le.ignoreLayout = false;
                _moveTweens.Remove(rt);

                if (--stillMoving == 0)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                    _reordering = false;

                    // 재정렬 도중 들어온 최신 랭킹이 있으면 한 번만 반영
                    if (_pendingAfterReorder && _pendingRanks != null)
                    {
                        _pendingAfterReorder = false;
                        StartCoroutine(UpdateLiveRoutine(_pendingRanks));
                    }
                }
            });

            _moveTweens[rt] = tw;
        }
    }

    private List<RankingRow> CollectRowsInOrder(List<KeyValuePair<string, int>> ordered)
    {
        var list = new List<RankingRow>(ordered.Count);
        foreach (var kv in ordered)
            if (_rows.TryGetValue(kv.Key, out var row) && row) list.Add(row);
        return list;
    }

    private void SnapshotRanks(Dictionary<string, int> ranks)
    {
        _lastRanks.Clear();
        foreach (var kv in ranks) _lastRanks[kv.Key] = kv.Value;
    }

    // ======================= 패널 열고 닫기 =======================
    private void OpenPanel()
    {
        if (!root) return;
        root.SetActive(true);
        if (!_rootCg) return;

        _rootFadeTween?.Kill();
        if (fadeInRoot)
        {
            _rootCg.alpha = 0f;
            _rootFadeTween = _rootCg.DOFade(1f, Mathf.Max(0.01f, panelFadeIn));
        }
        else _rootCg.alpha = 1f;
    }

    private void ClosePanel()
    {
        if (!root) return;
        _rootFadeTween?.Kill();
        if (_rootCg) _rootCg.alpha = 0f;
        root.SetActive(false);
    }

    // ======================= 유틸 =======================
    private string ResolveNickname(string uid)
    {
        var gp = PlayerManager.Instance?.GetPlayer(uid);
        if (gp != null && !string.IsNullOrEmpty(gp.Nickname)) return gp.Nickname;

        Player p = PhotonNetwork.PlayerList.FirstOrDefault(pp =>
            pp.CustomProperties != null &&
            pp.CustomProperties.TryGetValue("uid", out var v) && v as string == uid);

        if (p != null && !string.IsNullOrEmpty(p.NickName)) return p.NickName;
        return $"Player({(uid?.Substring(0, Mathf.Min(6, uid.Length)) ?? "???")})";
    }

    private void OnDestroy() => ClearRows();
}