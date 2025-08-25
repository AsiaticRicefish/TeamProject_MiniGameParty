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

        private JengaBlock _currentBlock;           // ���� Ÿ�̹� ���� ���
        public bool _isTimingActive;

        protected override void OnAwake()
        {
            isPersistent = false;
        }

        /// <summary>
        /// ���� �ʱ�ȭ ���������ο��� ȣ���
        /// UI�� ������ ã�� �ʰ� ���� �ʱ�ȭ ��� ���
        /// </summary>
        public IEnumerator InitializeCoroutine()
        {
            Debug.Log("[JengaTimingManager] Initialize - using deferred UI loading");
            // UI �ʱ�ȭ�� ���� ��� �������� ����
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
        /// TimingGame UI�� �����ϰ� �������� ���� �ʱ�ȭ �޼���
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
        /// ����� Ÿ�̹� ���� ���� ��û ó��
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
        /// Ÿ�̹� ������ ������ �� �ִ��� �˻�
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
        /// Ÿ�̹� ���� ���� ó��
        /// </summary>
        private void StartTimingGame(JengaBlock block, TimingGame ui)
        {
            _currentBlock = block;
            _isTimingActive = true;

            // ���� ��ü Ȱ�� ���� (�θ� ���� ������ ����)
            ActivateHierarchy(ui.gameObject);

            // 2) ���� Ȱ��/��� ���� ���� ����
            if (!ui.gameObject.activeInHierarchy || !ui.isActiveAndEnabled)
            {
                ResetTimingState();
                return;
            }

            // 3) �̺�Ʈ ���� + GameStart
            ui.OnFinished += HandleTimingFinished;
            ui.GameStart();

            // 4) �Ŵ������� �ڷ�ƾ ����
            _countdownCo = StartCoroutine(ui.IE_CountDownPublic());

            Debug.Log($"[JengaTimingManager] Timing game started for block: {block.name}");
        }

        private static void ActivateHierarchy(GameObject go)
        {
            // �ڱ� �ڽ�
            if (!go.activeSelf) go.SetActive(true);

            // �θ� ü�� ��� Ȱ��ȭ
            var p = go.transform.parent;
            while (p != null)
            {
                if (!p.gameObject.activeSelf) p.gameObject.SetActive(true);
                p = p.parent;
            }
        }

        /// <summary>
        /// Ÿ�̹� ���� �Ϸ� ó��
        /// </summary>
        private void HandleTimingFinished(bool success, float accuracy)
        {
            Debug.Log($"[JengaTimingManager] Timing finished - Success: {success}, Accuracy: {accuracy:F2}");

            // ��� ó��
            _currentBlock?.ApplyTimingResult(success, accuracy);

            // ���� ���¸�
            ResetTimingState();
        }

        /// <summary>
        /// TimingUI ���� �� �̺�Ʈ ����
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
        /// TimingUI �̺�Ʈ ���� (OnDisable���� ���)
        /// </summary>
        private void UnsubscribeFromTimingUI()
        {
            if (timingUI != null)
            {
                timingUI.OnFinished -= HandleTimingFinished;
            }
        }

        /// <summary>
        /// Ÿ�̹� ���� �ʱ�ȭ
        /// </summary>
        private void ResetTimingState()
        {
            _currentBlock = null;
            _isTimingActive = false;
        }

        /// <summary>
        /// ���� Ÿ�̹� ������ Ȱ�� �������� Ȯ��
        /// </summary>
        public bool IsTimingActive => _isTimingActive;

        /// <summary>
        /// ���� Ÿ�̹� ���� ��� ��ȯ (�б� ����)
        /// </summary>
        public JengaBlock CurrentTimingBlock => _currentBlock;

        /// <summary>
        /// Ÿ�̹� ������ ������ �ߴ� (�ʿ�� ���)
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
        /// �����Ϳ��� TimingUI ���� ������ (Inspector���� �巡��&���)
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