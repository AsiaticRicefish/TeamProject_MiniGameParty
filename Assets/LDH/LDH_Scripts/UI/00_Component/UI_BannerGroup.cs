using UnityEngine;
using UnityEngine.UI.Extensions;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine.EventSystems;
using System;
using UnityEngine.UI;
using UnityEditor.ShaderGraph;

namespace LDH_UI
{
    public class UI_BannerGroup : MonoBehaviour,IPointerUpHandler, IPointerDownHandler
    {
        [Header("데이터 & 프리팹")]
        [SerializeField] private BannerGroupData groupData;       // BannerData 리스트
        [SerializeField] private GameObject bannerItemPrefab;     // BannerItem 프리팹
        [SerializeField] private Transform bannersParent;         // 배너의 부모(스냅의 콘텐츠 컨테이너) Transform 

        [Header("스크롤 스냅 & 자동 슬라이드")]
        [SerializeField] private HorizontalScrollSnap scrollSnap;
        [SerializeField][Range(0.1f,10f)] private float autoSlideDelay = 3f; // 몇 초마다 넘어갈지 결정
        [SerializeField] private bool isUserInteracting = false; // 클릭/드래그 중 여부    

        private CancellationTokenSource cts;

        private void Awake()
        {
            if(scrollSnap == null)
                scrollSnap = GetComponent<HorizontalScrollSnap>();
        }

        private void Start()
        {
            // 2) BannerData 수만큼 프리팹을 Content에 Instantiate       
            foreach (var bannerData in groupData.banners)
            {
                GameObject bannerGo = Instantiate(bannerItemPrefab, bannersParent);
                var banner = bannerGo.GetComponent<UI_Banner>();
                banner.Initialize(bannerData);
            }

            // 첫 페이지로 이동 & 자동 슬라이드 시작
            scrollSnap.GoToScreen(0, false);
            StartAutoSlide();
        }

        private void StartAutoSlide()
        {
            cts?.Cancel();
            cts = new CancellationTokenSource();
            AutoSlideLoop(cts.Token).Forget();
        }

        /*private void OnEnable()
        {
            cts = new CancellationTokenSource();
            AutoSlideLoop(cts.Token).Forget(); // 실행
        }*/

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
                    int lastpage = bannersParent.childCount;
                    if (scrollSnap.CurrentPage >= lastpage - 1)
                        scrollSnap.GoToScreen(0);
                    else
                        scrollSnap.NextScreen();
                }
            }
            catch (OperationCanceledException)
            {
                // 토큰 취소로 인한 정상 종료
                // Debug.Log("토큰 취소!");
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
            // Debug.Log(" 계속 호출 되나 확인?");
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
    }
}