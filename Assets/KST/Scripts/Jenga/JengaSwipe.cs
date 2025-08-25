using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(PlayerInput))]
public class JengaSwipe : MonoBehaviour
{
    [SerializeField] float _speed =0.1f; //드래그 시 회전 속도
    [SerializeField] float _clickPx = 8f; //해당 픽셀 이하 이동이면 클릭으로 판정
    [SerializeField] float _clickTime = 0.1f; //해당 시간 이하동안 터치 시 클릭으로 판정

    bool _blockInput = false; //UI 클릭 시 true -> 입력 방지
    bool _isPress = false; // 눌림 여부
    Vector3 prevPos; //직전 좌표
    Vector3 pressPos; //처음 누른 좌표
    float pressTime; //눌린 시간

    /// <summary>
    /// 눌림 발생 시 호출
    /// </summary>
    public void OnPress(InputAction.CallbackContext callback)
    {
        if (callback.started) //눌린 순간
        {
            if (IsOnUI()) //UI 위가 눌렸을 경우
            {
                _blockInput = true;
                _isPress = false;
                return;
            }

            _blockInput = false;
            _isPress = true;

            var pos = Pointer.current.position.ReadValue(); //현재 포인터 좌표(누르기 시작한 위치)

            //초기화
            pressPos = pos; 
            prevPos = pos; 
            pressTime = Time.unscaledTime;
        }
        else if (callback.canceled) // 뗄 때
        {
            if (_blockInput)
            {
                _blockInput = false;
                return;
            }

            if (!_isPress) return;
            _isPress = false;

            var pos = Pointer.current.position.ReadValue(); //현재 포인터 좌표(손을 뗀 위치)

            float distance = Vector2.Distance(pos, pressPos);//손을 뗀 위치 ~ 누르기 시작했던 위치 거리
            float time = Time.unscaledTime - pressTime; //눌린 시간

            //클릭 최소 거리와 클릭 최소 시간 만족 시
            if (distance <= _clickPx && time <= _clickTime)
            {
                Debug.Log("클릭판정 처리");
            }
        }
    }

    /// <summary>
    /// 드래그 시 호출
    /// </summary>
    public void OnPointer(InputAction.CallbackContext callback)
    {
        if (!_isPress || _blockInput) return;

        Vector2 pos = callback.ReadValue<Vector2>();  //현재 포인터 좌표

        float movement = (pos.x - prevPos.x) * _speed;
        transform.Rotate(Vector3.up, -movement);

        prevPos = pos; //직전 좌표 갱신
    }

    /// <summary>
    /// 현재 포인터가 UI 위에 있는지 판정
    /// </summary>
    /// <returns></returns>
    bool IsOnUI()
    {
        if (!EventSystem.current) return false;

        return EventSystem.current.IsPointerOverGameObject();
    }
}
