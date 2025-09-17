using Firebase.Firestore;
using UnityEngine;

namespace Data
{
    /// <summary>
    /// firestore 구조
    /// items/{typeDoc}
    ///  |----- entries/{itemId}
    /// </summary>
    public class ItemData
    {
        [FirestoreDocumentId] public string id { get; set; }
        [FirestoreProperty] public string name { get; set; } 
        [FirestoreProperty] public long price { get; set; }
        [FirestoreProperty] public bool enabled { get; set; } 
        [FirestoreProperty] public long updatedAt { get; set; }
        
    }
}