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
            
            
            // 미니게임 씬 addtive 로드
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            
            while (!op.isDone) yield return null;

            _loadedMiniScene = SceneManager.GetSceneByName(sceneName);
            _hasMiniScene = _loadedMiniScene.IsValid();

            //카메라만 나중에 비활성
            yield return StartCoroutine(Disable<Camera>(_mainScene));
          
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
            //올라온 미니게임 씬이 있는지 확인
            if (!_hasMiniScene) yield break;
            
            //활성씬 ㅁ너저 변경
            if (SceneManager.GetActiveScene() == _loadedMiniScene && _mainScene.IsValid())
            {
                SceneManager.SetActiveScene(_mainScene);
                yield return null;
            }
            
            //미니게임 씬 언로드
            var op = SceneManager.UnloadSceneAsync(_loadedMiniScene);
            while (!op.isDone) yield return null;

            
            // 플래그 초기활
            _loadedMiniScene = default;
            _hasMiniScene = false;
            
            
            // 메인 씬 컴포넌트 복원
            foreach (var b in _disabledOnMain.Where(b => b != null))
                b.enabled = true;
            _disabledOnMain.Clear();
            
            var mainScene = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(mainScene);
            
            
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