using UnityEngine;
using UnityEngine.EventSystems;

public class JengaSwipe : MonoBehaviour
{
    [SerializeField] float _speed;
    [SerializeField] float _clickPx = 8f; //�ش� �ȼ� ���� �̵��̸� Ŭ������ ����
    [SerializeField] float _clickTime = 0.1f; //�ش� �ð� ���ϵ��� ��ġ �� Ŭ������ ����

    bool _blockInput = false;
    Vector3 prevPos;
    Vector3 pressPos;
    float pressTime;

    //���� ��
    void OnMouseDown()
    {
        //UI���� Ŭ������ ���.
        if (EventSystem.current && EventSystem.current.IsPointerOverGameObject())
        {
            _blockInput = true;
            return;
        }
        pressPos = prevPos = Input.mousePosition;
        pressTime = Time.unscaledTime;
    }

    //�巡�� �� �� ��
    void OnMouseDrag()
    {
        if (_blockInput) return;

        Vector3 currentPos = Input.mousePosition;
        float movement = (currentPos.x - pressPos.x) * _speed;

        transform.Rotate(Vector3.up, -movement);

        prevPos = currentPos;
    }

    //�� ��
    void OnMouseUp()
    {
        if (_blockInput)
        {
            _blockInput = false;
            return;
        }

        Vector3 currentPos = Input.mousePosition;

        float distance = Vector2.Distance((Vector2)currentPos, (Vector2)pressPos);
        float time = Time.unscaledTime - pressTime;

        bool isClicked = distance <= _clickPx && time <= _clickTime;

        if (isClicked)
            Debug.Log("Ŭ�� �ƽ��ϴ�.");
    }

}
