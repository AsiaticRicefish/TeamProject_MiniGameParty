using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Firebase;
using Firebase.Firestore;
using LDH_Util;
using UnityEngine;
using static LDH_Util.Define_LDH;

namespace Data
{
    public class FirestoreItemRepository
    {
        private readonly FirebaseFirestore _db;
        
        public FirestoreItemRepository(FirebaseFirestore db)
        {
            _db = db;
        }
        
        private CollectionReference ItemsRoot => _db.Collection("items");
        
        private CollectionReference Entries(ItemType type)
            => ItemsRoot.Document(type.ToString()).Collection("entries");
        
        private DocumentReference TypeDoc(ItemType type)
            => ItemsRoot.Document(type.ToString()); // 메타데이터 읽기
        
        
        /// 특정 타입 전체 항목 (한 번만 읽기!)
        public async UniTask<List<ItemData>> LoadEntriesAsync(ItemType type, CancellationToken ct = default)
        {
            var snap = await Entries(type).GetSnapshotAsync().AsUniTask().AttachExternalCancellation(ct);
            
            var list = new List<ItemData>(snap.Count);
            
            foreach (var doc in snap.Documents)
            {
                try
                {
                    var data = doc.ConvertTo<ItemData>();
                    data.id = doc.Id; // 안전하게 한번 더
                    list.Add(data);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[FirestoreItemRepository] Parse error {doc.Id}: {e.Message}");
                }
            }
            return list;
        }
        
        /// 실시간 구독 (변경 시 자동 업데이트)
        /// 반환되는 IDisposable을 보관했다가 필요 없으면 Dispose() 호출
        public IDisposable SubscribeEntries(
            ItemType type,
            Action<IReadOnlyList<ItemData>> onChanged,
            Action<Exception> onError = null)
        {
            return Entries(type).Listen(snapshot =>
            {
                try
                {
                    var list = new List<ItemData>(snapshot.Count);
                    foreach (var doc in snapshot.Documents)
                    {
                        var data = doc.ConvertTo<ItemData>();
                        data.id = doc.Id;
                        list.Add(data);
                    }
                    onChanged?.Invoke(list);
                }
                catch (Exception e)
                {
                    onError?.Invoke(e);
                }
            });
        }
        
        /// 상단 타입 도큐먼트의 updatedAt을 읽음 (증분 로직에 활용)
        public async UniTask<long> ReadTypeUpdatedAtAsync(ItemType type, CancellationToken ct = default)
        {
            var doc = await TypeDoc(type).GetSnapshotAsync().AsUniTask().AttachExternalCancellation(ct);
            if (!doc.Exists) return 0;
            if (doc.TryGetValue<long>("updatedAt", out var ts)) return ts;
            return 0;
        }
        
        
       
    }
}