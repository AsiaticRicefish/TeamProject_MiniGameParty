using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;
using TMPro;


namespace YG
{
    public enum DistributionRule
    {
        UniqueRandom,   // 1..MaxValue 중 중복 없이 뽑음
        FixedPreset,    // 프리셋 배열 사용
        RandomAny       // 각 카드마다 랜덤 (중복 허용)
    }

    public class MeteorCardManager : MonoBehaviourPun
    {
        [Header("Layout / Prefabs")]
        [SerializeField] private RectTransform cardParent;
        [SerializeField] private GameObject cardItemPrefab;
        [SerializeField] private CanvasGroup cardUICanvas;     // 카드 UI 전체 ON/OFF

        [Header("Overlay / Reveal")]
        [SerializeField] private CanvasGroup blackout;         // 화면 어둡게(다른 UI 가리기)
        [SerializeField, Range(0, 5f)] private float revealDelay = 1.2f;   // 모두 선택 후 n초 뒤 일괄 공개
        [SerializeField, Range(0, 0.5f)] private float flipInterval = 0.08f; // 카드 하나씩 토닥 공개 연출(0이면 동시)

        [Header("Card Rules")]
        [SerializeField] private DistributionRule distributionRule = DistributionRule.UniqueRandom;
        [SerializeField] private int maxCardValue = 9;
        [SerializeField] private int[] presetValues = { 1, 2, 3, 4 }; // FixedPreset 모드
        [SerializeField] private bool ascendingOrder = true;

        [Header("Timeout")]
        [SerializeField] private float selectionTimeout = 15f;
        [SerializeField] private TMP_Text countdownText;
        [SerializeField] private TMP_Text bannerText;

        // runtime
        private bool _spawned;
        private readonly Dictionary<int, MeteorCardItem> _indexToItem = new();
        private readonly Dictionary<int, int> _actorToValue = new();
        private readonly HashSet<int> _lockedValues = new();

        private List<int> _values = new();
        private Coroutine _timeoutCo;
        
        private bool _localPickLocked = false; // 첫 클릭 직후 즉시 true

        private void OnEnable() => StartCoroutine(Co_WaitAndSpawn());
        
        private void Awake()
        {
            TryAutoWireFromScene();
        }

        private IEnumerator Co_WaitAndSpawn()
        {
            yield return new WaitUntil(() => PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InRoom);
            yield return null;

            if (_spawned) yield break;
            _spawned = true;

            BuildCardsForCurrentPlayers();
            OpenUI(true);
            SetBanner("카드를 선택하세요");

            if (selectionTimeout > 0f)
                _timeoutCo = StartCoroutine(Co_SelectionTimeout(selectionTimeout));
        }

        private void BuildCardsForCurrentPlayers()
        {
            ClearChildren(cardParent);

            int n = Mathf.Clamp(PhotonNetwork.CurrentRoom.PlayerCount, 1, 4);

            if (PhotonNetwork.IsMasterClient)
            {
                // 1..N 덱 생성 후 Fisher–Yates 셔플
                var deck = Enumerable.Range(1, n).ToList();
                for (int i = n - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    (deck[i], deck[j]) = (deck[j], deck[i]);
                }
                // 전원에게 같은 덱 동기화
                photonView.RPC(nameof(RPC_SetDeck), RpcTarget.AllBuffered, deck.ToArray());
            }
        }
        
        // 2) 덱을 수신하면 즉시 카드 스폰 (전원 동일)
        [PunRPC]
        private void RPC_SetDeck(int[] deck)
        {
            _values = deck.ToList();

            // 카드 생성 + 값 바인딩
            for (int i = 0; i < _values.Count; i++)
            {
                var go = Instantiate(cardItemPrefab, cardParent);
                var item = go.GetComponent<MeteorCardItem>();
                if (!item) continue;

                int value = _values[i];
                int index = i;
                item.Init(index, OnClickCard, value);
                _indexToItem[index] = item;
            }
        }


        /*private IEnumerator GenerateRandomValues(int n)
        {
            for (int i = 0; i < n; i++)
            {
                yield return new WaitForSeconds(2f);

                if (!PhotonNetwork.IsMasterClient) continue;

                int num = 0;

                while (_values.Contains(num))
                {
                    num = Random.Range(1, n + 1);
                }
                
                photonView.RPC(nameof(RPC_AddListRandomNumber), RpcTarget.AllBuffered, num);
            }

            yield return new WaitUntil(() => _values.Count == n);

            for (int i = 0; i < n; i++)
            {
                var go = Instantiate(cardItemPrefab, cardParent);
                var item = go.GetComponent<MeteorCardItem>();
                if (!item) continue;

                int value = _values[i];
                int index = i;
                item.Init(index, OnClickCard, value);
                _indexToItem[index] = item;
            }
        }*/
        
        

        [PunRPC]
        private void RPC_AddListRandomNumber(int n)
        {
            _values.Add(n);
        }
        
        
        private void ClearChildren(RectTransform rt)
        {
            if (!rt) return;
            for (int i = rt.childCount - 1; i >= 0; i--)
                Destroy(rt.GetChild(i).gameObject);
        }

        private void OpenUI(bool open)
        {
            if (cardUICanvas)
            {
                cardUICanvas.alpha = open ? 1f : 0f;
                cardUICanvas.interactable = open;
                cardUICanvas.blocksRaycasts = open;
            }

            // 배경 블랙아웃(다른 UI 가리기)
            if (blackout)
            {
                blackout.alpha = open ? 0.75f : 0f;
                blackout.blocksRaycasts = open;
                blackout.interactable = open;
            }
        }

        private void SetBanner(string msg) { if (bannerText) bannerText.text = msg; }

        private void OnClickCard(int index)
        {
            if (!PhotonNetwork.InRoom) return;
            if (_localPickLocked) return; // ★ 같은 프레임 중복 방지 (확정 전에도 막음)

            if (!_indexToItem.TryGetValue(index, out var item)) return;

            int value = item.Value;

            // ★ 로컬 즉시 잠금 + 모든 버튼 임시 비활성 (중복 입력 방어)
            _localPickLocked = true;
            SetAllInteractable(false);

            photonView.RPC(nameof(RPC_RequestPick), RpcTarget.MasterClient,
                PhotonNetwork.LocalPlayer.ActorNumber, value, index);
        }
        
        // 모든 카드 버튼 활성/비활성 일괄 제어
        private void SetAllInteractable(bool interactable)
        {
            foreach (var kv in _indexToItem)
            {
                var item = kv.Value;                  // 카드 아이템(또는 null)
                if (item == null) continue;           // null 안전 처리

                var btn = item.GetComponent<Button>(); // 버튼 컴포넌트 찾기
                if (btn != null)                      // 버튼이 있을 때만
                    btn.interactable = interactable;
            }
        }
        
        // 현재까지 확정된 값(_actorToValue) 기준으로 버튼 상태 재계산
        private void RefreshInteractivityFromTaken()
        {
            // UniqueRandom 정책: 이미 선택된 value만 막으면 됨
            HashSet<int> taken = new HashSet<int>(_actorToValue.Values);

            foreach (var kv in _indexToItem)
            {
                var it = kv.Value;
                if (!it) continue;

                bool alreadyTaken = taken.Contains(it.Value);
                var btn = it.GetComponent<UnityEngine.UI.Button>();
                if (btn) btn.interactable = !alreadyTaken && !_localPickLocked;
                if (alreadyTaken) it.DimUnavailable(); // 시각 피드백(뒷면 유지)
            }
        }
        
        private void TryAutoWireFromScene()
        {
            // 이미 연결돼 있으면 스킵
            var root = GetComponentInParent<Canvas>()?.transform ?? transform.root;

            // CanvasGroup 자동 탐색 (이름 기준)
            if (!cardUICanvas)
                cardUICanvas = FindByName<CanvasGroup>(root, "CardSelectionUI");
            if (!blackout)
                blackout = FindByName<CanvasGroup>(root, "BlackoutPanel");

            // 부모/텍스트 자동 탐색
            if (!cardParent)
                cardParent = FindByName<RectTransform>(root, "CardParent");
            if (!countdownText)
                countdownText = FindByName<TMP_Text>(root, "CountdownText"); // TMP면 TextMeshProUGUI로 바꿔도 OK
            if (!bannerText)
                bannerText = FindByName<TMP_Text>(root, "BannerText");

            // 누락 체크 (게임 시작 전에 로그로 확인)
            if (!cardUICanvas || !blackout || !cardParent || !cardItemPrefab)
            {
                Debug.LogWarning($"[Card] AutoWire result → " +
                                 $"cardUICanvas:{(cardUICanvas? "OK":"NULL")} | " +
                                 $"blackout:{(blackout? "OK":"NULL")} | " +
                                 $"cardParent:{(cardParent? "OK":"NULL")} | " +
                                 $"cardItemPrefab:{(cardItemPrefab? "OK":"NULL")} | " +
                                 $"countdownText:{(countdownText? "OK":"NULL")} | " +
                                 $"bannerText:{(bannerText? "OK":"NULL")}");
            }
        }
        
        private T FindByName<T>(Transform root, string name) where T : Component
        {
            if (!root) return null;
            foreach (var t in root.GetComponentsInChildren<T>(true))
                if (t.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    return t;
            return null;
        }

        [PunRPC]
        private void RPC_RequestPick(int actorNumber, int value, int index, PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            if (_lockedValues.Contains(value) && distributionRule == DistributionRule.UniqueRandom)
            {
                photonView.RPC(nameof(RPC_PickRejected), info.Sender, value);
                return;
            }

            _lockedValues.Add(value);
            _actorToValue[actorNumber] = value;
            photonView.RPC(nameof(RPC_ConfirmPick), RpcTarget.AllBuffered, actorNumber, value);

            if (_actorToValue.Count >= PhotonNetwork.CurrentRoom.PlayerCount)
                FinalizeOrderAndStart();
        }

        [PunRPC]
        private void RPC_PickRejected(int value)
        {
            // ★ 선택 충돌 시(동일 카드 경합) → 로컬 잠금 해제 + 상태 재계산
            _localPickLocked = false;
            SetBanner("이미 선택된 카드입니다. 다른 카드를 고르세요.");
            RefreshInteractivityFromTaken();
            Debug.Log($"[Card] Pick rejected: {value}");
        }

        [PunRPC]
        private void RPC_ConfirmPick(int actorNumber, int value)
        {
            // ★ 모든 클라이언트의 딕셔너리도 동기화하여 로컬 체크가 유효하게
            _actorToValue[actorNumber] = value;

            // 시각 피드백: 선택된 카드만 강조(숫자 공개는 나중 일괄 공개)
            foreach (var kv in _indexToItem)
            {
                var item = kv.Value;
                if (!item) continue;

                if (item.Value == value)
                    item.MarkPicked(mine: actorNumber == PhotonNetwork.LocalPlayer.ActorNumber);
            }

            // 내가 확정된 경우 → 계속 잠금 유지(재선택 불가)
            if (actorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
            {
                SetBanner("다른 플레이어 대기중…");
                // 나머지 카드들은 굳이 상호작용 복구할 필요 없음(이미 내 선택 확정)
            }
            else
            {
                // 다른 사람이 확정되면, 그 value만 선택 불가로 갱신
                if (!_localPickLocked) RefreshInteractivityFromTaken();
            }
        }

        private void FinalizeOrderAndStart()
        {
            if (_timeoutCo != null) StopCoroutine(_timeoutCo);

            // 1) 순서 계산(작은 숫자 → 먼저)
            var pairs = _actorToValue.ToList();
            pairs.Sort((a, b) =>
            {
                int cmp = a.Value.CompareTo(b.Value);
                return ascendingOrder ? cmp : -cmp;
            });
            var order = pairs.Select(p => p.Key).ToArray();
            Debug.Log("[Card] Final order → " + string.Join(",", order));

            // 2) 모두에게 “n초 뒤 일괄 공개 + 시작” 신호
            photonView.RPC(nameof(RPC_RevealAndStart), RpcTarget.AllBuffered, order, revealDelay);
        }

        [PunRPC]
        private void RPC_RevealAndStart(int[] order, float delay)
        {
            StartCoroutine(Co_RevealAndStart(order, delay));
        }

        private IEnumerator Co_RevealAndStart(int[] order, float delay)
        {
            // n초 대기(연출용)
            float t = Mathf.Max(0f, delay);
            while (t > 0f)
            {
                if (countdownText) countdownText.text = Mathf.CeilToInt(t).ToString();
                yield return null; t -= Time.deltaTime;
            }

            // 모두 공개(원하면 flipInterval로 톡톡 순차 공개)
            var items = _indexToItem.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList();
            for (int i = 0; i < items.Count; i++)
            {
                items[i]?.FlipReveal();
                if (flipInterval > 0f) yield return new WaitForSeconds(flipInterval);
            }

            // 잠깐 보여주고 닫기
            yield return new WaitForSeconds(0.6f);
            CloseSelectionUI();

            // 마스터만 턴 시작을 실제로 호출(중복 방지)
            if (PhotonNetwork.IsMasterClient && order != null && order.Length > 0)
                TurnManager.Instance.InitTurnOrder(order);
        }

        /// <summary>카드 선택 UI를 연다. (씬 시작 시 SceneController에서 호출)</summary>
        public void OpenSelectionUI()
        {
            // 필수 레퍼런스 보장 시도
            TryAutoWireFromScene();

            if (!cardUICanvas || !blackout || !cardParent || !cardItemPrefab)
            {
                Debug.LogError($"[Card] Missing refs: " +
                               $"{(cardUICanvas ? "" : "cardUICanvas ")}" +
                               $"{(blackout ? "" : "blackout ")}" +
                               $"{(cardParent ? "" : "cardParent ")}" +
                               $"{(cardItemPrefab ? "" : "cardItemPrefab ")}" +
                               $"{(countdownText ? "" : "countdownText ")}" +
                               $"{(bannerText ? "" : "bannerText ")}");
                return; // ★ NRE 차단
            }

            OpenUI(true);

            // 선택 타임아웃 코루틴
            if (selectionTimeout > 0f)
            {
                if (_timeoutCo != null) StopCoroutine(_timeoutCo);
                _timeoutCo = StartCoroutine(Co_SelectionTimeout(selectionTimeout));
            }

            SetBanner("카드를 선택하세요");
        }
        
        public class InGameUIManager : MonoBehaviour
        {
            public static InGameUIManager Instance;

            [SerializeField] private CanvasGroup uiGroup;

            private void Awake()
            {
                Instance = this;
            }

            public void Show(bool show)
            {
                uiGroup.alpha = show ? 1f : 0f;
                uiGroup.interactable = show;
                uiGroup.blocksRaycasts = show;
            }
        }

        /// <summary>카드 선택 UI를 닫는다(선택 종료 후 호출).</summary>
        public void CloseSelectionUI()
        {
            OpenUI(false);
            if (_timeoutCo != null) { StopCoroutine(_timeoutCo); _timeoutCo = null; }
        }

        /// <summary>다음 라운드를 위해 선택 상태 초기화.</summary>
        public void ResetSelection(bool rebuildCards = false)
        {
            _actorToValue.Clear();
            _lockedValues.Clear();
            _localPickLocked = false;
            if (rebuildCards) BuildCardsForCurrentPlayers();
            foreach (var it in _indexToItem.Values) it?.SetBackface();

            OpenUI(true);
            if (selectionTimeout > 0f)
            {
                if (_timeoutCo != null) StopCoroutine(_timeoutCo);
                _timeoutCo = StartCoroutine(Co_SelectionTimeout(selectionTimeout));
            }
            SetBanner("카드를 선택하세요");
        }

        private IEnumerator Co_SelectionTimeout(float seconds)
        {
            float t = seconds;
            while (t > 0f)
            {
                if (countdownText) countdownText.text = Mathf.CeilToInt(t).ToString();
                yield return null; t -= Time.deltaTime;
                if (_actorToValue.Count >= PhotonNetwork.CurrentRoom.PlayerCount) yield break;
            }

            // 타임아웃: 마스터가 미선택자에게 자동 배정
            if (PhotonNetwork.IsMasterClient)
            {
                var remainingPlayers = PhotonNetwork.PlayerList
                    .Select(p => p.ActorNumber)
                    .Where(a => !_actorToValue.ContainsKey(a)).ToList();

                // 아직 잠기지 않은 값만 추출
                var remainingValues = _indexToItem.Values
                    .Select(it => it.Value)
                    .Where(v => !_lockedValues.Contains(v)).ToList();

                foreach (var actor in remainingPlayers)
                {
                    if (remainingValues.Count == 0) break;
                    int v = remainingValues[0];
                    remainingValues.RemoveAt(0);

                    _lockedValues.Add(v);
                    _actorToValue[actor] = v;
                    photonView.RPC(nameof(RPC_ConfirmPick), RpcTarget.AllBuffered, actor, v);
                }
                if (_actorToValue.Count >= PhotonNetwork.CurrentRoom.PlayerCount)
                    FinalizeOrderAndStart();
            }
        }
    }
}
