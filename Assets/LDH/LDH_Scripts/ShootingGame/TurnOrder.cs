using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Managers;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace LDH.LDH_Scripts.ShootingGame
{
    public class TurnOrder
    {
        private readonly LinkedList<GamePlayer> _list = new();
        private readonly Dictionary<string, LinkedListNode<GamePlayer>> _dicByUid = new();
        private LinkedListNode<GamePlayer> _current;
        public int Count => _list.Count;

        // public event Action<GamePlayer, GamePlayer> OnTurnChanged; // (prev, current)


        public GamePlayer Current => _current?.Value;
        public LinkedListNode<GamePlayer> CurrentNode => _current;

        
        public LinkedListNode<GamePlayer> NextNode()
        {
            if (_list.Count == 0)
            {
                Debug.LogError("linked list에 노드가 없습니다.");
                return null;
            }
            LinkedListNode<GamePlayer> nextNode = _current.Next ?? _list.First;
            while (nextNode.Value == null)
            {
                nextNode = nextNode.Next ?? _list.First;
            }

            return nextNode;
        }
    
    
        public void Clear()
        {
            _list.Clear();
            _dicByUid.Clear();
            _current = null;
        }
        


        public void InitFromActorOrder(int[] actorOrder)
        {
            Clear();

            for (int i = 0; i < actorOrder.Length; i++)
            {
                int currentActorNum = actorOrder[i];
                
                //현재 존재하는 플레이어인지 검증
                if(PhotonNetwork.CurrentRoom.GetPlayer(currentActorNum) == null) continue;
                
                int turnIndex = Array.IndexOf(actorOrder, currentActorNum) + 1;
                Player currentPlayer = PhotonNetwork.CurrentRoom.GetPlayer(currentActorNum);
                string currentPlayerUID = currentPlayer.CustomProperties["uid"].ToString();

                var gamePlayer = Manager.Player.GetPlayer(currentPlayerUID);
                
                if(gamePlayer==null) continue;
                
                gamePlayer.ShootingData.myTurnIndex = turnIndex;

                var node = _list.AddLast(gamePlayer);
                _dicByUid[currentPlayerUID] = node;
                
                
                //현재 게임 플레이어가 '나'면 커스텀 프로퍼티도 반영
                if (currentActorNum == PhotonNetwork.LocalPlayer.ActorNumber)
                {
                    var table = new Hashtable { { ShootingGamePlayerPropertyKeys.MyTurnIndex, turnIndex }};
                    PhotonNetwork.LocalPlayer.SetCustomProperties(table);
                }
            }

            _current = _list.Last;
        }

        public bool RemovePlayer(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return false;
            if (_dicByUid.TryGetValue(uid, out var node))
            {
                Debug.Log("노드 삭제");
                RemoveNode(node);
                return true;
            }
            return false;
        }
        private void RemoveNode(LinkedListNode<GamePlayer> node)
        {
            // if (node == _current)
            //     _current = node.Previous ?? _list.Last;
            if (!string.IsNullOrEmpty(node.Value.PlayerId))
            {
                string targetPlayerID = node.Value.PlayerId;
                //링크드 리스트에서 해당 플레이어 노드를 찾는다.
                var targetNode = _dicByUid[node.Value.PlayerId];
                
                //노드 내부 데이터를 비운다
                targetNode.Value = null;
                
                //딕셔너리에서 제거한다.
                _dicByUid.Remove(targetPlayerID);
            }
            
            if (_list.Count == 0) _current = null;
        }

        public void MoveToNext()
        {
            _current = NextNode();
        }

        public bool IsFirstNode(LinkedListNode<GamePlayer> node)
        {
            var firstNode = _list.First;
            if (_list.Count == 0)
            {
                Debug.LogError("list is empty!!!");
                return false;
            }
            while (firstNode?.Value == null)
            {
                firstNode = firstNode?.Next;
            }
            return node.Value.PlayerId == firstNode.Value.PlayerId;
        }

        public bool IsCurrentFirstNode()
        {
            return IsFirstNode(_current);
        }

        public int GetPlayerTurnIndex(string uid)
        {
            if (_dicByUid.TryGetValue(uid, out var node))
            {
                return node.Value.ShootingData.myTurnIndex;
            }

            else return -1;
        }
    }
}