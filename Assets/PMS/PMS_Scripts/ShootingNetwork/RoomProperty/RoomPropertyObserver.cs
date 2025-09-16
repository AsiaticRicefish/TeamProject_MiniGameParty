using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using DesignPattern;
using ExitGames.Client.Photon;
using UnityEngine;
using System.Linq;

class ObserverEntry
{
    public string Id;
    public Action<object> Callback;
}

public class RoomPropertyObserver : PunSingleton<RoomPropertyObserver>, IGameComponent
{
    private Dictionary<string, List<ObserverEntry>> _observersByKey 
        = new Dictionary<string, List<ObserverEntry>>();

    protected override void OnAwake()
    {
        isPersistent = false;
    }

    #region Legacy Code - 문제점:UnRegister 호출 문제
    /// <summary>
    /// 특정 RoomProperty Key를 구독
    /// </summary>
    public string RegisterObserver(string key, Action<object> callback)
    {
        string id = Guid.NewGuid().ToString();
        if (!_observersByKey.ContainsKey(key))
            _observersByKey[key] = new List<ObserverEntry>();

        _observersByKey[key].Add(new ObserverEntry { Id = id, Callback = callback });
        return id;
    }
    #endregion

    /// <summary>
    /// ID로 특정 Observer 해제 return값: 성공여부
    /// </summary>
    /// <param name="id">등록 시 반환된 Observer ID</param>
    /// <returns>해제 성공 시 true, 실패 시 false</returns>
    public bool UnregisterObserverById(string id)
    {
        foreach (var kv in _observersByKey)
        {
            var list = kv.Value;
            if (list.RemoveAll(o => o.Id == id) > 0)
            {
                if (list.Count == 0)
                    _observersByKey.Remove(kv.Key);
                return true;
            }
        }
        return false;
    }

    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable changed)
    {
        foreach (DictionaryEntry prop in changed)
        {
            string key = prop.Key.ToString();
            if (_observersByKey.TryGetValue(key, out var list))
            {
                var copy = list.ToList(); // 또는 list.ToArray()
                foreach (var obs in copy)
                    obs.Callback.Invoke(prop.Value);
            }
        }
    }

    /// <summary>
    /// 룸 프로퍼티 가져오기
    /// </summary>
    public object GetRoomProperty(string key)
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object value))
        {
            return value;
        }
        return null;
    }

    /// <summary>
    /// 마스터 클라이언트 전용 함수 - 룸 프로퍼티 설정
    /// </summary>
    public void SetRoomProperty(string key, object value)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        if (PhotonNetwork.InRoom && key != null && value != null)
        {
            var props = new ExitGames.Client.Photon.Hashtable { { key, value } };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        }
    }

    /// <summary>
    /// 마스터 클라이언트 전용 함수 - 여러 개의 룸 프로퍼티 설정
    /// </summary>
    /// <param name="properties">Key-Value 쌍</param>
    public void SetRoomProperties(Dictionary<string,object> properties)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        if (PhotonNetwork.InRoom && properties != null)
        {
            var props = new ExitGames.Client.Photon.Hashtable();
            foreach (var prop in properties)              
            {
                props.Add(prop.Key, prop.Value);
            }

            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        }
    }

    public override void OnLeftRoom()
    {
        _observersByKey.Clear();
        Destroy(gameObject);
    }

    public void Initialize()
    {
        Debug.Log("Room property observer 초기화 ");
    }
}