using System;
using System.Collections.Generic;
using Photon.Pun;
using DesignPattern;
using ExitGames.Client.Photon;
using UnityEngine;


public class RoomPropertyObserver : PunSingleton<RoomPropertyObserver>, IGameComponent
{
    private Dictionary<string, Action<object>> _observers = new();

    protected override void OnAwake()
    {
        isPersistent = false;
    }

    /// <summary>
    /// 특정 RoomProperty Key를 구독
    /// </summary>
    public void RegisterObserver(string key, Action<object> callback)
    {
        if (_observers.ContainsKey(key))
            _observers[key] += callback;
        else
            _observers[key] = callback;
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
        Destroy(gameObject);
    }

    public void Initialize()
    {
        Debug.Log("Room property observer 초기화 ");
    }
}