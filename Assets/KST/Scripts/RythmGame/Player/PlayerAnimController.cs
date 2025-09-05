using System.Collections;
using Photon.Pun;
using UnityEngine;

public class PlayerAnimController : MonoBehaviourPun
{
    //캐릭터 랜더러 관련
    Renderer[] _renderer;
    string _invincibleProp = "_Invincible";
    int _invincibleID;

    //애니메이션
    [SerializeField] Animator animator;
    // public readonly int idle_Hash = Animator.StringToHash("anim_CH000_Idle");
    // public readonly int stun_Hash = Animator.StringToHash("anim_CH000_Stun");

    Coroutine _stunCo;
    bool isStun;

    void Awake()
    {
        _renderer = GetComponentsInChildren<Renderer>();
        _invincibleID = Shader.PropertyToID(_invincibleProp);

        //내가 아닌 플레이어들은 투명하게
        if (!photonView.IsMine)
        {
            foreach (var renderer in _renderer)
            {
                //유니모 쉐이더 용
                var mpb = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(mpb);
                mpb.SetFloat(_invincibleID, 0.5f);
                renderer.SetPropertyBlock(mpb);

                //일반 테스트용
                // Color c = renderer.material.color;
                // c.a = 0.5f;
                // renderer.material.color = c;
            }
        }
    }

    public void PlayeStunAnim(float time)
    {
        if (_stunCo != null) StopCoroutine(_stunCo);
        
        _stunCo = StartCoroutine(IE_Stun(time));
    }

    IEnumerator IE_Stun(float time)
    {
        isStun = true;
        // animator.Play(stun_Hash);
        animator.SetBool("isstun", true);
        Debug.Log("스턴 애니메이션 실행");

        yield return new WaitForSeconds(time);

        isStun = false;
        // animator.Play(idle_Hash);
        animator.SetBool("isstun", false);
        Debug.Log("휴지 애니메이션 실행");
        _stunCo = null;
    }

}
