using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 버튼 클릭 시 지정한 SFX를 재생하는 간단한 컴포넌트
/// </summary>
[RequireComponent(typeof(Button))]
public class PlaySfxOnClick : MonoBehaviour
{
    [SerializeField] private string sfxName = "Click";

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() =>
        {
            if (SoundManager.Instance != null && !string.IsNullOrEmpty(sfxName))
                SoundManager.Instance.PlaySFX(sfxName);
        });
    }
}