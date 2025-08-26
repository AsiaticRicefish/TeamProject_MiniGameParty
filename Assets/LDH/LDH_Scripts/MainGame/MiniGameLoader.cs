using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace LDH_MainGame
{
    public static class MiniGameLoader
    {
        private static Scene _loadedMiniScene;
        private static bool _hasMiniScene;
        private static List<Behaviour> _disabledOnMain = new(); // Camera, AudioListener, EventSystemBase 등
            
        
        public static IEnumerator LoadAdditive(string sceneName, Action onReady)
        {
            // 메인 씬 컴포넌트 중 비활성화 할 컴포넌트 처리
            var mainScene = SceneManager.GetActiveScene();
            _disabledOnMain.Clear();
            Disable<Camera>(mainScene);
            Disable<AudioListener>(mainScene);
            Disable<EventSystem>(mainScene); 
            
            
            
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            while (!op.isDone) yield return null;

            _loadedMiniScene = SceneManager.GetSceneByName(sceneName);
            _hasMiniScene = _loadedMiniScene.IsValid();
            
            if (_hasMiniScene)
                SceneManager.SetActiveScene(_loadedMiniScene);
     
            onReady?.Invoke();
        }
        
        public static IEnumerator UnloadAdditive()
        {
            if (_hasMiniScene)
            {
                var op = SceneManager.UnloadSceneAsync(_loadedMiniScene);
                while (!op.isDone) yield return null;
                _hasMiniScene = false;
            }
            
            // 메인 씬 컴포넌트 복원
            foreach (var b in _disabledOnMain.Where(b => b != null))
                b.enabled = true;
            _disabledOnMain.Clear();
            
            var mainScene = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(mainScene);
            
            
        }

        private static void Disable<T>(Scene scene) where T : Behaviour
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
        }
        private static void Enable<T>(Scene scene) where T : Behaviour
        {
            foreach (var go in scene.GetRootGameObjects())
            foreach (var c in go.GetComponentsInChildren<T>(true))
                c.enabled = true;
        }
        
    }
    
}