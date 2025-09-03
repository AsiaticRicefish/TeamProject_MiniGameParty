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
        private static Scene _loadedMiniScene;
        private static bool _hasMiniScene;
        private static List<Behaviour> _disabledOnMain = new(); // Camera, AudioListener, EventSystemBase 등
            
        
        public IEnumerator LoadAdditive(string sceneName, Action onReady)
        {
            // 메인 씬 컴포넌트 중 비활성화 할 컴포넌트 처리
            var mainScene = SceneManager.GetActiveScene();
            _disabledOnMain.Clear();
            // Disable<Camera>(mainScene);
            // Disable<AudioListener>(mainScene);
            // Disable<EventSystem>(mainScene); 
            
            //먼저 disable 되도 괜찮은 컴포넌트
            yield return StartCoroutine(Disable<AudioListener>(mainScene));
            yield return StartCoroutine(Disable<EventSystem>(mainScene)); 
            
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            
            while (!op.isDone) yield return null;

            _loadedMiniScene = SceneManager.GetSceneByName(sceneName);
            _hasMiniScene = _loadedMiniScene.IsValid();

            //카메라만 나중에 비활성
            yield return StartCoroutine(Disable<Camera>(mainScene));
          
            
            if (_hasMiniScene)
            {
                Debug.Log("미니게임 씬 활성화 시점");
                SceneManager.SetActiveScene(_loadedMiniScene);
            }
            
          

            onReady?.Invoke();
        }
        
        public IEnumerator UnloadAdditive()
        {
            if (!_hasMiniScene) yield break;
            
            var op = SceneManager.UnloadSceneAsync(_loadedMiniScene);
            while (!op.isDone) yield return null;
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