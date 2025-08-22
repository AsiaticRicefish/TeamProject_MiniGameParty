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

        public Vector3 normalScale = Vector3.one;    // ���� ũ��
        public Vector3 hoverScale = Vector3.one * 1.2f; // ���콺 ������ Ŀ���� ũ��

        private Vector3 targetScale;

        //CardFlip
        private RectTransform rt;
        private bool isFlipped = false;
        private bool isFlipping = false;
        private float flipDuration = 0.2f; // ������ �ð�
        private Quaternion startRot;
        private Quaternion endRot;

        public void Awake()
        {
           rt = GetComponent<RectTransform>();
        }
        public void Start() => Subscribe();

        public void Subscribe()
        {
            //UI���� �հ��� ��ġ�� �ϰ� ������ Tap
            UI_Base.BindUIEvent(gameObject, (_) => TapClick());
            //UI�� �հ��� ��ġ�� �ν����� �� 
            UI_Base.BindUIEvent(gameObject, (_) => OnClick(), LDH_Util.Define_LDH.UIEvent.PointEnter);
            //UI�� �հ��� ��ġ�� �νĸ����� ��
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

        //���� Dotween�̳� Animation ������� ������
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
            Debug.Log("Ŭ����");
            //UI 180�� ȸ���ؼ� �ٽ� Ŭ���� ����
        }

        //IPointerDownHandler, IPointerUpHandler �ʿ�
        //public void OnPointerDown(PointerEventData eventData) => OnClick();
        //public void OnPointerUp(PointerEventData eventData) => UnClick();

        public void Flip()
        {
            if (isFlipping) return; // ���� ���̸� ����

            isFlipped = !isFlipped;
            startRot = rt.localRotation;                //���� ȸ������ ����
            endRot = startRot * Quaternion.Euler(0, 180, 0); // y�� ȸ��
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