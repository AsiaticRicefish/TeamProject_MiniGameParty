using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;

#if DOTWEEN
using DG.Tweening;
#endif

using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 젠가 랭킹 패널 컨트롤러
/// - GameFinished 시 전달된 uid→rank를 받아 정렬/배치/애니메이션
/// - VerticalLayout 없이 직접 위치 제어
/// - DOTween 유무 모두 지원
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

    [Header("Layout")]
    [SerializeField] private float rowHeight = 58f;
    [SerializeField] private float spacing = 8f;
    [SerializeField] private float moveDuration = 0.45f;
    [Tooltip("처음 등장 시 아래쪽으로 얼마나 밀어둘지(행 수 기준)")]
    [SerializeField] private int introOffsetRows = 4;

    [Header("Panel FX")]
    [SerializeField] private bool fadeInRoot = true;
    [SerializeField] private float panelFadeIn = 0.25f;

    [Header("Options")]
    [SerializeField] private bool emphasizeFirst = true;
    [Tooltip("content의 앵커/피벗이 올바르지 않으면 런타임에 보정")]
    [SerializeField] private bool enforceContentAnchors = true;

    private readonly Dictionary<string, RankingRow> _rows = new();

    private readonly Dictionary<string, AsyncOperationHandle<GameObject>> _rowHandles = new();

    private CanvasGroup _rootCg;

#if DOTWEEN
    private Tween _rootFadeTween;
#endif

    private void Awake()
    {
        if (!root) Debug.LogWarning("[JengaRankingUIAnimated] Root not set.");
        if (root)
        {
            _rootCg = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();
            root.SetActive(false);
        }

        if (enforceContentAnchors && content)
        {
            content.pivot = new Vector2(0.5f, 1f);
            content.anchorMin = new Vector2(0.5f, 1f);
            content.anchorMax = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
        }
    }

    public void Show(Dictionary<string, int> uidToRank)
    {
        if (!root || !content)
        {
            Debug.LogWarning("[JengaRankingUIAnimated] Root/Content not assigned.");
            return;
        }
        StartCoroutine(ShowRoutine(uidToRank));
    }

    public void Hide() => ClosePanel();

    public void ClearRows()
    {
        foreach (var kv in _rowHandles)
        {
            if (kv.Value.IsValid())
                Addressables.ReleaseInstance(kv.Value);
        }
        _rowHandles.Clear();
        _rows.Clear();
    }

    private System.Collections.IEnumerator ShowRoutine(Dictionary<string, int> uidToRank)
    {
        OpenPanel();

        // 1 → N 정렬
        var ordered = uidToRank.OrderBy(kv => kv.Value).ToList();

        // 필요한 행 확보/생성 (Addressables)
        yield return EnsureRowsRoutine(ordered);

        // 배치/애니메이션
        AnimateToRanks(ordered);

        // 1등 강조
        if (emphasizeFirst && ordered.Count > 0)
        {
            var firstUid = ordered[0].Key;
            if (_rows.TryGetValue(firstUid, out var firstRow))
                firstRow.EmphasizeFirstPlace();
        }
    }

    private System.Collections.IEnumerator EnsureRowsRoutine(List<KeyValuePair<string, int>> ordered)
    {
        // 새로 필요한 uid만 Addressables로 인스턴스 생성
        foreach (var kv in ordered)
        {
            var uid = kv.Key;
            if (_rows.ContainsKey(uid)) continue;

            var handle = Addressables.InstantiateAsync(rowAddress, content);
            yield return handle;

            if (handle.Status != AsyncOperationStatus.Succeeded || !handle.Result)
            {
                Debug.LogError($"[JengaRankingUIAnimated] Failed to instantiate row from Address '{rowAddress}'.");
                continue;
            }

            var go = handle.Result;
            go.name = $"RankingRow_{uid}";

            var row = go.GetComponent<RankingRow>();
            if (!row)
            {
                Debug.LogError("[JengaRankingUIAnimated] RankingRow component not found on instantiated prefab root.");
                // 실패 시 인스턴스 해제
                Addressables.ReleaseInstance(handle);
                continue;
            }

            int lastIndex = _rows.Count;
            var startPos = GetAnchorForIndex(lastIndex + introOffsetRows);
            row.SetPositionInstant(startPos);

            string nickname = ResolveNickname(uid);
            row.SetContent(kv.Value, nickname);

            _rows[uid] = row;
            _rowHandles[uid] = handle;
        }

        var toRemove = _rows.Keys.Where(uid => !ordered.Any(p => p.Key == uid)).ToList();
        foreach (var uid in toRemove)
        {
            if (_rowHandles.TryGetValue(uid, out var h) && h.IsValid())
                Addressables.ReleaseInstance(h);
            _rowHandles.Remove(uid);
            _rows.Remove(uid);
        }
    }

    private void AnimateToRanks(List<KeyValuePair<string, int>> ordered)
    {
        for (int i = 0; i < ordered.Count; i++)
        {
            string uid = ordered[i].Key;
            int rank = ordered[i].Value;

            if (!_rows.TryGetValue(uid, out var row) || row == null) continue;

            string nickname = ResolveNickname(uid);
            row.SetContent(rank, nickname);

            Vector2 target = GetAnchorForIndex(i);
            row.AnimateTo(target, moveDuration);
        }

        float totalH = ordered.Count * rowHeight + Mathf.Max(0, ordered.Count - 1) * spacing;
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, totalH);
    }

    private Vector2 GetAnchorForIndex(int index)
    {
        float y = -(index * (rowHeight + spacing));
        return new Vector2(0f, y);
    }

    private void OpenPanel()
    {
        if (!root) return;

        root.SetActive(true);

        if (!_rootCg) return;
#if DOTWEEN
        _rootFadeTween?.Kill();
#endif
        if (fadeInRoot)
        {
#if DOTWEEN
            _rootCg.alpha = 0f;
            _rootFadeTween = _rootCg.DOFade(1f, Mathf.Max(0.01f, panelFadeIn));
#else
            StartCoroutine(FadeCanvasGroup(_rootCg, 0f, 1f, Mathf.Max(0.01f, panelFadeIn)));
#endif
        }
        else
        {
            _rootCg.alpha = 1f;
        }
    }

    private void ClosePanel()
    {
        if (!root) return;
#if DOTWEEN
        _rootFadeTween?.Kill();
#endif
        if (_rootCg) _rootCg.alpha = 0f;
        root.SetActive(false);
    }

    private string ResolveNickname(string uid)
    {
        var gp = PlayerManager.Instance?.GetPlayer(uid);
        if (gp != null && !string.IsNullOrEmpty(gp.Nickname))
            return gp.Nickname;

        Player p = PhotonNetwork.PlayerList.FirstOrDefault(pp =>
            pp.CustomProperties != null &&
            pp.CustomProperties.TryGetValue("uid", out var v) && v as string == uid);

        if (p != null && !string.IsNullOrEmpty(p.NickName))
            return p.NickName;

        return $"Player({(uid?.Substring(0, Mathf.Min(6, uid.Length)) ?? "???")})";
    }

#if !DOTWEEN
    private System.Collections.IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        cg.alpha = from;
        if (duration <= 0f) { cg.alpha = to; yield break; }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
            yield return null;
        }
        cg.alpha = to;
    }
#endif

    private void OnDestroy()
    {
        ClearRows();
    }
}