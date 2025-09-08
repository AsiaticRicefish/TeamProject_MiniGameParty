using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LDH_Util;

namespace LDH_UI
{
    public class UI_Screen : UI_Base
    {
       
        protected override UniTask OnShowAsync(CancellationToken ct)
        {
            if (!cg) return UniTask.CompletedTask;
            cg.alpha = 1f;
            cg.blocksRaycasts = true;
            cg.interactable = true;
            return UniTask.CompletedTask;
        }


        protected override  UniTask OnCloseAsync(CancellationToken ct)
        {
            if (!cg) return UniTask.CompletedTask;
           
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;
            gameObject.SetActive(false);
            return UniTask.CompletedTask;
        }

    }
}