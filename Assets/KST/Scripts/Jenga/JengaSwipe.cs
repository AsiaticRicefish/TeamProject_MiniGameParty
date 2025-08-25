using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(PlayerInput))]
public class JengaSwipe : MonoBehaviour
{
    [SerializeField] float _speed =0.1f; //�巡�� �� ȸ�� �ӵ�
    [SerializeField] float _clickPx = 8f; //�ش� �ȼ� ���� �̵��̸� Ŭ������ ����
    [SerializeField] float _clickTime = 0.1f; //�ش� �ð� ���ϵ��� ��ġ �� Ŭ������ ����

    bool _blockInput = false; //UI Ŭ�� �� true -> �Է� ����
    bool _isPress = false; // ���� ����
    Vector3 prevPos; //���� ��ǥ
    Vector3 pressPos; //ó�� ���� ��ǥ
    float pressTime; //���� �ð�

    /// <summary>
    /// ���� �߻� �� ȣ��
    /// </summary>
    public void OnPress(InputAction.CallbackContext callback)
    {
        if (callback.started) //���� ����
        {
            if (IsOnUI()) //UI ���� ������ ���
            {
                _blockInput = true;
                _isPress = false;
                return;
            }

            _blockInput = false;
            _isPress = true;

            var pos = Pointer.current.position.ReadValue(); //���� ������ ��ǥ(������ ������ ��ġ)

            //�ʱ�ȭ
            pressPos = pos; 
            prevPos = pos; 
            pressTime = Time.unscaledTime;
        }
        else if (callback.canceled) // �� ��
        {
            if (_blockInput)
            {
                _blockInput = false;
                return;
            }

            if (!_isPress) return;
            _isPress = false;

            var pos = Pointer.current.position.ReadValue(); //���� ������ ��ǥ(���� �� ��ġ)

            float distance = Vector2.Distance(pos, pressPos);//���� �� ��ġ ~ ������ �����ߴ� ��ġ �Ÿ�
            float time = Time.unscaledTime - pressTime; //���� �ð�

            //Ŭ�� �ּ� �Ÿ��� Ŭ�� �ּ� �ð� ���� ��
            if (distance <= _clickPx && time <= _clickTime)
            {
                Debug.Log("Ŭ������ ó��");
            }
        }
    }

    /// <summary>
    /// �巡�� �� ȣ��
    /// </summary>
    public void OnPointer(InputAction.CallbackContext callback)
    {
        if (!_isPress || _blockInput) return;

        Vector2 pos = callback.ReadValue<Vector2>();  //���� ������ ��ǥ

        float movement = (pos.x - prevPos.x) * _speed;
        transform.Rotate(Vector3.up, -movement);

        prevPos = pos; //���� ��ǥ ����
    }

    /// <summary>
    /// ���� �����Ͱ� UI ���� �ִ��� ����
    /// </summary>
    /// <returns></returns>
    bool IsOnUI()
    {
        if (!EventSystem.current) return false;

        return EventSystem.current.IsPointerOverGameObject();
    }
}
