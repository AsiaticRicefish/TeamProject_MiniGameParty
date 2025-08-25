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
        // ������/PC���� ���콺�� ��ġó�� ���
        TouchSimulation.Enable();
        // ��Ƽ��ġ ��Ȯ�� ���
        EnhancedTouchSupport.Enable();
#endif
    }
}
