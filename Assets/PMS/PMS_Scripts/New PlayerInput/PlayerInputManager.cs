using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShootingScene
{
    public class PlayerInputManager : MonoBehaviour
    {
        public static PlayerInputManager Instance { get; private set; }

        [SerializeField] private Camera mainCam;

        private PlayerInput playerInput; // PlayerInput ������Ʈ ���� ����
        private InputAction touchAction; // TouchPress �׼� ���� ����

        private DirectionSign selectedDirectionSign;
        private UnimoEgg currentPlayerUnimo;

        //private float limitTime = 5.0f;    //�÷��� ���ѽð�
        private float chargingtime;        //���� ��¡ �ð�

        private bool isPressing = false;

        //��ġ �ϸ� UI�� ��������
        public event Action onTouched;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject); // Ȥ�� �׳� return
            }

            // PlayerInput ������Ʈ�� ��������, �׼��� ã���ϴ�.
            playerInput = GetComponent<PlayerInput>();
            if (playerInput != null)
            {
                touchAction = playerInput.actions.FindAction("TouchPress");
            }
        }

        private void Update()
        {
            // ������ �ִ� ���ȸ� timer ����
            if (isPressing)
            {
                chargingtime += Time.deltaTime;
            }
        }

        #region �̱� ȯ�� �׽�Ʈ �ڵ� - ���� �ڱ����϶� �ڱ⸸ Input�� �����ϵ��� ���� ���� 
        private void OnEnable()
        {
            EnableInput();
        }

        private void OnDisable()
        {
            // ��ũ��Ʈ�� ��Ȱ��ȭ�� �� �̺�Ʈ ���� ����
            DisableInput();
        }
        #endregion

        public void EnableInput()
        {
            // TouchPress �׼��� �̺�Ʈ�� ����
            if (touchAction != null)
            {
                touchAction.started += OnTouchPress;
                touchAction.performed += OnTouchPress;
                touchAction.canceled += OnTouchPress;
                touchAction.Enable();
            }
        }

        //touchAction ��Ȱ��ȭ - Ŭ�����ϰ� ����
        public void DisableInput()
        {
            if (touchAction != null)
            {
                touchAction.started -= OnTouchPress;
                touchAction.performed -= OnTouchPress;
                touchAction.canceled -= OnTouchPress;
                touchAction.Disable();
            }
        }

        // TouchPress �̺�Ʈ ����
        public void OnTouchPress(InputAction.CallbackContext ctx)
        {
            /*if(limitTime > 5.0f)
            {
                Debug.Log("���ѽð��� ������ ���� ����");

                // TODO - ���� ���� �߰� �۾�ó��                 
                // �ƿ� ���� �ȴ�ų�
                // ��ġ���ε� ���ѽð��� �����ų�
                 
            }*/

            Vector2 screenPos;

            //�� ���� �𸣰ڴµ� pc���� Simulrator�� Player�ϸ� �ڲ� �̻��ѵ� ���� ����

            // ��ġ���� ���콺���� Ȯ���ؼ� ��ġ ��������
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            {
                screenPos = Touchscreen.current.primaryTouch.position.ReadValue();   //Vector2 -> x,y��
            }
            else if (Mouse.current != null)
            {
                screenPos = Mouse.current.position.ReadValue(); //Vector2 -> x,y��
            }
            else
            {
                return;
            }

            Ray ray = mainCam.ScreenPointToRay(screenPos);

            if (ctx.started)
            {
                Debug.Log("start");

                // Ÿ�̸� �ʱ�ȭ
                ResetChargingTime();

                // ��ġ ���� �� Raycast�� �� ����
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, LayerMask.GetMask("DirectionSign")))
                {
                    Debug.Log("Raycast hit DirectionSign!");
                    onTouched?.Invoke();
                    selectedDirectionSign = hit.collider.GetComponent<DirectionSign>();
                    selectedDirectionSign?.OnTouchStart(screenPos);
                }
            }
            else if (ctx.performed)
            {
                isPressing = true;
                Debug.Log($"click�� - ���� �ð�: {chargingtime}��");
                selectedDirectionSign?.OnTouch(chargingtime);

            }
            else if (ctx.canceled)
            {
                Debug.Log($"end - ���� ���� �ð�: {chargingtime:F2}��");
                isPressing = false; // Ÿ�̸� ���� �ߴ�

                selectedDirectionSign?.OnTouchEnd();
                selectedDirectionSign = null;

                //DisableInput();                         //�ѹ� ��� ���� �ٽ� �����

                if (selectedDirectionSign == null)
                {
                    Debug.Log("NULLó���Ϸ�");
                }

            }
        }

        public void ResetChargingTime()
        {
            // Ÿ�̸� �ʱ�ȭ �� ��¡������ �ƴ��� bool ���� �ʱ�ȭ
            chargingtime = 0f;
            isPressing = false;
        }

        public void OnTouchPosition(InputAction.CallbackContext ctx)
        {
            Vector3 touchpos = ctx.ReadValue<Vector3>();
            // Debug.Log("��ġ ��ġ: " + pos);
        }
    }
}
