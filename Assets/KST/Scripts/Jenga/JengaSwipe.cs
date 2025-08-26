using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UIElements;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(PlayerInput))]
public class JengaSwipe : MonoBehaviour
{

    //ȸ�� ����
    [SerializeField] float _speed = 0.1f; //�巡�� �� ȸ�� �ӵ�
    [SerializeField] float _clickPx = 8f; //�ش� �ȼ� ���� �̵��̸� Ŭ������ ����
    [SerializeField] float _clickTime = 0.1f; //�ش� �ð� ���ϵ��� ��ġ �� Ŭ������ ����

    bool _blockInput = false; //UI Ŭ�� �� true -> �Է� ����
    bool _isPress = false; // ���� ����
    Vector2 prevPos; //���� ��ǥ
    Vector2 pressPos; //ó�� ���� ��ǥ
    float pressTime; //���� �ð�
    Collider _collider;

    void Awake()
    {
        _collider = GetComponent<Collider>();
    }

    /// <summary>
    /// ���� �߻� �� ȣ��
    /// </summary>
    public void OnPress(InputAction.CallbackContext callback)
    {
        if (callback.started) //���� ����
        {
            _blockInput = false;
            _isPress = false;

            var pos = Pointer.current.position.ReadValue(); //���� ������ ��ǥ(������ ������ ��ġ)

            //�ʱ�ȭ
            pressPos = pos;
            prevPos = pos;
            pressTime = Time.unscaledTime;

            BlockInput(pos);
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
    void BlockInput(Vector2 pos)
    {
        if (IsOnUI())
        {
            Debug.Log("UI �� �Է��Դϴ�.");

            _blockInput = true;
            _isPress = false;

            return;
        }

        if (!IsHit(pos)) //���� ������Ʈ�� ������ �ʾ��� ���
        {
            Debug.Log("�� ������Ʈ�� ������ �ʾҽ��ϴ�");

            _blockInput = true;
            _isPress = false;

            return;
        }

        _blockInput = false;
        _isPress = true;
    }

    /// <summary>
    /// ����ĳ��Ʈ�� Ȱ���Ͽ� �ش� ������ ������ �ִ��� ����
    /// </summary>
    bool IsHit(Vector2 pos)
    {
        if (!float.IsFinite(pos.x) || !float.IsFinite(pos.y))
            return false;
            
        pos.x = Mathf.Clamp(pos.x, 0f, Screen.width);
        pos.y = Mathf.Clamp(pos.y, 0f, Screen.height);

        Ray ray = Camera.main.ScreenPointToRay(pos);
        if (Physics.Raycast(ray, out var hit, Mathf.Infinity))
            return hit.collider == _collider;

        return false;
    }

    /// <summary>
    /// �ش� ��ǥ�� UI ���� Ȯ��
    /// </summary>
    bool IsOnUI()
    {
        if (!EventSystem.current) return false;

        //���� ��ǥ ���
        Vector2 pos = Pointer.current != null ? Pointer.current.position.ReadValue() :
        Touchscreen.current != null ? Touchscreen.current.primaryTouch.position.ReadValue() :
        Vector2.zero;


        var eventData = new PointerEventData(EventSystem.current) { position = pos };

        //�ش� ��ǥ�� ui ����ĳ��Ʈ ���� ���
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        //UI�� �� �� �̻� ���� �Ǹ� true ��ȯ
        return results.Count > 0;
    }
}
