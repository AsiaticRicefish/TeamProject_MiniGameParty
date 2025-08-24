using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using ExitGames.Client.Photon; // Hashtable
using System.Linq;

namespace PMS_Util
{
    public static class PMS_Util
    {
        //한방에 100명씩 있는 대규모 멀티게임이 아니니깐 괜찮지 않을까? 
        //아 그냥 bool값 이 몇개인지만 마스터가 확인하면 되지 않을까, 일일히 확인하지 않고

        /// <summary>
        /// 모든 플레이어의 특정 Bool 프로퍼티가 true가 될 때까지 대기
        /// </summary>
        /// <param name="mono">코루틴 실행할 MonoBehaviour</param>                             -> 유틸 클래스가 MonoBehaviour 클래스가 아니기 때문에 외부에서 받아서 코루틴을 사용
        /// <param name="property">확인할 Property 이름/일단 bool변수만 가능 </param>   
        /// <param name="checkInterval">체크 간격 (초)</param>                                 -> 부담이 되면 이것을 조금 수정
        /// <param name="timeout">타임아웃 (0이면 무제한)</param>
        /// <returns>모든 플레이어가 true가 되면 true, 타임아웃 시 false</returns>
        public static IEnumerator WaitForAllPlayersPropertyTrue(MonoBehaviour mono, string property, float checkInterval = 0.1f, float timeout = 0f, Action onAllReady = null, Action onTimeout = null)
        {
            float elapsed = 0f;

            while (true)
            {
                // 모든 플레이어가 true인지 확인
                bool allReady = CheckAllPlayerProperty(property);

                if (allReady)
                {
                    onAllReady?.Invoke();
                    yield break; // 준비 완료
                }

                // 타임아웃 체크
                if (timeout > 0f)
                {
                    elapsed += checkInterval;
                    if (elapsed >= timeout)
                    {
                        onTimeout?.Invoke();
                        Debug.Log("[PMS_Util] - WaitForAllPlayersPropertyTrue 함수 대기시간 초과");
                        yield break;
                    }
                }

                yield return new WaitForSeconds(checkInterval);
            }
        }

        /// <summary>
        /// Room에서 각 플레이어들의 Bool type 플레이어 Property를 확인 할 수 있는 함수 
        /// </summary>
        public static bool CheckAllPlayerProperty(string property)
        {
            //연결 안 됨 or 연결됐지만 Room에 존재하지 않음
            if (!PhotonNetwork.IsConnected || (!PhotonNetwork.InRoom))
            {
                Debug.Log("현재 해당 클라이언트는 체크를 확인 할 수 없는 상태입니다.");
                return false;
            }

            foreach (var player in PhotonNetwork.CurrentRoom.Players)
            {
                if (player.Value.CustomProperties.TryGetValue(property, out object value))
                {
                    //패턴 매칭
                    if (value is bool b)
                    {
                        if (!b) return false; // 하나라도 false면 바로 종료
                    }
                    else
                    {
                        Debug.LogWarning($"플레이어 {property}이 Bool 타입이 아닙니다.");
                        return false;
                    }
                }
                else
                {
                    Debug.LogWarning($"플레이어 프로퍼티에 {property}이 없습니다.");
                    return false;
                }
            }
            return true; // 모든 플레이어 true
        }

        //범용적 사용 - 제한 두지 않음 로비/룸
        //자신의 플레이어 프로퍼티 변경하는 함수 -> Myself
        public static void SetPlayerProperty(string prop, object value)
        {
            ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable 
            {
                { prop, value }
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);

            Debug.Log($"[PMS_Util] 플레이어 {PhotonNetwork.LocalPlayer.NickName} 프로퍼티 '{prop}' = {value} 설정 완료");
        }

        public static string TryGetUidFromActor(int actorNumber)
        {
            //연결 안 됨 or 연결됐지만 Room에 존재하지 않음
            if (!PhotonNetwork.IsConnected || (!PhotonNetwork.InRoom))
            {
                Debug.Log("현재 해당 클라이언트는 체크를 확인 할 수 없는 상태입니다.");
                return null;
            }

            // 현재 방에 있는 플레이어 중 ActorNumber가 같은 플레이어를 찾음
            var p = PhotonNetwork.PlayerList.FirstOrDefault(x => x.ActorNumber == actorNumber);                         //using System.Linq; 추가 해야 FirstOrDefault 사용가능

            // 찾은 플레이어의 CustomProperties에서 "uid" 키 꺼내기
            if (p != null && p.CustomProperties != null && p.CustomProperties.TryGetValue("uid", out var uidObj))
            {
                return uidObj as string;
            }
            return null;
        }
    }
}
