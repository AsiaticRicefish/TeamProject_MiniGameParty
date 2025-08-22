using UnityEngine;
using UnityEngine.EventSystems;

public class JengaSwipe : MonoBehaviour
{
    [SerializeField] float _speed;
    [SerializeField] float _clickPx = 8f; //해당 픽셀 이하 이동이면 클릭으로 판정
    [SerializeField] float _clickTime = 0.1f; //해당 시간 이하동안 터치 시 클릭으로 판정

    bool _blockInput = false;
    Vector3 prevPos;
    Vector3 pressPos;
    float pressTime;

    //누를 때
    void OnMouseDown()
    {
        //UI위를 클릭했을 경우.
        if (EventSystem.current && EventSystem.current.IsPointerOverGameObject())
        {
            _blockInput = true;
            return;
        }
        pressPos = prevPos = Input.mousePosition;
        pressTime = Time.unscaledTime;
    }

    //드래그 중 일 때
    void OnMouseDrag()
    {
        if (_blockInput) return;

        Vector3 currentPos = Input.mousePosition;
        float movement = (currentPos.x - pressPos.x) * _speed;

        transform.Rotate(Vector3.up, -movement);

        prevPos = currentPos;
    }

    //뗄 때
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
            Debug.Log("클릭 됐습니다.");
    }

}
