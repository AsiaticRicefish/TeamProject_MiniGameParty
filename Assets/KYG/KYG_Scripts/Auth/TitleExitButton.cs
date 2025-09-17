using UnityEngine;

/// <summary>
/// 타이틀/로그인 씬 전용 종료 버튼 핸들러
/// - 에디터: Play 모드 종료
/// - 안드로이드: finishAndRemoveTask → Application.Quit 순으로 확실한 종료
/// </summary>
public class TitleExitButton : MonoBehaviour
{
    public void OnClickQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_ANDROID
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                activity.Call("finishAndRemoveTask");
        } catch { /* ignore */ }
        Application.Quit();
#else
        Application.Quit();
#endif
        Debug.Log("[TitleExitButton] Quit requested");
    }
}