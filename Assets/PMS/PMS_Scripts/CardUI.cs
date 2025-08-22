using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using LDH_UI;
//using DG.Tweening;

namespace ShootingScene
{
    public class CardUI : MonoBehaviour
    {
        [SerializeField] 
        private Sprite faceSprite,backSprite;

        [SerializeField] private Image cardBackGround;
        [SerializeField] private Image cardImage;
        [SerializeField] private Color ChangeColor;

        private bool isChecking;

        public Vector3 normalScale = Vector3.one;    // 원래 크기
        public Vector3 hoverScale = Vector3.one * 1.2f; // 마우스 오버시 커지는 크기

        private Vector3 targetScale;

        //CardFlip
        private RectTransform rt;
        private bool isFlipped = false;
        private bool isFlipping = false;
        private float flipDuration = 0.2f; // 뒤집는 시간
        private Quaternion startRot;
        private Quaternion endRot;

        public void Awake()
        {
           rt = GetComponent<RectTransform>();
        }
        public void Start() => Subscribe();

        public void Subscribe()
        {
            //UI에서 손가락 터치를 하고 땟을때 Tap
            UI_Base.BindUIEvent(gameObject, (_) => TapClick());
            //UI가 손가락 터치를 인식했을 때 
            UI_Base.BindUIEvent(gameObject, (_) => OnClick(), LDH_Util.Define_LDH.UIEvent.PointEnter);
            //UI가 손가락 터치를 인식못했을 때
            UI_Base.BindUIEvent(gameObject, (_) => UnClick(), LDH_Util.Define_LDH.UIEvent.PointExit);

            cardImage.sprite = backSprite;
        }

        public void OnClick()
        {
            sizeUp();    
        }

        public void UnClick()
        {
            sizeDown();
        }

        //추후 Dotween이나 Animation 사용하지 않을까
        public void sizeUp()
        {
            if (isFlipped && !isFlipping) return;

            targetScale = hoverScale;
            transform.localScale = targetScale;
        }

        public void sizeDown()
        {
            if (isFlipped && !isFlipping) return;

            targetScale = normalScale;
            transform.localScale = targetScale;
        }

        public void TapClick()
        {
            if (isFlipped && !isFlipping) return;

            Flip();
            Debug.Log("클릭함");
            //UI 180도 회전해서 다시 클릭을 못함
        }

        //IPointerDownHandler, IPointerUpHandler 필요
        //public void OnPointerDown(PointerEventData eventData) => OnClick();
        //public void OnPointerUp(PointerEventData eventData) => UnClick();

        public void Flip()
        {
            if (isFlipping) return; // 진행 중이면 무시

            isFlipped = !isFlipped;
            startRot = rt.localRotation;                //시작 회전값을 저장
            endRot = startRot * Quaternion.Euler(0, 180, 0); // y축 회전
            StartCoroutine(FlipAnim());
        }

        private IEnumerator FlipAnim()
        {
            isFlipping = true;
            float time = 0;

            while (time < flipDuration)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / flipDuration);
                rt.localRotation = Quaternion.Slerp(startRot, endRot, t);
                yield return null;
            }
            rt.localRotation = Quaternion.Euler(0, 0, 0);
            cardImage.sprite = faceSprite;
            endRot = startRot;
            isFlipping = false;
        }
    }
}