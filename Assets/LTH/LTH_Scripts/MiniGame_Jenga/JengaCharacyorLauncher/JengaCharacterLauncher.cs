using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using System;

/// <summary>
/// Ÿ�̹� ����� ���� ĳ���͸� �߻��Ͽ� ���� ����� �̴� ���� ���
/// </summary>
public class JengaCharacterLauncher : MonoBehaviour
{
    [Header("ĳ���� ����")]
    [SerializeField] private GameObject characterPrefab; // �⺻ ĳ����
    [SerializeField] private Transform launchPoint; // �߻� ���� ��ġ
    [SerializeField] private float launchSpeed = 10f;
    [SerializeField] private float launchArcHeight = 2f; // ������ ����

    [Header("���� ����")]
    [SerializeField] private float successImpactForce = 8f;
    [SerializeField] private float failureDelay = 1f; // ���� �� Ÿ�� �ر����� �����ð�

    // �÷��̾ ĳ���� ������ ����
    private Dictionary<string, GameObject> playerCharacterPrefabs = new();

    public void Initialize(int ownerActorNumber)
    {
        // �÷��̾ ĳ���� ������ �ε�
        LoadPlayerCharacterPrefabs();

        // �߻� ���� ���� (Ÿ�� ����)
        if (launchPoint == null)
        {
            var launchGO = new GameObject("LaunchPoint");
            launchGO.transform.SetParent(transform);
            launchGO.transform.localPosition = Vector3.forward * 3f + Vector3.up * 1f;
            launchPoint = launchGO.transform;
        }
    }

    /// <summary>
    /// Ÿ�̹� ����� ���� ĳ���� �߻�
    /// </summary>
    public void LaunchCharacter(JengaBlock targetBlock, bool success, float accuracy, Action onComplete = null)
    {
        StartCoroutine(LaunchCharacterCoroutine(targetBlock, success, accuracy, onComplete));
    }

    private IEnumerator LaunchCharacterCoroutine(JengaBlock targetBlock, bool success, float accuracy, Action onComplete)
    {
        // 1. ĳ���� ����
        var characterPrefab = GetCharacterPrefabForPlayer(targetBlock.OwnerUid);
        var character = Instantiate(characterPrefab, launchPoint.position, launchPoint.rotation);

        // 2. ��ǥ ���� ����
        Vector3 targetPos = success ?
            GetSuccessTargetPosition(targetBlock) :
            GetFailureTargetPosition(targetBlock);

        // 3. �߻� �ִϸ��̼�
        yield return StartCoroutine(AnimateCharacterFlight(character, targetPos, success));

        // 4. �浹 ����
        if (success)
        {
            yield return StartCoroutine(HandleSuccessImpact(character, targetBlock, accuracy));
        }
        else
        {
            yield return StartCoroutine(HandleFailureImpact(character, targetBlock));
        }

        // 5. ����
        Destroy(character, 2f);
        onComplete?.Invoke();
    }

    private IEnumerator AnimateCharacterFlight(GameObject character, Vector3 targetPos, bool success)
    {
        Vector3 startPos = character.transform.position;
        float duration = success ? 1.5f : 1.2f; // ���� �� �� ��Ȯ�� ����
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;

            // ������ ���� ���
            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, t);
            currentPos.y += Mathf.Sin(t * Mathf.PI) * launchArcHeight;

            character.transform.position = currentPos;

            // ���� �������� ȸ��
            Vector3 direction = (targetPos - startPos).normalized;
            character.transform.rotation = Quaternion.LookRotation(direction);

            elapsed += Time.deltaTime;
            yield return null;
        }

        character.transform.position = targetPos;
    }

    private IEnumerator HandleSuccessImpact(GameObject character, JengaBlock targetBlock, float accuracy)
    {
        // ĳ���Ͱ� ��Ͽ� �浹�Ͽ� �о�� ����

        // ����� �о�� ���� ���
        Vector3 pushDirection = GetBlockPushDirection(targetBlock);

        // ��Ȯ���� ���� �� ����
        float finalForce = successImpactForce * Mathf.Lerp(0.8f, 1.2f, accuracy);

        // ��Ͽ� ���� ����
        var rb = targetBlock.GetComponent<Rigidbody>();
        if (rb)
        {
            rb.isKinematic = false;
            rb.AddForce(pushDirection * finalForce, ForceMode.Impulse);

            // �ణ�� ȸ���µ� �߰� (�ڿ������� ����)
            rb.AddTorque(UnityEngine.Random.insideUnitSphere * 2f, ForceMode.Impulse);
        }

        // ���� ����Ʈ/����
        // JengaEffectManager.Instance?.PlaySuccessEffect(targetBlock.transform.position);
        // JengaSoundManager.Instance?.PlayBlockHitSound();

        yield return new WaitForSeconds(0.5f);
    }

    private IEnumerator HandleFailureImpact(GameObject character, JengaBlock targetBlock)
    {
        // ĳ���Ͱ� �ٸ� ���� �ε�ġ�ų� �������� ����

        // ���� ����Ʈ
        // JengaEffectManager.Instance?.PlayFailureEffect(character.transform.position);
        // JengaSoundManager.Instance?.PlayMissSound();

        yield return new WaitForSeconds(failureDelay);

        // Ÿ�� �ر� Ʈ����
        var tower = JengaTowerManager.Instance?.GetPlayerTower(targetBlock.OwnerActorNumber);
        tower?.TriggerCollapseOnce();

        // ��Ʈ��ũ �˸�
        if (PhotonNetwork.IsMasterClient)
        {
            
        }
    }

    private Vector3 GetSuccessTargetPosition(JengaBlock targetBlock)
    {
        // ��� �ٷ� �� ��ġ (��Ȯ�� Ÿ�� ����)
        Vector3 blockPos = targetBlock.transform.position;
        Vector3 launchDir = (blockPos - launchPoint.position).normalized;
        return blockPos - launchDir * 0.5f; // ��� ����
    }

    private Vector3 GetFailureTargetPosition(JengaBlock targetBlock)
    {
        // ����� �������� ��ġ (�����ϰ� ��߳�)
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
        // Ÿ�� �߽ɿ��� �ٱ������� �о�� ����
        var tower = targetBlock.GetComponentInParent<JengaTower>();
        if (tower)
        {
            Vector3 towerToBlock = (targetBlock.transform.position - tower.transform.position).normalized;
            towerToBlock.y = 0; // ���� ���⸸
            return towerToBlock;
        }

        return Vector3.forward; // �⺻��
    }

    private void LoadPlayerCharacterPrefabs()
    {
        // ToDo :�÷��̾ ĳ���� ������ �ε� ����
        // ��: Resources ������ Addressable �ý��� ���

        // �ӽ� �׽�Ʈ ����
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
        return characterPrefab; // �⺻ ĳ����
    }
}