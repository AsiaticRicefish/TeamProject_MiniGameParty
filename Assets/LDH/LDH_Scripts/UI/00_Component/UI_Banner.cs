using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using LDH_Util;

public class UI_Banner : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [SerializeField] private Image bannerBackGround;
    [SerializeField] private Image bannerImage;         //인스펙터창에서 무조건 넣어주기
    private BannerData data;
    private LDH_UI.UI_BannerGroup parentGroup;

    // 판정 변수
    private Vector2 pointerDownPos;
    private float pointerDownTime;
    [SerializeField] private float dragThreshold = 25f;      // 픽셀
    [SerializeField] private float clickMaxDuration = 0.35f; // 초


    private void Awake()
    {
        if(bannerBackGround == null)
            bannerBackGround = GetComponent<Image>();
    }
    // 런타임에 호출되는 초기화 메서드
    public void Initialize(BannerData bannerData, LDH_UI.UI_BannerGroup group = null)
    {
        data = bannerData;
        parentGroup = group;
        bannerBackGround.color = bannerData.bannerBackGroundColor;
        bannerBackGround.sprite = data.bannerBackGroundImage;
        bannerImage.sprite = data.bannerImage;
    }

    /*public void OnPointerClick(PointerEventData eventData)
    {
        data.onClickEvent?.Invoke();
    }*/
    public void OnPointerDown(PointerEventData eventData)
    {
        pointerDownPos = eventData.position;
        pointerDownTime = Time.unscaledTime;
        Debug.Log($"[Banner] OnPointerDown pos={pointerDownPos} time={pointerDownTime}");
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // nothing needed here for now; OnPointerClick will evaluate
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 1) 그룹에서 드래그 중이면 클릭 무시
        if (parentGroup != null && parentGroup.IsUserDragging)
            return;

        // 2) 시간/이동 기준 검사
        float duration = Time.unscaledTime - pointerDownTime;
        float move = Vector2.Distance(pointerDownPos, eventData.position);
        Debug.Log($"[Banner] OnPointerClick duration={duration} move={move} isDragging={(parentGroup != null && parentGroup.IsUserDragging)}");

        if (duration > clickMaxDuration || move > dragThreshold)
            return;

        // 실제 클릭으로 인정하면 URL 실행
        if (!string.IsNullOrEmpty(data?.url))
        {
            Debug.Log($"[UI_Banner] URL 오픈 시도: {data.url}");
            UrlOpener.Open(data.url);
        }
            
            //Application.OpenURL(data.url);

        //if (!string.IsNullOrEmpty(data?.url))
        //    Application.OpenURL(data.url);

    }
}
