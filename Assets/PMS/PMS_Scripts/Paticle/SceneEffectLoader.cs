using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;

public class SceneEffectLoader : MonoBehaviour
{
    [SerializeField] SceneEffectConfig config;

    //씬 변경시 함수 호출
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;      
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != config.sceneName) return;
        foreach (var data in config.effects)
        {
            // ParticleManager에 미리 로드 요청
            ParticleManager.Instance.PreloadAsync(data).Forget();
        }
    }

    private void OnSceneUnloaded(Scene scene)
    {
        if (scene.name != config.sceneName) return;
        foreach (var data in config.effects)
        {
            // 릴리즈 요청
            ParticleManager.Instance.Release(data.id);
        }
    }
}
