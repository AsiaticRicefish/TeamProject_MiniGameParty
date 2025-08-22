using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using System;
using UnityEngine.EventSystems;

/// <summary>
/// ���� ����� �Է�/ǥ��/��û�� ���
/// - ����(1�� Ŭ��), Ÿ�̹� ����(2�� Ŭ��) ��û
/// - Ÿ�̹� ��� ���� �� �����Ϳ� ���� ��û
/// - ���� ���� ������ ��Ʈ��ũ ��ε�ĳ��Ʈ ���� �� ����
/// </summary>

[Serializable]
public class JengaBlock : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    #region �ĺ�/����
    public int BlockId { get; private set; }
    public int Layer { get; private set; }
    public int IndexInLayer { get; private set; }
    public bool IsRemoved { get; private set; }

    // ������ ǥ��ȭ: ��Ʈ��ũ Ÿ������ ActorNumber, ���� ���� Ű�� UID
    public int OwnerActorNumber { get; private set; }
    public string OwnerUid { get; private set; }
    #endregion

    #region �Է� ����
    private bool _isSelected = false;     // 1��(����) / 2��(Ÿ�̹� ����) Ŭ�� ����
    private bool _interactable = true;   // ���¿� ���� �ܺο��� ����
    private bool _busy = false;         // Ÿ�̹� ���� �߿� �߰� �Է� ���
    #endregion

    #region ����/���� ĳ��
    private Rigidbody _rb;
    private Collider _col;
    private Renderer _renderer;
    private MaterialPropertyBlock _mpb;
    private Color _baseColor = Color.white;
    private static readonly int COLOR_ID = Shader.PropertyToID("_Color");
    #endregion

    #region �ܺ� �̺�Ʈ
    public static event Action<JengaBlock> OnAnyBlockSelected;      // 1�� Ŭ��
    public static event Action<JengaBlock> OnAnyBlockTimingStart;   // 2�� Ŭ��
    #endregion

    private void Awake()
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
    }

    // �ʿ��� �ּ� ����(Rigidbody/Collider/Renderer)�� ���ٸ� �����ϰ� ����
    private void EnsureCaches()
    {
        if (_rb == null) _rb = GetComponent<Rigidbody>() ?? gameObject.AddComponent<Rigidbody>();
        if (_col == null) _col = GetComponent<Collider>() ?? gameObject.AddComponent<BoxCollider>();
        if (_renderer == null) _renderer = GetComponent<Renderer>();
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
    }

    public void Initialize(int blockId, int layer, int indexInLayer, int ownerActorNumber, string ownerUid)
    {
        BlockId = blockId;
        Layer = layer;
        IndexInLayer = indexInLayer;
        OwnerActorNumber = ownerActorNumber;
        OwnerUid = ownerUid;
        IsRemoved = false;

        EnsureCaches();

        _rb.isKinematic = true;

        if (_renderer != null && _renderer.sharedMaterial != null && _renderer.sharedMaterial.HasProperty(COLOR_ID))
        {
            _renderer.GetPropertyBlock(_mpb);
            _baseColor = _renderer.sharedMaterial.color;
        }
    }

    // �� ��ȯ/���� ���¿� ���� �ܺο��� ȣ��
    public void SetInteractable(bool canInteract)
    {
        _interactable = canInteract && !IsRemoved;
        if (!_interactable) ClearSelection();
    }

    #region Pointer Handlers

    /// <summary>
    /// ���� Ŭ�� �� ó��
    /// 1�� Ŭ��: ��� ����
    /// 2�� Ŭ��: Ÿ�̹� ����
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_interactable || _busy || IsRemoved) return;

        // �� Ÿ���� ��ϸ� ���� ����
        if (OwnerActorNumber != PhotonNetwork.LocalPlayer.ActorNumber) return;

        var tower = JengaTowerManager.Instance?.GetPlayerTower(OwnerActorNumber);
        if (tower == null) return;

        // ���� ���� ������� �˻�
        var canRemove = tower.GetRemovableBlocks().Contains(this);
        if (!canRemove) return;

        if (!_isSelected)
        {
            // 1�� Ŭ��: ����
            _isSelected = true;
            Highlight(true);
            OnAnyBlockSelected?.Invoke(this); // UI: �ٽ� Ŭ���ϸ� Ÿ�̹� ����
            // ToDo : Ŭ�� �� ���� ���
        }
        else
        {
            // 2�� Ŭ��: Ÿ�̹� ���� ����
            if (_busy || IsRemoved) return; // �̹� Ÿ�̹� ���̰ų� ���ŵ� ����� ����
            _isSelected = false;
            Highlight(false);
            OnAnyBlockTimingStart?.Invoke(this);

            _busy = true; 

            // ToDo : Ÿ�̹� ���� �� JengaTimingManager
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_interactable || IsRemoved) return;
        if (OwnerActorNumber != PhotonNetwork.LocalPlayer.ActorNumber) return;

        var tower = JengaTowerManager.Instance?.GetPlayerTower(OwnerActorNumber);
        if (tower != null && tower.GetRemovableBlocks().Contains(this) && !_isSelected)
            Highlight(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!_interactable || IsRemoved) return;
        if (!_isSelected) Highlight(false);
    }
    #endregion


    #region Ÿ�̹� �Ϸ� �ݹ� �� ������ ���� ��û
    /// <summary>
    /// Ÿ�̹� �Ŵ������� ȣ��Ǵ� �ݹ�
    /// - ���⼭�� ���� ���� ��길 �ϰ�, �������Ϳ� ���� ��û���� ������.
    /// - ���� ����(����/�ִ�)�� ������ ���� �� ��ε�ĳ��Ʈ���� ó��.
    /// </summary>
    private void OnTimingComplete(bool success, float accuracy)
    {
        _busy = false;
        Highlight(false);
        if (IsRemoved) return;

        var playerId = PhotonNetwork.LocalPlayer.ActorNumber;

        if (!success)
        {
            // ���д� UI�� ó��. �ʿ� �� ���� ���� ���� ���� �� ����.
            // JengaSoundManager.Instance?.PlayTimingFailSound();
            return;
        }
        // JengaSoundManager.Instance?.PlayTimingSuccessSound();

        // (������Ÿ��) ���� ���� ����
        int baseScore = 10;
        int bonusScore = Mathf.RoundToInt(Mathf.Clamp01(accuracy) * 10f);
        int totalScore = baseScore + bonusScore;

        // ������ ���� ��Ʈ�� ����
        JengaNetworkManager.Instance?.RequestBlockRemoval_MasterAuth(
            OwnerActorNumber, BlockId, totalScore, accuracy
        );
    }
    #endregion

    #region ��Ʈ��ũ ���� �� ���� ���� (JengaNetworkManager.RPC_ApplyBlockRemoval���� ȣ��)
    public void RemoveWithAnimation()
    {
        if (IsRemoved) return;
        IsRemoved = true;
        StartCoroutine(RemoveAnimation());
    }

    public void RemoveImmediately()
    {
        if (IsRemoved) return;
        IsRemoved = true;
        gameObject.SetActive(false);
    }

    private IEnumerator RemoveAnimation()
    {
        if (_rb) _rb.isKinematic = false;

        var tower = GetComponentInParent<JengaTower>();
        Vector3 dir = Vector3.up; // �⺻��
        if (tower) dir = (transform.position - tower.transform.position).normalized;

        _rb?.AddForce(dir * 5f, ForceMode.Impulse);
        _rb?.AddTorque(UnityEngine.Random.insideUnitSphere * 2f, ForceMode.Impulse);

        yield return new WaitForSeconds(3f);
        gameObject.SetActive(false);
    }
    #endregion

    #region �ð� ����
    private void Highlight(bool on)
    {
        if (_renderer == null) return;
        EnsureCaches();

        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetColor(COLOR_ID, on ? Color.yellow : _baseColor);
        _renderer.SetPropertyBlock(_mpb);
    }

    private void ClearSelection()
    {
        _isSelected = false;
        Highlight(false);
    }
    #endregion
}