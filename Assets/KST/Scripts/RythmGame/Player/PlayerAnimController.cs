using Photon.Pun;
using UnityEngine;

public class PlayerAnimController : MonoBehaviourPun
{
    [SerializeField] Renderer _renderer;

    //애니메이션
    [SerializeField] Animator animator;
    public readonly int idle_Hash = Animator.StringToHash("Idle");
    public readonly int stun_Hash = Animator.StringToHash("Stun");
    void Awake()
    {

        //내가 아닌 플레이어들은 투명하게
        if (!photonView.IsMine)
        {
            Color c = _renderer.material.color;
            c.a = 0.5f;
            _renderer.material.color = c;
        }   
    }
}