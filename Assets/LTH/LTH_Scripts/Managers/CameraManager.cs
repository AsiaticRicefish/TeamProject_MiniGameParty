using System;
using System.Collections.Generic;
using Cinemachine;
using DesignPattern;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CameraManager : CombinedSingleton<CameraManager>
{
    [Header("Priority Settings")]
    [SerializeField] private int activePriority = 20;
    [SerializeField] private int inactivePriority = 0;

    private Dictionary<string, CinemachineVirtualCamera> _cameraDict = new();
    private Stack<string> _cameraStack = new();
    
    private Camera _mainCam;

    protected override void OnAwake()
    {
        isPersistent = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start() => _mainCam = Camera.main;

    protected private void OnDestroy()
    {
        base.OnDestroy();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[CameraManager] OnSceneLoaded() → 스택 초기화. 씬: {scene.name}");
        ClearStack();  // 스택 클리어
    }


    #region Camera Main Reference


    /// <summary>
    /// 현재 씬의 MainCamera를 찾아 재지정합니다.
    /// </summary>
    public void UpdateMainCamera()
    {
        _mainCam = Camera.main;
        Debug.Log("[CameraManager] UpdateMainCamera() 호출됨 → MainCamera 갱신");
    }

    /// <summary>
    /// 외부에서 직접 MainCamera를 지정합니다.
    /// </summary>
    public void SetMainCamera(Camera cam)
    {
        _mainCam = cam;

        // CinemachineBrain 컴포넌트가 없으면 추가
        if (_mainCam && !_mainCam.TryGetComponent(out CinemachineBrain _))
        {
            _mainCam.gameObject.AddComponent<CinemachineBrain>();
        }
    }

    public Camera MainCamera => _mainCam != null ? _mainCam : Camera.main;

    #endregion

    #region Camera Register / UnRegister

    public bool HasCamera(string id) => _cameraDict.ContainsKey(id);

    /// <summary>
    /// VirtualCamera를 ID와 함께 등록합니다.
    /// </summary>
    public void RegisterCamera(string id, CinemachineVirtualCamera cam)
    {
        if (string.IsNullOrEmpty(id) || cam == null) return;
        if (_cameraDict.ContainsKey(id)) 
        { 
            Debug.LogWarning($"[CameraManager] Duplicate id: {id}"); 
            return; 
        }

        _cameraDict.Add(id, cam);
        cam.Priority = inactivePriority;

        Debug.Log($"[CameraManager] Registered: {id}");
    }

    /// <summary>
    /// ID로 등록된 VirtualCamera를 해제합니다.
    /// </summary>
    public void UnregisterCamera(string id)
    {
        if (!_cameraDict.Remove(id)) return;
        Debug.Log($"[CameraManager] UnregisterCamera() → 카메라 해제: {id}");
    }

    #endregion

    /// <summary>
    /// ID에 해당하는 VirtualCamera를 반환합니다.
    /// </summary>
    public CinemachineVirtualCamera GetCamera(string id)
    {
        _cameraDict.TryGetValue(id, out var cam);
        return cam;
    }

    #region Stack-based Camera Switching

    public string TopId => _cameraStack.Count > 0 ? _cameraStack.Peek() : null;

    /// <summary>
    /// 지정한 카메라를 스택 최상단에 올리고 우선순위 10으로 설정합니다.
    /// </summary>
    public void PushCamera(string id)
    {
        Debug.Log($"[CameraManager] PushCamera() 호출됨: {id}");

        if (!_cameraDict.ContainsKey(id))
        {
            Debug.LogWarning($"[CameraManager] PushCamera() → {id} 카메라가 존재하지 않음");
            return;
        }

        // 현재 top 비활성화
        if (_cameraStack.Count > 0)
        {
            var prev = GetCamera(_cameraStack.Peek());
            if (prev) prev.Priority = inactivePriority;
        }

        _cameraStack.Push(id);

        var now = GetCamera(id);
        if (now) now.Priority = activePriority;

        EnsureBrain(); // 전환 보장을 위해 Brain 부착 확인
    }

    /// <summary>
    /// 스택에서 현재 카메라를 제거하고, 바로 아래 카메라를 활성화합니다.
    /// </summary>
    public void PopCamera()
    {
        Debug.Log("[CameraManager] PopCamera() 호출됨");


        if (_cameraStack.Count == 0)
        {
            Debug.LogWarning("[CameraManager] PopCamera() → 카메라 스택이 비어 있음");
            return;
        }

        var poppedId = _cameraStack.Pop();
        var popped = GetCamera(poppedId);
        if (popped) popped.Priority = inactivePriority;


        if (_cameraStack.Count > 0)
        {
            var top = GetCamera(_cameraStack.Peek());
            if (top) top.Priority = activePriority;
        }
        Debug.Log($"[CameraManager] Pop → {poppedId} (nowTop: {TopId})");
    }

    /// <summary>
    /// 모든 카메라 우선순위를 0으로 초기화하고 스택을 비웁니다.
    /// </summary>
    public void ClearStack()
    {
        Debug.Log("[CameraManager] ClearStack() 호출됨");
        foreach (var cam in _cameraDict.Values) if (cam) cam.Priority = inactivePriority;
        _cameraStack.Clear();
    }

    public void ResetAllPriorities()
    {
        foreach (var kv in _cameraDict) if (kv.Value) kv.Value.Priority = inactivePriority;
    }

    #endregion

    #region Blends / Impulse (카메라 흔들림 기능)

    public void ApplyDefaultBlend(CinemachineBlendDefinition.Style style = CinemachineBlendDefinition.Style.EaseInOut,
                                  float time = 0.25f)
    {
        var brain = EnsureBrain();
        brain.m_DefaultBlend = new CinemachineBlendDefinition(style, time);
    }

    public void ApplyCustomBlends(CinemachineBlenderSettings settings)
    {
        var brain = EnsureBrain();
        brain.m_CustomBlends = settings;
    }

    public void PlayImpulseOnTop(float force = 1f)
    {
        if (TopId == null) { Debug.LogWarning("[CameraManager] Impulse → no top"); return; }
        var cam = GetCamera(TopId);
        if (!cam) return;

        var src = cam.GetComponent<CinemachineImpulseSource>();
        if (!src) { Debug.LogWarning($"[CameraManager] Impulse → no source on {TopId}"); return; }

        src.GenerateImpulseWithForce(force);
    }

    #endregion

    private CinemachineBrain EnsureBrain()
    {
        var cam = MainCamera != null ? MainCamera : Camera.main;
        if (!cam) cam = Camera.main;
        if (!cam) cam = FindObjectOfType<Camera>();
        if (!cam) throw new Exception("[CameraManager] No Camera found in scene.");

        if (!cam.TryGetComponent(out CinemachineBrain brain))
            brain = cam.gameObject.AddComponent<CinemachineBrain>();

        return brain;
    }
}