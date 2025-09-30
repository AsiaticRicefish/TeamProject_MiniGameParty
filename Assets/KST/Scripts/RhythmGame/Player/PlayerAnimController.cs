using System.Collections;
using Photon.Pun;
using RhythmGame;
using UnityEngine;

public class PlayerAnimController : MonoBehaviourPun
{
    //캐릭터 랜더러 관련
    Renderer[] _renderer;
    string _invincibleProp = "_Invincible";
    int _invincibleID;

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
                mpb.SetFloat(_invincibleID, 0.2f);
                renderer.SetPropertyBlock(mpb);
            }
        }
    }
}
