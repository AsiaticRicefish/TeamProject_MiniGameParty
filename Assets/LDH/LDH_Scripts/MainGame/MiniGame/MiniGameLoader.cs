using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace LDH_MainGame
{
    public class MiniGameLoader : MonoBehaviour
    {
        private static Scene _mainScene;
        private static Scene _loadedMiniScene;
        private static bool _hasMiniScene;
        private static List<Behaviour> _disabledOnMain = new(); // Camera, AudioListener, EventSystemBase 등
        
        public IEnumerator LoadAdditive(string sceneName, Action onReady)
        {
            // 메인 씬 캐싱
            _mainScene = SceneManager.GetActiveScene();
            
            // 메인 씬 컴포넌트 중 비활성화 할 컴포넌트 처리
            _disabledOnMain.Clear();
            
            //먼저 disable 되도 괜찮은 컴포넌트
            yield return StartCoroutine(Disable<AudioListener>(_mainScene));
            yield return StartCoroutine(Disable<EventSystem>(_mainScene)); 
            yield return StartCoroutine(Disable<AudioListener>(_mainScene));

            // 미니게임 씬 addtive 로드
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            
            while (!op.isDone) yield return null;

            _loadedMiniScene = SceneManager.GetSceneByName(sceneName);
            _hasMiniScene = _loadedMiniScene.IsValid();

            //카메라만 나중에 비활성
            yield return StartCoroutine(Disable<Camera>(_mainScene));
            // 캔버스 비활성화
            yield return StartCoroutine(Disable<Canvas>(_mainScene));
            
            
            Debug.Log($"[MiniGameLoader] _hasMiniScene = _loadedMiniScene.IsValid() = {_hasMiniScene}");
            if (_hasMiniScene)
            {
                // 미니게임 씬을 활성씬으로 변경
                Debug.Log("[MiniGameLoader] Active MiniGame Scene");
                SceneManager.SetActiveScene(_loadedMiniScene);
            }

            onReady?.Invoke();
        }
        
        public IEnumerator UnloadAdditive()
        {
            Debug.Log("[MiniGameLoader] UnloadAdditive() called.");
            Debug.Log($"[MiniGameLoader] State check → hasMini={_hasMiniScene}, mini.IsValid={_loadedMiniScene.IsValid()}, mini.isLoaded={_loadedMiniScene.isLoaded}, active='{SceneManager.GetActiveScene().name}', main.IsValid={_mainScene.IsValid()}");

            //올라온 미니게임 씬이 있는지 확인
            if (!_hasMiniScene || !_loadedMiniScene.IsValid() || !_loadedMiniScene.isLoaded)
            {
                Debug.LogWarning("[MiniGameLoader] Nothing to unload (flag/scene invalid). Exiting early.");
                yield break;
            }
            
            
            //활성씬 먼저 변경
            if (SceneManager.GetActiveScene() == _loadedMiniScene && _mainScene.IsValid())
            {
                Debug.Log($"[MiniGameLoader] Active scene is mini '{_loadedMiniScene.name}'. Switching active scene back to main '{_mainScene.name}'.");

                SceneManager.SetActiveScene(_mainScene);
                yield return null;
                Debug.Log($"[MiniGameLoader] Active scene after switch → '{SceneManager.GetActiveScene().name}'.");

            }
            
            //미니게임 씬 언로드
            string miniName = _loadedMiniScene.name;
            Debug.Log($"[MiniGameLoader] Requesting unload for mini scene '{miniName}'...");

            var op = SceneManager.UnloadSceneAsync(_loadedMiniScene);
            if (op == null)
            {
                Debug.LogError("[MiniGameLoader] UnloadSceneAsync returned null. Scene might not be loaded or name/handle mismatch.");
                yield break;
            }
            float t0 = Time.realtimeSinceStartup;
            const float TIMEOUT = 15f; // seconds
            while (!op.isDone)
            {
                float elapsed = Time.realtimeSinceStartup - t0;
                Debug.Log($"[MiniGameLoader] Unloading... progress={op.progress:0.00}, elapsed={elapsed:0.00}s");
                if (elapsed > TIMEOUT)
                {
                    Debug.LogError($"[MiniGameLoader] Unload timeout (> {TIMEOUT}s). Something is holding references or async op stuck.");
                    break;
                }
                yield return null;
            }
            
            
            // 3) Verify unload by name
            var check = SceneManager.GetSceneByName(miniName);
            Debug.Log($"[MiniGameLoader] Verification → GetSceneByName('{miniName}'): isValid={check.IsValid()}, isLoaded={check.isLoaded}, active='{SceneManager.GetActiveScene().name}'");

            
            // 플래그 초기활
            _loadedMiniScene = default;
            _hasMiniScene = false;
            
            
            // 메인 씬 컴포넌트 복원
            int restoreCount = 0;

            foreach (var b in _disabledOnMain.Where(b => b != null))
            {
                b.enabled = true;
                restoreCount++;
                Debug.Log($"[MiniGameLoader] Restored {_disabledOnMain.Count} → actually re-enabled {restoreCount} behaviours on main scene.");

            }
            
            _disabledOnMain.Clear();
            Debug.Log($"[MiniGameLoader] Restored {_disabledOnMain.Count} → actually re-enabled {restoreCount} behaviours on main scene.");

            var main = _mainScene.IsValid() ? _mainScene : SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(main);
            Debug.Log($"[MiniGameLoader] Final active scene → '{SceneManager.GetActiveScene().name}'.");

            
            
        }

        private IEnumerator Disable<T>(Scene scene) where T : Behaviour
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                foreach (var c in go.GetComponentsInChildren<T>(true))
                {
                    if (c.enabled)
                    {
                        c.enabled = false;
                        _disabledOnMain.Add(c);
                    }
                }
            }

            yield return null;
        }
        // private static void Enable<T>(Scene scene) where T : Behaviour
        // {
        //     foreach (var go in scene.GetRootGameObjects())
        //     foreach (var c in go.GetComponentsInChildren<T>(true))
        //         c.enabled = true;
        // }
        
    }
    
}