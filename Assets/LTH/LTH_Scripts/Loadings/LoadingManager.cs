using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DesignPattern;
using LDH_UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Managers
{
    /// <summary>
    /// 씬 로딩 관리 및 UI_Loading 제어
    /// </summary>
    public class LoadingManager : CombinedSingleton<LoadingManager>
    {
        [SerializeField] private UI_Loading loadingPopupPrefab;

        private UI_Loading _view;
        private CancellationTokenSource _cts;

        [SerializeField] private float sceneWeightDefault = 0.6f;    // 씬 로딩 반영 비율
        [SerializeField] private float initWeightDefault = 0.4f;     // 초기화(Reporter) 반영 비율
        [SerializeField] private float minShowSeconds = 0.6f;        // 너무 빨리 꺼져 깜박이는 것 방지

        protected override void OnAwake() { }

        /// <summary>
        /// 메인맵에서 호출: 미니게임 Additive 로딩 + 초기화 집계 + UI 표시
        /// </summary>
        /// <param name="sceneName">미니게임 씬 이름</param>
        /// <param name="theme">로딩 테마(SO). null이면 기본</param>
        /// <param name="sceneWeight">씬 로딩 가중치(0~1)</param>
        /// <param name="initWeight">초기화 가중치(0~1), 둘의 합은 1로 정규화됨</param>
        public async UniTask LoadMiniGameFlowAsync(
                string sceneName,
                UI_LoadingTheme theme = null,
                float sceneWeight = -1f,
                float initWeight = -1f)
        {
            CancelRunning();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            // 가중치 정규화 => (sw + iw)=1 이 되도록 정규화. 음수면 기본값 사용
            float sw = sceneWeight < 0 ? sceneWeightDefault : Mathf.Max(0f, sceneWeight);
            float iw = initWeight < 0 ? initWeightDefault : Mathf.Max(0f, initWeight);
            float sum = Mathf.Max(0.0001f, sw + iw);
            sw /= sum; iw /= sum;

            // 1) 로딩 UI 표시 (프리팹을 넘겨주는 시그니처 대응)
            float openedAt = Time.realtimeSinceStartup;

            if (!loadingPopupPrefab)
            {
                Debug.LogError("[LoadingManager] loadingPopupPrefab 이 비어있습니다.");
                return;
            }

            // Manager.UI.ShowPopupUI(loadingPopupPrefab)로 로딩 팝업 생성
            _view = await Manager.UI.ShowPopupUI(loadingPopupPrefab);
            if (!_view)
            {
                Debug.LogError("[LoadingManager] UI_Loading 인스턴스 생성 실패");
                return;
            }

            // 테마가 있는 경우 적용
            if (theme != null) _view.ApplyTheme(theme);
            _view.SetProgress(0f);

            // 2) 씬 Additive 로딩(sw 구간)
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            op.allowSceneActivation = true;

            while (!op.isDone)
            {
                ct.ThrowIfCancellationRequested();
                float p = Mathf.Clamp01(op.progress / 0.9f);
                _view.SetProgress(p * sw);
                await UniTask.Yield(ct);
            }
            _view.SetProgress(sw);

            // 3) 리포터 수집
            var reporters = Object.FindObjectsOfType<MonoBehaviour>(true)
                                  .OfType<ILoadReporter>()
                                  .Distinct()
                                  .ToList();

            if (reporters.Count == 0)
            {
                await EnsureMinShow(openedAt, minShowSeconds, ct);
                _view.SetProgress(1f);
                // 외부(카운트다운 직전)에 CloseAsync 호출
                return;
            }

            // 4) 병렬 초기화
            var tasks = new List<UniTask>(reporters.Count);
            foreach (var r in reporters)
                tasks.Add(r.BeginAsync(ct));

            while (!tasks.All(t => t.Status.IsCompleted()))
            {
                ct.ThrowIfCancellationRequested();

                float avg = 0f;
                for (int i = 0; i < reporters.Count; i++)
                    avg += Mathf.Clamp01(reporters[i].Progress);
                avg /= reporters.Count;

                _view.SetProgress(sw + avg * iw);
                await UniTask.Yield(ct);
            }

            await UniTask.WhenAll(tasks);

            await EnsureMinShow(openedAt, minShowSeconds, ct);
            _view.SetProgress(1f);
            // 닫기는 외부에서 CloseAsync()
        }

        public async UniTask CloseAsync()
        {
            // UI를 닫고 null 처리
            if (_view != null)
            {
                await Manager.UI.ClosePopupUI(_view);
                _view = null;
            }

            // CancelRunning()으로 정리
            CancelRunning();
        }

        /// <summary>
        /// Time.realtimeSinceStartup => 어플리케이션 실행 후 실제로 경과한 초 단위 시간을 반환
        /// </summary>>
        private async UniTask EnsureMinShow(float openedAt, float minSec, CancellationToken ct)
        {
            float elapsed = Time.realtimeSinceStartup - openedAt;
            float remain = minSec - elapsed;
            if (remain > 0f)
                await UniTask.Delay((int)(remain * 1000f), cancellationToken: ct);
        }

        private void CancelRunning()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            CancelRunning();
        }
    }
}