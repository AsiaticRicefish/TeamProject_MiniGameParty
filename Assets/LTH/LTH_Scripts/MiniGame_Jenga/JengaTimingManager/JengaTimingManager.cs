using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DesignPattern;

namespace MiniGameJenga
{
    public class JengaTimingManager : CombinedSingleton<JengaTimingManager>, ICoroutineGameComponent
    {
        [SerializeField] private TimingGame timingUI;
        private Coroutine _countdownCo;

        private JengaBlock _currentBlock;           // 현재 타이밍 중인 블록
        public bool _isTimingActive;

        protected override void OnAwake()
        {
            isPersistent = false;
        }

        /// <summary>
        /// 병렬 초기화 파이프라인에서 호출됨
        /// UI를 강제로 찾지 않고 지연 초기화 방식 사용
        /// </summary>
        public IEnumerator InitializeCoroutine()
        {
            Debug.Log("[JengaTimingManager] Initialize - using deferred UI loading");
            yield return null;
        }

        private void OnEnable()
        {
            JengaBlock.OnAnyBlockTimingStart += HandleTimingStart;
        }

        private void OnDisable()
        {
            JengaBlock.OnAnyBlockTimingStart -= HandleTimingStart;
            UnsubscribeFromTimingUI();
        }

        /// <summary>
        /// TimingGame UI를 안전하게 가져오는 지연 초기화 메서드
        /// </summary>
        private TimingGame GetOrFindTimingUI()
        {
            if (timingUI == null)
            {
                timingUI = FindObjectOfType<TimingGame>(true);
                if (timingUI == null)
                {
                    Debug.LogWarning("[JengaTimingManager] TimingGame UI not found in scene");
                }
                else
                {
                    Debug.Log("[JengaTimingManager] TimingGame UI found and cached");
                }
            }
            return timingUI;
        }

        /// <summary>
        /// 블록의 타이밍 게임 시작 요청 처리
        /// </summary>
        private void HandleTimingStart(JengaBlock block)
        {
            if (!CanStartTiming(block)) return;

            var ui = GetOrFindTimingUI();
            if (ui == null)
            {
                Debug.LogError("[JengaTimingManager] Cannot start timing - UI not available");
                return;
            }

            StartTimingGame(block, ui);
        }

        /// <summary>
        /// 타이밍 게임을 시작할 수 있는지 검사
        /// </summary>
        private bool CanStartTiming(JengaBlock block)
        {
            if (_isTimingActive)
            {
                Debug.LogWarning("[JengaTimingManager] Timing already active - ignoring new request");
                return false;
            }

            if (block == null)
            {
                Debug.LogWarning("[JengaTimingManager] Block is null - cannot start timing");
                return false;
            }

            if (block.IsRemoved)
            {
                Debug.LogWarning("[JengaTimingManager] Block already removed - cannot start timing");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 타이밍 게임 시작 처리
        /// </summary>
        private void StartTimingGame(JengaBlock block, TimingGame ui)
        {
            _currentBlock = block;
            _isTimingActive = true;

            // 계층 전체 활성 보장 (부모가 꺼져 있으면 켜줌)
            ActivateHierarchy(ui.gameObject);

            // 최종 활성/사용 가능 여부 가드
            if (!ui.gameObject.activeInHierarchy || !ui.isActiveAndEnabled)
            {
                ResetTimingState();
                return;
            }

            // 해당 블록의 타워에서 현재 제거된 블록 개수 가져와서 적용
            var tower = JengaTowerManager.Instance?.GetPlayerTower(block.OwnerActorNumber);
            if (tower != null)
            {
                int removedCount = tower.GetRemovedBlocksCount();
                Debug.Log($"[JengaTimingManager] 타이밍 시작 - 제거된 블록 수: {removedCount}");

                // 타이밍 게임에 제거된 블록 수 기반으로 난이도 적용
                ui.DifficultyChange(removedCount);
            }

            // 이벤트 연결 + GameStart
            ui.OnFinished += HandleTimingFinished;
            ui.GameStart();

            // 매니저에서 코루틴 시작
            _countdownCo = StartCoroutine(ui.IE_CountDownPublic());

            Debug.Log($"[JengaTimingManager] Timing game started for block: {block.name}");
        }

        private static void ActivateHierarchy(GameObject go)
        {
            // 자기 자신
            if (!go.activeSelf) go.SetActive(true);

            // 부모 체인 모두 활성화
            var p = go.transform.parent;
            while (p != null)
            {
                if (!p.gameObject.activeSelf) p.gameObject.SetActive(true);
                p = p.parent;
            }
        }

        /// <summary>
        /// 타이밍 게임 완료 처리
        /// </summary>
        private void HandleTimingFinished(bool success, float accuracy)
        {
            Debug.Log($"[JengaTimingManager] Timing finished - Success: {success}, Accuracy: {accuracy:F2}");

            // 블록 처리
            _currentBlock?.ApplyTimingResult(success, accuracy);

            ResetTimingState();
        }

        /// <summary>
        /// TimingUI 정리 및 이벤트 해제
        /// </summary>
        private void CleanupTimingUI()
        {
            if (_countdownCo != null)
            {
                StopCoroutine(_countdownCo);
                _countdownCo = null;
            }

            UnsubscribeFromTimingUI();
        }

        /// <summary>
        /// TimingUI 이벤트 해제 (OnDisable에서 사용)
        /// </summary>
        private void UnsubscribeFromTimingUI()
        {
            if (timingUI != null)
            {
                timingUI.OnFinished -= HandleTimingFinished;
            }
        }

        /// <summary>
        /// 타이밍 상태 초기화
        /// </summary>
        private void ResetTimingState()
        {
            _currentBlock = null;
            _isTimingActive = false;
        }

        /// <summary>
        /// 현재 타이밍 게임이 활성 상태인지 확인
        /// </summary>
        public bool IsTimingActive => _isTimingActive;

        /// <summary>
        /// 현재 타이밍 중인 블록 반환 (읽기 전용)
        /// </summary>
        public JengaBlock CurrentTimingBlock => _currentBlock;

        /// <summary>
        /// 타이밍 게임을 강제로 중단 (필요시 사용)
        /// </summary>
        public void CancelTiming()
        {
            if (!_isTimingActive) return;

            Debug.Log("[JengaTimingManager] Timing game cancelled");
            CleanupTimingUI();
            ResetTimingState();
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터에서 TimingUI 수동 설정용 (Inspector에서 드래그&드롭)
        /// </summary>
        [ContextMenu("Find TimingGame UI")]
        public void EditorFindTimingUI()
        {
            timingUI = FindObjectOfType<TimingGame>(true);
            if (timingUI != null)
            {
                Debug.Log($"[JengaTimingManager] Found TimingGame UI: {timingUI.name}");
            }
            else
            {
                Debug.LogWarning("[JengaTimingManager] TimingGame UI not found in scene");
            }
        }
#endif
    }
}