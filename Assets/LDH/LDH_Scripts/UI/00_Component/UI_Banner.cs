using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UI_Banner : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image bannerBackGround;
    [SerializeField] private Image bannerImage;         //인스펙터창에서 무조건 넣어주기
    private BannerData data;

    private void Awake()
    {
        if(bannerBackGround == null)
            bannerBackGround = GetComponent<Image>();
    }
    // 런타임에 호출되는 초기화 메서드
    public void Initialize(BannerData bannerData)
    {
        data = bannerData;
        bannerBackGround.color = bannerData.bannerBackGroundColor;
        bannerBackGround.sprite = data.bannerBackGroundImage;
        bannerImage.sprite = data.bannerImage;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        data.onClickEvent?.Invoke();
    }
}
