// using Firebase.Database;
// using UnityEngine;
//
// namespace LDH.LDH_Scripts.Test
// {
//     public class RTDBTest : MonoBehaviour
//     {
//         public static async void StartTest()
//         {
//             // 1) RTDB 준비 완료 대기 (AuthBootstrapper가 BindDatabase 한 뒤 신호)
//             await BackendManager.WhenDatabaseReady();
//
//             // 2) UID 확보
//             var uid = BackendManager.Auth.CurrentUser.UserId;
//
//
//             // 3) 내 사용자 커스터마이징 경로 참조
//             // DatabaseReference custRef = BackendManager.UserCustomizationRef(uid);
//
//
//             // 4) 쓰기 (규칙에 맞는 필드만)
//             await custRef.Child("characterId").SetValueAsync("unimo_ch_001");
//             await custRef.Child("updatedAt").SetValueAsync(ServerValue.Timestamp);
//             //SetValueAsync(...) : 해당 위치에 값 쓰기(비동기), 서버 기준 UTC의 유닉스 타임스탬프(ms) 로 저장
//
//
//             // 5) 읽기
//             var snap = await custRef.GetValueAsync();
//             var readChar = snap.Child("characterId").Value as string;
//             var readTS = snap.Child("updatedAt").Value;
//             var updatedAtMs = System.Convert.ToInt64(readTS); // ms 단위
//             var updatedAtUtc = System.DateTimeOffset.FromUnixTimeMilliseconds(updatedAtMs).UtcDateTime; 
//             // DateTimeOffset.FromUnixTimeMilliseconds(ms)는 그 값을 UTC 기준의 절대 시점으로 해석
//             // .UtcDateTime은 UTC DateTime(Kind=Utc)으로 변환
//
//             Debug.Log($"[SmokeTest] characterId={readChar}, updatedAt(ms)={updatedAtMs}, utc={updatedAtUtc:O}");
//
//
//         }
//     }
// }