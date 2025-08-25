using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using TMPro; // 상태 출력용(선택)
using Random = System.Random;

namespace ShootingScene
{
    /// <summary>
    /// Photon 없이 로컬 단일 디바이스에서 카드 선택/공개/순서 산출을 테스트하는 매니저.
    /// - 플레이어 수(2~4)에 맞춰 1..N 카드 생성 후 셔플
    /// - 클릭한 순서대로 "P1, P2, P3, P4"가 카드를 선택했다고 가정
    /// - 모두 선택되면 카드 일괄 공개 + 숫자 오름차순으로 턴 순서 계산
    /// </summary>
    public class CardManagerLocal : MonoBehaviour
    {
        [Header("Prefabs & Layout")]
        [SerializeField] private Transform cardParent;     // 카드를 배치할 Grid/Vertical/Horizontal
        [SerializeField] private CardUI cardPrefab;        // 카드 프리팹(PF_Card)

        [Header("Test Settings")]
        [Range(2, 4)]
        [SerializeField] private int playerCount = 4;      // 로컬 테스트용 인원(2~4)
        [SerializeField] private int randomSeed = 0;       // 0이면 Time 기반 시드

        [Header("Optional UI")]
        [SerializeField] private TMP_Text statusText;      // 상태/결과 표기용(없어도 동작)

        // 로컬 상태
        private List<CardUI> _cards = new();
        private int[] _deckValues; // 셔플된 카드 숫자(1..N)
        private int[] _owners;     // 각 카드의 소유자 index (0..N-1), 미선택= -1
        private string[] _mockPlayers; // "P1", "P2", "P3", "P4"
        private int _pickerIndex;  // 현재 선택 차례인 플레이어 index

        private void Start()
        {
            BuildAndSpawn();
        }

        /// <summary>씬에 배치된 카드들을 초기화하고 새 라운드를 시작</summary>
        public void BuildAndSpawn()
        {
            // 1) 기존 정리
            foreach (Transform t in cardParent) Destroy(t.gameObject);
            _cards.Clear();

            // 2) 데이터 준비
            int count = Mathf.Clamp(playerCount, 2, 4);
            _deckValues = Enumerable.Range(1, count).ToArray();

            int seed = (randomSeed == 0) ? Environment.TickCount : randomSeed;
            ShuffleInPlace(_deckValues, new Random(seed));

            _owners = Enumerable.Repeat(-1, count).ToArray();
            _mockPlayers = Enumerable.Range(1, count).Select(i => $"P{i}").ToArray();
            _pickerIndex = 0;

            // 3) 카드 UI 생성
            for (int i = 0; i < _deckValues.Length; i++)
            {
                var card = Instantiate(cardPrefab, cardParent);
                int idx = i;
                // CardUI는 (index, value, onClicked) 형태로 초기화
                card.Setup(idx, _deckValues[idx], TryPickLocal);
                _cards.Add(card);
            }

            SetStatus($"카드를 선택하세요. 현재 차례: {_mockPlayers[_pickerIndex]}");
            RefreshInteractables();
        }

        /// <summary>로컬: 카드 클릭 시 호출. 클릭 순서대로 P1→P2→P3→P4 가 선택했다고 가정.</summary>
        private void TryPickLocal(int cardIndex)
        {
            // 범위/중복 검사
            if (cardIndex < 0 || cardIndex >= _owners.Length) return;
            if (_owners[cardIndex] != -1) return; // 이미 선택된 카드

            // 현재 플레이어가 이 카드를 가져감
            _owners[cardIndex] = _pickerIndex;
            _pickerIndex++;

            RefreshInteractables();

            // 모두 선택 완료?
            bool allPicked = _owners.All(o => o != -1);
            if (allPicked)
            {
                // 일괄 공개
                foreach (var c in _cards) c.RevealFace();

                // 숫자 오름차순 → 해당 소유자(Pi) 순서로 턴 산출
                var pairs = new List<(int value, int ownerIdx)>();
                for (int i = 0; i < _deckValues.Length; i++)
                    pairs.Add((_deckValues[i], _owners[i]));

                var order = pairs.OrderBy(p => p.value).Select(p => p.ownerIdx).ToArray();
                string orderText = string.Join(" → ", order.Select(i => _mockPlayers[i]));

                Debug.Log($"[LOCAL] 턴 순서: {orderText}");
                SetStatus($"모든 카드 공개!\n턴 순서: {orderText}\n(Reset 버튼으로 다시 테스트)");
            }
            else
            {
                SetStatus($"선택됨! 다음 차례: {_mockPlayers[_pickerIndex]}");
            }
        }

        /// <summary>선택 가능/불가 색상 표기 갱신</summary>
        private void RefreshInteractables()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                bool free = _owners[i] == -1;
                bool isMine = false; // 로컬은 '내 것' 개념이 없으니 색상만 기본으로
                _cards[i].SetInteractable(free || isMine, isMine);
            }
        }

        /// <summary>다음 라운드용 리셋(버튼에 연결해서 사용)</summary>
        public void ResetLocal()
        {
            BuildAndSpawn();
        }

        private void SetStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
            Debug.Log(msg);
        }

        private void ShuffleInPlace<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
