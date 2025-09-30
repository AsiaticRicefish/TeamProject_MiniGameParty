using System.Collections;
using RhythmGame;
using UnityEngine;
using UnityEngine.UI;

public class CooldownUI : MonoBehaviour
{
    [SerializeField] Image _img;
    Coroutine _co;

    void OnEnable()
    {
        StartCoroutine(IE_DelaySubscribe());
    }
    void OnDisable()
    {
        RhythmPlayerInput.OnCooldownStart -= Cooldown;
    }


    IEnumerator IE_DelaySubscribe()
    {
        yield return new WaitUntil(() => RhythmPlayerInput.instance != null);
        RhythmPlayerInput.OnCooldownStart += Cooldown;
        _img.gameObject.SetActive(false);
    }

    void Cooldown(float time)
    {
        if (_co != null)
            StopCoroutine(_co);
        _co = StartCoroutine(IE_Cooldown(time));

    }

    IEnumerator IE_Cooldown(float time)
    {
        _img.gameObject.SetActive(true);

        float t = time;
        _img.fillAmount = 1f;
        while (t > 0)
        {
            t -= Time.deltaTime;
            float value = Mathf.Clamp01(t / time);
            _img.fillAmount = value;
            yield return null;
        }

        _img.fillAmount = 0f;
        _img.gameObject.SetActive(false);
        _co = null;

    }

}