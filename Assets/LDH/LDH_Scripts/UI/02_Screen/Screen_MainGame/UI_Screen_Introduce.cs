using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Customization;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using LDH_Util;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LDH_UI.Screen_MainGame
{
    public class UI_Screen_Introduce : UI_Screen
    {
        [Serializable]
        public class PlayerUI
        {
            public GameObject panel;
            public RectTransform playerInfoTransform;
            public TMP_Text nickname;
            public Image profileImage;
        }

        [Header("UI")]
        [SerializeField] private List<PlayerUI> playerUis;
        
        [Header("Animation")]
        [SerializeField] private float moveDistance = 500f;   // 좌/우 오프셋
        [SerializeField] private float moveTime = 0.6f;      // 한 패널 슬라이드 시간
        [SerializeField] private float overshoot = 1.4f;      // Ease.OutBack 탄성감
        [SerializeField] private float stepDelay = 1f;     // 다음 패널까지 딜레이
        [SerializeField] private float fadeTime = 0.25f;      // 알파 페이드 시간

        [Header("Sound")] private Define_LDH.SfxKey playerPanelSpawnSfxKey = Define_LDH.SfxKey.Main_PlayerBanner;

        
        private int[] _order;   // 데이터 & 애니메이션 모두에 사용할 순서
        private Vector2[] _targetPosCache;                    // 각 패널의 본래 위치
        
        
        protected override void Clear()
        {
            base.Clear();
            foreach (var playerUi in playerUis)
            {
                if(!playerUi?.panel) continue;
                var rt = playerUi.playerInfoTransform;
                rt.DOKill();
                DOTween.Kill(playerUi.panel);
            }
        }

        
        
        // 외곽 -> 중앙 대칭 채우기 순서로 채운다.
        private IEnumerable<int> GetPanelOrder()
        {
            int l = 0, r = 3;
            while (l <= r)
            {
                yield return l;
                if (r != l) yield return r;
                l++;
                r--;
            }
            // return new int[] { 0, 3, 2, 1 };
        }
        
        public async UniTask SetData(Player[] players)
        {
            if(playerUis == null || playerUis.Count == 0) return;
            
            int playerCount = Mathf.Clamp(players.Length, 0, playerUis.Count);
            
            // 원래 위치 캐싱하기 위한 배열 초기화
            _targetPosCache ??= new Vector2[playerUis.Count];
            
            // 전부 끄기
            for (int i = 0; i < playerUis.Count; i++)
            {
                var ui = playerUis[i];
                if (!ui?.panel) continue;
                _targetPosCache[i] = ui.playerInfoTransform.anchoredPosition;
                ui.panel.SetActive(false);
            }
            
            // 1) 대칭 순서에서 인원수만큼 자르고 → 2) 오름차순 정렬 → 이 순서를 전역으로 보관
            _order = GetPanelOrder()
                        .Take(playerCount)
                        .OrderBy(x => x)
                        .ToArray();
            
            for (int i = 0; i < playerCount; i++)
            {
                int panelIndex = _order[i];
                var ui = playerUis[panelIndex];
                var rt = ui.playerInfoTransform;
           
                ui.panel.SetActive(true);     // 레이아웃 값 얻으려면 켜둬야 함
                await UniTask.Yield();        // 1프레임 대기(레이아웃 반영)

                
                ui.nickname.text = players[i].NickName;
                
                string profileKey = Define_LDH.PlayerProps.GetPlayerInfoKey(Define_LDH.PlayerProps.PlayerInfoKey.CharacterId);
                if (players[i].CustomProperties.TryGetValue(profileKey, out var v) == true && v is string profileId)
                    ui.profileImage.sprite = await CustomizationManager.Instance.GetIconAsync(profileId);
                else
                    ui.profileImage.sprite = null;

                await UniTask.Yield();
                
                // 시작 위치 설정
                // 시작 위치: 0/2는 +X에서, 1/3은 -X에서
                moveDistance = rt.rect.width; // 필요시 고정값 사용 권장
                float fromX = (panelIndex == 0 || panelIndex == 2) ? +moveDistance : -moveDistance;
                rt.anchoredPosition = _targetPosCache[panelIndex] + new Vector2(fromX, 0f);
            }
        }


        protected override async UniTask OnShowAsync(CancellationToken ct)
        {
            base.OnShowAsync(ct);
            for (int i = 0; i < _order.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                int panelIndex = _order[i];
                var ui = playerUis[panelIndex];
                var rt = ui.playerInfoTransform;               
                
                // 남은 트윈 정리
                rt.DOKill();
                
                var toPos = _targetPosCache[panelIndex];

                SoundManager.Instance.PlaySFX(playerPanelSpawnSfxKey.ToString());
                
                var seq = DOTween.Sequence()
                    .Join(rt.DOAnchorPos(toPos, moveTime).SetEase(Ease.OutBack, overshoot))
                    .Join(cg.DOFade(1f, fadeTime))
                    .SetLink(ui.panel, LinkBehaviour.KillOnDestroy);
                
                await seq.AsyncWaitForCompletion();
                
                if (stepDelay > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(stepDelay), cancellationToken: ct);

            }
        }
    }
}