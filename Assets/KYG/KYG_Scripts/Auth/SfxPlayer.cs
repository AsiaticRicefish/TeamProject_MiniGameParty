using UnityEngine;

public class SfxPlayer : MonoBehaviour
{
    [SerializeField] string sfxName = "Click"; // SoundCollection의 Sound Name과 동일하게

    // Button OnClick()에서 이 메서드를 호출하세요.
    public void Play()
    {
        if (SoundManager.Instance != null && !string.IsNullOrEmpty(sfxName))
            SoundManager.Instance.PlaySFX(sfxName);
    }
}