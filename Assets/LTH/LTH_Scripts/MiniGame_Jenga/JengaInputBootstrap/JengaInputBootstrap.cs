using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
#endif

public class JengaInputBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
#if ENABLE_INPUT_SYSTEM
        // 에디터/PC에서 마우스를 터치처럼 사용
        TouchSimulation.Enable();
        // 멀티터치 정확도 향상
        EnhancedTouchSupport.Enable();
#endif
    }
}
