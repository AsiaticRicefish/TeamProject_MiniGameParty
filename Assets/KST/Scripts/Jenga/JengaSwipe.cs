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

    //회전 관련
    [SerializeField] float _speed = 0.1f; //드래그 시 회전 속도
    [SerializeField] float _clickPx = 8f; //해당 픽셀 이하 이동이면 클릭으로 판정
    [SerializeField] float _clickTime = 0.1f; //해당 시간 이하동안 터치 시 클릭으로 판정

    bool _blockInput = false; //UI 클릭 시 true -> 입력 방지
    bool _isPress = false; // 눌림 여부
    Vector2 prevPos; //직전 좌표
    Vector2 pressPos; //처음 누른 좌표
    float pressTime; //눌린 시간
    Collider _collider;

    void Awake()
    {
        _collider = GetComponent<Collider>();
    }

    /// <summary>
    /// 눌림 발생 시 호출
    /// </summary>
    public void OnPress(InputAction.CallbackContext callback)
    {
        if (callback.started) //눌린 순간
        {
            _blockInput = false;
            _isPress = false;

            var pos = Pointer.current.position.ReadValue(); //현재 포인터 좌표(누르기 시작한 위치)

            //초기화
            pressPos = pos;
            prevPos = pos;
            pressTime = Time.unscaledTime;

            BlockInput(pos);
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
    void BlockInput(Vector2 pos)
    {
        if (IsOnUI())
        {
            Debug.Log("UI 위 입력입니다.");

            _blockInput = true;
            _isPress = false;

            return;
        }

        if (!IsHit(pos)) //젠가 오브젝트를 누르지 않았을 경우
        {
            Debug.Log("이 오브젝트를 누르지 않았습니다");

            _blockInput = true;
            _isPress = false;

            return;
        }

        _blockInput = false;
        _isPress = true;
    }

    /// <summary>
    /// 레이캐스트를 활용하여 해당 젠가를 누르고 있는지 판정
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
    /// 해당 좌표에 UI 여부 확인
    /// </summary>
    bool IsOnUI()
    {
        if (!EventSystem.current) return false;

        //현재 좌표 얻기
        Vector2 pos = Pointer.current != null ? Pointer.current.position.ReadValue() :
        Touchscreen.current != null ? Touchscreen.current.primaryTouch.position.ReadValue() :
        Vector2.zero;


        var eventData = new PointerEventData(EventSystem.current) { position = pos };

        //해당 좌표로 ui 레이캐스트 수행 결과
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        //UI가 한 개 이상 검출 되면 true 반환
        return results.Count > 0;
    }
}
