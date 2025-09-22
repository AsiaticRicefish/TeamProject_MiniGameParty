using UnityEngine;
using System.Linq;    
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

public class SceneEffectLoader : MonoBehaviour
{
    [SerializeField] SceneEffectConfig[] configs;

    private Dictionary<string, SceneEffectConfig> _configMap;

    void Awake()
    {
        _configMap = configs.ToDictionary(cfg => cfg.sceneName);
    }

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
        Debug.Log("[SceneEffectLoader] - 씬 변경 감지");
        // 1) 씬 이름으로 Config 조회, 없으면 바로 리턴
        if (!_configMap.TryGetValue(scene.name, out var sceneConfig))
            return;

        // 2) effects가 비어있거나 null인지 확인(안정성 확보)
        if (sceneConfig.effects == null || sceneConfig.effects.Length == 0)
            return;

        Debug.Log("[SceneEffectLoader] - Effect 비동기 로드 시작");
        // 3) 파티클 Preload
        foreach (var data in sceneConfig.effects)
            ParticleManager.Instance.PreloadAsync(data).Forget();
    }

    private void OnSceneUnloaded(Scene scene)
    {
        // 1) 씬 이름으로 Config 조회, 없으면 바로 리턴
        if (!_configMap.TryGetValue(scene.name, out var sceneConfig))
            return;

        // 2) effects가 비어있거나 null인지 확인(안정성 확보)
        if (sceneConfig.effects == null || sceneConfig.effects.Length == 0)
            return;

        // 3) 파티클 Release
        foreach (var data in sceneConfig.effects)
            ParticleManager.Instance.Release(data.id);
    }
}
