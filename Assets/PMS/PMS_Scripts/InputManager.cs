using DesignPattern;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShootingScene
{
    public class InputManager : CombinedSingleton<InputManager>, IGameComponent
    {
        [SerializeField] private Camera mainCam;

        private PlayerInput playerInput; // PlayerInput ������Ʈ ���� ����
        private InputAction touchAction; // TouchPress �׼� ���� ����

        private UnimoEgg selectedUnimoEgg;

        protected override void OnAwake()
        {
            isPersistent = false;
        }

        public void Initialize()
        {
            // PlayerInput ������Ʈ�� ��������, �׼��� ã���ϴ�.
            playerInput = GetComponent<PlayerInput>();
            if (playerInput != null)
            {
                touchAction = playerInput.actions.FindAction("TouchPress");
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
                // ��ġ ���� �� Raycast�� �� ����
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, LayerMask.GetMask("UnimoEgg")))
                {
                    selectedUnimoEgg = hit.collider.GetComponent<UnimoEgg>();
                    selectedUnimoEgg?.OnTouchStart(screenPos);
                }
            }
            else if (ctx.performed)
            {
                Debug.Log("move");
                selectedUnimoEgg?.OnTouchMove(screenPos);
            }
            else if (ctx.canceled)
            {
                Debug.Log("end");
                selectedUnimoEgg?.OnTouchEnd(screenPos);
                selectedUnimoEgg = null;

                //DisableInput();                         //�ѹ� ��� ���� �ٽ� �����

                if (selectedUnimoEgg == null)
                {
                    Debug.Log("NULLó���Ϸ�");
                }

            }
        }

        public void OnTouchPosition(InputAction.CallbackContext ctx)
        {
            Vector3 touchpos = ctx.ReadValue<Vector3>();
            // Debug.Log("��ġ ��ġ: " + pos);
        }
    }
}
