using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using System;

/// <summary>
/// 타이밍 결과에 따라 캐릭터를 발사하여 젠가 블록을 미는 연출 담당
/// </summary>
public class JengaCharacterLauncher : MonoBehaviour
{
    [Header("캐릭터 설정")]
    [SerializeField] private GameObject characterPrefab; // 기본 캐릭터
    [SerializeField] private Transform launchPoint; // 발사 시작 위치
    [SerializeField] private float launchSpeed = 10f;
    [SerializeField] private float launchArcHeight = 2f; // 포물선 높이

    [Header("연출 설정")]
    [SerializeField] private float successImpactForce = 8f;
    [SerializeField] private float failureDelay = 1f; // 실패 시 타워 붕괴까지 지연시간

    // 플레이어별 캐릭터 프리팹 매핑
    private Dictionary<string, GameObject> playerCharacterPrefabs = new();

    public void Initialize(int ownerActorNumber)
    {
        // 플레이어별 캐릭터 프리팹 로드
        LoadPlayerCharacterPrefabs();

        // 발사 지점 설정 (타워 앞쪽)
        if (launchPoint == null)
        {
            var launchGO = new GameObject("LaunchPoint");
            launchGO.transform.SetParent(transform);
            launchGO.transform.localPosition = Vector3.forward * 3f + Vector3.up * 1f;
            launchPoint = launchGO.transform;
        }
    }

    /// <summary>
    /// 타이밍 결과에 따른 캐릭터 발사
    /// </summary>
    public void LaunchCharacter(JengaBlock targetBlock, bool success, float accuracy, Action onComplete = null)
    {
        StartCoroutine(LaunchCharacterCoroutine(targetBlock, success, accuracy, onComplete));
    }

    private IEnumerator LaunchCharacterCoroutine(JengaBlock targetBlock, bool success, float accuracy, Action onComplete)
    {
        // 1. 캐릭터 생성
        var characterPrefab = GetCharacterPrefabForPlayer(targetBlock.OwnerUid);
        var character = Instantiate(characterPrefab, launchPoint.position, launchPoint.rotation);

        // 2. 목표 지점 설정
        Vector3 targetPos = success ?
            GetSuccessTargetPosition(targetBlock) :
            GetFailureTargetPosition(targetBlock);

        // 3. 발사 애니메이션
        yield return StartCoroutine(AnimateCharacterFlight(character, targetPos, success));

        // 4. 충돌 연출
        if (success)
        {
            yield return StartCoroutine(HandleSuccessImpact(character, targetBlock, accuracy));
        }
        else
        {
            yield return StartCoroutine(HandleFailureImpact(character, targetBlock));
        }

        // 5. 정리
        Destroy(character, 2f);
        onComplete?.Invoke();
    }

    private IEnumerator AnimateCharacterFlight(GameObject character, Vector3 targetPos, bool success)
    {
        Vector3 startPos = character.transform.position;
        float duration = success ? 1.5f : 1.2f; // 성공 시 더 정확한 궤적
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;

            // 포물선 궤적 계산
            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, t);
            currentPos.y += Mathf.Sin(t * Mathf.PI) * launchArcHeight;

            character.transform.position = currentPos;

            // 비행 방향으로 회전
            Vector3 direction = (targetPos - startPos).normalized;
            character.transform.rotation = Quaternion.LookRotation(direction);

            elapsed += Time.deltaTime;
            yield return null;
        }

        character.transform.position = targetPos;
    }

    private IEnumerator HandleSuccessImpact(GameObject character, JengaBlock targetBlock, float accuracy)
    {
        // 캐릭터가 블록에 충돌하여 밀어내는 연출

        // 블록을 밀어내는 방향 계산
        Vector3 pushDirection = GetBlockPushDirection(targetBlock);

        // 정확도에 따른 힘 조절
        float finalForce = successImpactForce * Mathf.Lerp(0.8f, 1.2f, accuracy);

        // 블록에 물리 적용
        var rb = targetBlock.GetComponent<Rigidbody>();
        if (rb)
        {
            rb.isKinematic = false;
            rb.AddForce(pushDirection * finalForce, ForceMode.Impulse);

            // 약간의 회전력도 추가 (자연스러운 연출)
            rb.AddTorque(UnityEngine.Random.insideUnitSphere * 2f, ForceMode.Impulse);
        }

        // 성공 이펙트/사운드
        // JengaEffectManager.Instance?.PlaySuccessEffect(targetBlock.transform.position);
        // JengaSoundManager.Instance?.PlayBlockHitSound();

        yield return new WaitForSeconds(0.5f);
    }

    private IEnumerator HandleFailureImpact(GameObject character, JengaBlock targetBlock)
    {
        // 캐릭터가 다른 곳에 부딪치거나 빗나가는 연출

        // 실패 이펙트
        // JengaEffectManager.Instance?.PlayFailureEffect(character.transform.position);
        // JengaSoundManager.Instance?.PlayMissSound();

        yield return new WaitForSeconds(failureDelay);

        // 타워 붕괴 트리거
        var tower = JengaTowerManager.Instance?.GetPlayerTower(targetBlock.OwnerActorNumber);
        tower?.TriggerCollapseOnce();

        // 네트워크 알림
        if (PhotonNetwork.IsMasterClient)
        {
            
        }
    }

    private Vector3 GetSuccessTargetPosition(JengaBlock targetBlock)
    {
        // 블록 바로 앞 위치 (정확한 타격 지점)
        Vector3 blockPos = targetBlock.transform.position;
        Vector3 launchDir = (blockPos - launchPoint.position).normalized;
        return blockPos - launchDir * 0.5f; // 블록 앞쪽
    }

    private Vector3 GetFailureTargetPosition(JengaBlock targetBlock)
    {
        // 블록을 빗나가는 위치 (랜덤하게 어긋남)
        Vector3 blockPos = targetBlock.transform.position;
        Vector3 randomOffset = new Vector3(
            UnityEngine.Random.Range(-1.5f, 1.5f),
            UnityEngine.Random.Range(-0.5f, 0.5f),
            UnityEngine.Random.Range(-1f, 1f)
        );
        return blockPos + randomOffset;
    }

    private Vector3 GetBlockPushDirection(JengaBlock targetBlock)
    {
        // 타워 중심에서 바깥쪽으로 밀어내는 방향
        var tower = targetBlock.GetComponentInParent<JengaTower>();
        if (tower)
        {
            Vector3 towerToBlock = (targetBlock.transform.position - tower.transform.position).normalized;
            towerToBlock.y = 0; // 수평 방향만
            return towerToBlock;
        }

        return Vector3.forward; // 기본값
    }

    private void LoadPlayerCharacterPrefabs()
    {
        // ToDo :플레이어별 캐릭터 프리팹 로드 로직
        // 예: Resources 폴더나 Addressable 시스템 사용

        // 임시 테스트 로직
        playerCharacterPrefabs["player1"] = Resources.Load<GameObject>("Characters/Character1");
        playerCharacterPrefabs["player2"] = Resources.Load<GameObject>("Characters/Character2");
    }

    private GameObject GetCharacterPrefabForPlayer(string playerUid)
    {
        if (!string.IsNullOrEmpty(playerUid) &&
            playerCharacterPrefabs.TryGetValue(playerUid, out var prefab))
        {
            return prefab;
        }
        return characterPrefab; // 기본 캐릭터
    }
}