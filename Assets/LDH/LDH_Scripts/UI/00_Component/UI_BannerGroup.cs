using UnityEngine;
using UnityEngine.UI.Extensions;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine.EventSystems;
using System;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.Events;

namespace LDH_UI
{
    public class UI_BannerGroup : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        [Header("데이터 & 프리팹")]
        [SerializeField] private BannerGroupData groupData;       // BannerData 리스트
        [SerializeField] private GameObject bannerItemPrefab;     // BannerItem 프리팹
        [SerializeField] private Transform bannersParent;         // 배너의 부모(스냅의 콘텐츠 컨테이너) Transform 

        [Header("스크롤 스냅 & 자동 슬라이드")]
        [SerializeField] private HorizontalScrollSnap scrollSnap; 
        [SerializeField][Range(0.1f,10f)] private float autoSlideDelay = 3f; // 몇 초마다 넘어갈지 결정
        [SerializeField] private bool isAutoSliding = false;                 // auto-slide가 이미 실행 중인지 표시하는 플래그

        [Header("페이지 토글 관련")]
        [SerializeField] private Toggle togglePrefab;
        [SerializeField] private Transform toggleParent;
        [SerializeField] private ToggleGroup toggleGroup;

        private List<Toggle> toggles = new List<Toggle>();
        private CancellationTokenSource cts;

        private void Awake()
        {
            if(scrollSnap == null)
                scrollSnap = GetComponent<HorizontalScrollSnap>();
        }

        private void Start()
        {
            // 배너 생성  
            foreach (var bannerData in groupData.banners)
            {
                //GameObject bannerGo = Instantiate(bannerItemPrefab, bannersParent);
                GameObject bannerGo = Instantiate(bannerItemPrefab);
                var banner = bannerGo.GetComponent<UI_Banner>();
                banner.Initialize(bannerData);

                // parent 설정과 내부 리스트 등록을 한 번에 처리
                scrollSnap.AddChild(bannerGo, false); 
            }


            // 토글 생성
            int pageCount = bannersParent.childCount;
            for (int i = 0; i < pageCount; i++)
            {
                var toggle = Instantiate(togglePrefab, toggleParent);
                toggle.group = toggleGroup;
                int index = i;
                toggle.onValueChanged.AddListener(isOn =>
                {
                    if (isOn)
                    {
                        RestartAutoSlide();
                        scrollSnap.GoToScreen(index);
                    }
                });
                toggles.Add(toggle);
            }
            // 2) 페이지 변경 이벤트 구독
            //scrollSnap.OnSelectionPageChangedEvent.AddListener(UpdateToggleIndicator);

            // 페이지 변경 이벤트 구독
            scrollSnap.OnSelectionPageChangedEvent.AddListener(OnPageSettled);

            // 첫 페이지로 이동 & 자동 슬라이드 시작
            scrollSnap.GoToScreen(0, false);

            UpdateToggleIndicator(0);

            StartAutoSlide();
        }
        private void UpdateToggleIndicator(int pageIndex)
        {
            for (int i = 0; i < toggles.Count; i++)
                toggles[i].isOn = (i == pageIndex);
        }

        private void StartAutoSlide()
        {
            // 이미 실행 중이면 리턴
            if (isAutoSliding)
                return;

            cts?.Cancel();
            cts = new CancellationTokenSource();
            isAutoSliding = true; // 시작 플래그 세팅
            AutoSlideLoop(cts.Token).Forget();
        }

        private void CancelAutoSlide()
        {
            if (cts == null)
            {
                Debug.Log("[Banner] - cts 토큰이 null입니다");
                return;
            }

            cts?.Cancel();
            cts?.Dispose();
            cts = null;
            isAutoSliding = false; // 종료 시 플래그 리셋
        }

        private void RestartAutoSlide()
        {
            // 기존 Task 취소
            CancelAutoSlide();
            // 새로 시작
            StartAutoSlide();
        }

        private async UniTask AutoSlideLoop(CancellationToken token)
        {
            try
            {
                Debug.Log("AutoSlideLoop 시작");
                while (!token.IsCancellationRequested)
                {
                    Debug.Log("  → Delay 전");
                    await UniTask.Delay(System.TimeSpan.FromSeconds(autoSlideDelay), cancellationToken: token);

                    // 마지막 페이지라면 첫 페이지로
                    int pageCount = scrollSnap._screens;//bannersParent.childCount;
                    //int lastpage = bannersParent.childCount;
                    if (scrollSnap.CurrentPage >= pageCount - 1)
                    {
                        Debug.Log("마지막 페이지 입니다 -> 처음페이지 이동");
                        scrollSnap.GoToScreen(0);
                    }
                    else
                    {
                        Debug.Log("다음페이지 이동");
                        scrollSnap.NextScreen();
                    }
                    Debug.Log("  → Delay 후: 현재 페이지=" + scrollSnap.CurrentPage);
                }
            }
            catch (OperationCanceledException)
            {
                // 토큰 취소로 인한 정상 종료
                // Debug.Log("토큰 취소!");
                Debug.Log("AutoSlideLoop 정상 취소");
            }
        }

        private void OnPageSettled(int pageIndex)
        {
            Debug.Log($"[Banner] 페이지 안정화 완료: {pageIndex}");
            UpdateToggleIndicator(pageIndex);
        }

        // 사용자가 드래그 시작(눌렀을 때)
        public void OnBeginDrag(PointerEventData eventData)
        {
            Debug.Log("[Banner] OnBeginDrag 호출됨");
            CancelAutoSlide();
        }

        // 사용자가 드래그 해제
        public void OnEndDrag(PointerEventData eventData)
        {
            Debug.Log("[Banner] OnEndDrag 호출됨");
            scrollSnap.OnEndDrag(eventData);
            RestartAutoSlide();
        }

        private void OnDisable()
        {
            CancelAutoSlide();
        }
    }
}