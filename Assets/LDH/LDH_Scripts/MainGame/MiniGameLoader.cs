using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LDH_MainGame
{
    public static class MiniGameLoader
    {
        private static Scene _loadedScene;
        private static bool _hasScene;
        
        public static IEnumerator LoadAdditive(string sceneName, Action onReady)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            while (!op.isDone) yield return null;

            _loadedScene = SceneManager.GetSceneByName(sceneName);
            _hasScene = _loadedScene.IsValid();
            
            if (_hasScene)
                SceneManager.SetActiveScene(_loadedScene);
            
            if (_hasScene)
            {
                var root = _loadedScene.GetRootGameObjects();
            }

            onReady?.Invoke();
        }
        
        public static IEnumerator UnloadAdditive()
        {
            if (_hasScene)
            {
                var op = SceneManager.UnloadSceneAsync(_loadedScene);
                while (!op.isDone) yield return null;
                _hasScene = false;
            }
        }
    }
    
}