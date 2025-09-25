using UnityEngine;
using UnityEngine.SceneManagement;

public class SplashAnimationController : MonoBehaviour
{
    [SerializeField] private string startSceneName;

    public void OnAnimationEnd()
    {
        SceneManager.LoadScene(startSceneName); 
    }
}
