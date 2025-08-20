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

        public Vector3 normalScale = Vector3.one;    // ���� ũ��
        public Vector3 hoverScale = Vector3.one * 1.2f; // ���콺 ������ Ŀ���� ũ��

        private Vector3 targetScale;

        public void Awake()
        {
            cardFlip = GetComponent<CardFlip>();
        }
        public void Start() => Subscribe();

        public void Subscribe()
        {
            //UI_Base.BindUIEvent(gameObject, (_) => OnClick());
            //UI�� �հ��� ��ġ�� �ν����� �� 
            //UI_Base.BindUIEvent(gameObject, (_) => OnClick(), LDH_Util.Define_LDH.UIEvent.PointEnter);
            //UI�� �հ��� ��ġ�� �νĸ����� ��
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

        //���� Dotween�̳� Animation ������� ������
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

        //IPointerClickHandler �ʿ�
        public void OnPointerClick(PointerEventData eventData)
        {
            cardFlip?.Flip();
            Debug.Log("Ŭ����");
            //UI 180�� ȸ���ؼ� �ٽ� Ŭ���� ����
        }

        //IPointerDownHandler, IPointerUpHandler �ʿ�
        public void OnPointerDown(PointerEventData eventData) => OnClick();
        public void OnPointerUp(PointerEventData eventData) => UnClick();
    }
}