using System;
using System.Collections.Generic;
using Photon.Pun;
using DesignPattern;
using ExitGames.Client.Photon;
using UnityEngine;


public class RoomPropertyObserver : PunSingleton<RoomPropertyObserver>, IGameComponent
{
    private Dictionary<string, Action<object>> _observers = new();
    private Dictionary<string, (string key, Action<object> callback)> _observerIds = new(); // ID로 추적

    protected override void OnAwake()
    {
        isPersistent = false;
    }

    /// <summary>
    /// 특정 RoomProperty Key를 구독
    /// </summary>
    public string RegisterObserver(string key, Action<object> callback)
    {
        string observerId = Guid.NewGuid().ToString();          // GUID를 통한 고유 ID 생성(string)

        if (_observers.ContainsKey(key))
            _observers[key] += callback;
        else
            _observers[key] = callback;

        _observerIds[observerId] = (key, callback);             //Dictionary<id,(key,callback)>로 저장. key -> id , value -> (key,callback)

        return observerId;                                      // ID 반환해서 나중에 해제할 때 사용
    }

    /// <summary>
    /// ID로 특정 Observer 해제 return값: 성공여부
    /// </summary>
    /// <param name="id">등록 시 반환된 Observer ID</param>
    /// <returns>해제 성공 시 true, 실패 시 false</returns>
    public bool UnregisterObserverById(string observerId)
    {
        //발행받은 ID를 가지고 Observer을 해제하는 형식이다. 
        if (_observerIds.TryGetValue(observerId, out (string key, Action<object> callback) info))        //var로 가능 
        {
            string key = info.key;                                      
            Action<object> callback = info.callback;

            // 실제 observer에서 해당 콜백 제거
            if (_observers.ContainsKey(key))
            {
                _observers[key] -= callback;
                if (_observers[key] == null)
                    _observers.Remove(key);
            }

            _observerIds.Remove(observerId);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 특정 RoomProperty Key 구독 해제
    /// </summary>
    public void UnregisterObserver(string key, Action<object> callback)
    {
        if (_observers.ContainsKey(key))
        {
            _observers[key] -= callback;
            if (_observers[key] == null)
                _observers.Remove(key);

            // _observerIds에서도 해당 콜백을 찾아서 제거
            var toRemove = new List<string>();
            foreach (var kvp in _observerIds)
            {
                if (kvp.Value.key == key && kvp.Value.callback.Equals(callback))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var id in toRemove)
            {
                _observerIds.Remove(id);
            }

        }
    }

    /// <summary>
    /// 특정 Key의 모든 Observer 해제
    /// </summary>
    public void UnregisterAllObservers(string key)
    {
        if (_observers.ContainsKey(key))
        {
            _observers.Remove(key);

            // _observerIds에서도 해당 key의 모든 observer 제거
            var toRemove = new List<string>();
            foreach (var kvp in _observerIds)
            {
                if (kvp.Value.key == key)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var id in toRemove)
            {
                _observerIds.Remove(id);
            }
        }
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        foreach (var prop in propertiesThatChanged)
        {
            string key = prop.Key.ToString();
            if (_observers.TryGetValue(key, out var callback))
            {
                callback?.Invoke(prop.Value);
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
    /// 룸 프로퍼티 설정
    /// </summary>
    public void SetRoomProperty(string key, object value)
    {
        if (PhotonNetwork.InRoom && key != null && value != null)
        {
            var props = new Hashtable { { key, value } };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        }
    }

    /// <summary>
    /// 여러 개의 룸 프로퍼티 설정
    /// </summary>
    /// <param name="properties">Key-Value 쌍</param>
    public void SetRoomProperties(Dictionary<string,object> properties)
    {
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
        _observers.Clear();         //개인적인 _observers clear해줘야함
        _observerIds.Clear();
        Destroy(gameObject);
    }

    public void Initialize()
    {
        Debug.Log("Room property observer 초기화 ");
    }
}