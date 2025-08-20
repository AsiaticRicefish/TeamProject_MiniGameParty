using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using LDH_UI;
//using DG.Tweening;

namespace ShootingScene
{
    public class CardUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image cardBackGround;
        [SerializeField] private Image cardImage;
        [SerializeField] private Color ChangeColor;

        [SerializeField] private CardFlip cardFlip;

        private bool isChecking;

        public Vector3 normalScale = Vector3.one;    // 원래 크기
        public Vector3 hoverScale = Vector3.one * 1.2f; // 마우스 오버시 커지는 크기

        private Vector3 targetScale;

        public void Awake()
        {
            cardFlip = GetComponent<CardFlip>();
        }
        public void Start() => Subscribe();

        public void Subscribe()
        {
            //UI_Base.BindUIEvent(gameObject, (_) => OnClick());
            //UI가 손가락 터치를 인식했을 때 
            //UI_Base.BindUIEvent(gameObject, (_) => OnClick(), LDH_Util.Define_LDH.UIEvent.PointEnter);
            //UI가 손가락 터치를 인식못했을 때
            //UI_Base.BindUIEvent(gameObject, (_) => UnClick(), LDH_Util.Define_LDH.UIEvent.PointExit);
            
        }

        public void OnClick()
        {
            sizeUp();
            //cardBackGround.color = ChangeColor;          
        }

        public void UnClick()
        {
            sizeDown();
        }

        //추후 Dotween이나 Animation 사용하지 않을까
        public void sizeUp()
        {
            targetScale = hoverScale;
            transform.localScale = targetScale;
        }

        public void sizeDown()
        {
            targetScale = normalScale;
            transform.localScale = targetScale;
        }

        //IPointerClickHandler 필요
        public void OnPointerClick(PointerEventData eventData)
        {
            cardFlip?.Flip();
            Debug.Log("클릭함");
            //UI 180도 회전해서 다시 클릭을 못함
        }

        //IPointerDownHandler, IPointerUpHandler 필요
        public void OnPointerDown(PointerEventData eventData) => OnClick();
        public void OnPointerUp(PointerEventData eventData) => UnClick();
    }
}