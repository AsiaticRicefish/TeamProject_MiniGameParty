using UnityEngine;
using UnityEngine.SceneManagement;

public class MoveToActiveSceneOnAwake : MonoBehaviour
{
    private void Awake()
    {
        var s = SceneManager.GetActiveScene();
        SceneManager.MoveGameObjectToScene(gameObject, s);
    }
}
