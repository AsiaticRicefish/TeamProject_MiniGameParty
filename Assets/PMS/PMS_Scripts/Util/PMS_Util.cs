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
        //�ѹ濡 100�� �ִ� ��Ը� ��Ƽ������ �ƴϴϱ� ������ ������? 
        //�� �׳� bool�� �� ������� �����Ͱ� Ȯ���ϸ� ���� ������, ������ Ȯ������ �ʰ�

        /// <summary>
        /// ��� �÷��̾��� Ư�� Bool ������Ƽ�� true�� �� ������ ���
        /// </summary>
        /// <param name="mono">�ڷ�ƾ ������ MonoBehaviour</param>                             -> ��ƿ Ŭ������ MonoBehaviour Ŭ������ �ƴϱ� ������ �ܺο��� �޾Ƽ� �ڷ�ƾ�� ���
        /// <param name="property">Ȯ���� Property �̸�/�ϴ� bool������ ���� </param>   
        /// <param name="checkInterval">üũ ���� (��)</param>                                 -> �δ��� �Ǹ� �̰��� ���� ����
        /// <param name="timeout">Ÿ�Ӿƿ� (0�̸� ������)</param>
        /// <returns>��� �÷��̾ true�� �Ǹ� true, Ÿ�Ӿƿ� �� false</returns>
        public static IEnumerator WaitForAllPlayersPropertyTrue(MonoBehaviour mono, string property, float checkInterval = 0.1f, float timeout = 0f, Action onAllReady = null, Action onTimeout = null)
        {
            float elapsed = 0f;

            while (true)
            {
                // ��� �÷��̾ true���� Ȯ��
                bool allReady = CheckAllPlayerProperty(property);

                if (allReady)
                {
                    onAllReady?.Invoke();
                    yield break; // �غ� �Ϸ�
                }

                // Ÿ�Ӿƿ� üũ
                if (timeout > 0f)
                {
                    elapsed += checkInterval;
                    if (elapsed >= timeout)
                    {
                        onTimeout?.Invoke();
                        Debug.Log("[PMS_Util] - WaitForAllPlayersPropertyTrue �Լ� ���ð� �ʰ�");
                        yield break;
                    }
                }

                yield return new WaitForSeconds(checkInterval);
            }
        }

        /// <summary>
        /// Room���� �� �÷��̾���� Bool type �÷��̾� Property�� Ȯ�� �� �� �ִ� �Լ� 
        /// </summary>
        public static bool CheckAllPlayerProperty(string property)
        {
            //���� �� �� or ��������� Room�� �������� ����
            if (!PhotonNetwork.IsConnected || (!PhotonNetwork.InRoom))
            {
                Debug.Log("���� �ش� Ŭ���̾�Ʈ�� üũ�� Ȯ�� �� �� ���� �����Դϴ�.");
                return false;
            }

            foreach (var player in PhotonNetwork.CurrentRoom.Players)
            {
                if (player.Value.CustomProperties.TryGetValue(property, out object value))
                {
                    //���� ��Ī
                    if (value is bool b)
                    {
                        if (!b) return false; // �ϳ��� false�� �ٷ� ����
                    }
                    else
                    {
                        Debug.LogWarning($"�÷��̾� {property}�� Bool Ÿ���� �ƴմϴ�.");
                        return false;
                    }
                }
                else
                {
                    Debug.LogWarning($"�÷��̾� ������Ƽ�� {property}�� �����ϴ�.");
                    return false;
                }
            }
            return true; // ��� �÷��̾� true
        }

        //������ ��� - ���� ���� ���� �κ�/��
        //�ڽ��� �÷��̾� ������Ƽ �����ϴ� �Լ� -> Myself
        public static void SetPlayerProperty(string prop, object value)
        {
            ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable 
            {
                { prop, value }
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);

            Debug.Log($"[PMS_Util] �÷��̾� {PhotonNetwork.LocalPlayer.NickName} ������Ƽ '{prop}' = {value} ���� �Ϸ�");
        }

        public static string TryGetUidFromActor(int actorNumber)
        {
            //���� �� �� or ��������� Room�� �������� ����
            if (!PhotonNetwork.IsConnected || (!PhotonNetwork.InRoom))
            {
                Debug.Log("���� �ش� Ŭ���̾�Ʈ�� üũ�� Ȯ�� �� �� ���� �����Դϴ�.");
                return null;
            }

            // ���� �濡 �ִ� �÷��̾� �� ActorNumber�� ���� �÷��̾ ã��
            var p = PhotonNetwork.PlayerList.FirstOrDefault(x => x.ActorNumber == actorNumber);                         //using System.Linq; �߰� �ؾ� FirstOrDefault ��밡��

            // ã�� �÷��̾��� CustomProperties���� "uid" Ű ������
            if (p != null && p.CustomProperties != null && p.CustomProperties.TryGetValue("uid", out var uidObj))
            {
                return uidObj as string;
            }
            return null;
        }
    }
}
