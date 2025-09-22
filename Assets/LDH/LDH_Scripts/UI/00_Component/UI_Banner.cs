using UnityEngine;
using UnityEngine.UI.Extensions;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine.EventSystems;
using System;

namespace LDH_UI
{
    public class UI_Banner : MonoBehaviour, IPointerClickHandler,IPointerUpHandler, IPointerDownHandler
    {
        [SerializeField] private HorizontalScrollSnap scrollSnap;
        [SerializeField] private float autoSlideDelay = 3f; // 몇 초마다 넘어갈지 결정
        [SerializeField] private bool isUserInteracting = false; // 클릭/드래그 중 여부

       

        private CancellationTokenSource cts;

        private void Awake()
        {
            scrollSnap = GetComponent<HorizontalScrollSnap>();
        }

        private void OnEnable()
        {
            cts = new CancellationTokenSource();
            AutoSlideLoop(cts.Token).Forget(); // 실행
        }

        private void OnDisable()
        {
            cts?.Cancel();
            cts?.Dispose();
            cts = null;
        }

        private async UniTask AutoSlideLoop(CancellationToken token)
        {
            try
            {
                while (true)
                {
                    await UniTask.Delay(System.TimeSpan.FromSeconds(autoSlideDelay), cancellationToken: token);

                    // 마지막 페이지라면 첫 페이지로
                    if (scrollSnap.CurrentPage >= scrollSnap._screens - 1)
                        scrollSnap.GoToScreen(0);
                    else
                        scrollSnap.NextScreen();
                }
            }
            catch (OperationCanceledException)
            {
                // 토큰 취소로 인한 정상 종료
                Debug.Log("토큰 취소!");
            }
        }

        private void CancelTask()
        {
            cts?.Cancel();
            cts?.Dispose();
            cts = null;
        }

        private void Restart()
        {
            Debug.Log(" 계속 호출 되나 확인?");
            if(cts != null)
                CancelTask();

            cts = new CancellationTokenSource();
            AutoSlideLoop(cts.Token).Forget(); // 실행
        }

        // 사용자가 클릭(눌렀을 때)
        public void OnPointerDown(PointerEventData eventData)
        {
            if (isUserInteracting) return;

            isUserInteracting = true;
            CancelTask();
        }

        // 사용자가 클릭 해제
        public void OnPointerUp(PointerEventData eventData)
        {
            isUserInteracting = false;
            scrollSnap.OnEndDrag(eventData);
            Restart();
        }

        public void OnPointerClick(PointerEventData eventData)
        {          
            // 필요하다면 여기에 배너 클릭 시 로직 (예: 상세 페이지 열기)
        }
    }
}