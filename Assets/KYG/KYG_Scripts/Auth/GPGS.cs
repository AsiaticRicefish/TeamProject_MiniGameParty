using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using UnityEngine.SocialPlatforms;
using System.Threading.Tasks;


public class GPGS : MonoBehaviour
{
    public void GpgsTest()
    {
        PlayGamesPlatform.Instance.Authenticate(OnAuthenticated);
    }

    private void OnAuthenticated(SignInStatus status)
    {
        if (status == SignInStatus.Success)
        {
            Debug.Log("Success");
        }
        else
        {
            Debug.LogError("Failure");
        }
    }
}
