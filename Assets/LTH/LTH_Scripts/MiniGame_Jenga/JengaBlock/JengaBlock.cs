using System;
using System.Collections;
using System.Collections.Generic;
using MiniGameJenga;
using Photon.Pun;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 단일 블록의 입력/표현/요청만 담당
/// - 선택(1차 클릭), 타이밍 시작(2차 클릭) 요청
/// - 타이밍 결과 수신 → 마스터에 제거 요청
/// - 실제 제거 적용은 네트워크 브로드캐스트 수신 시 실행
/// </summary>

[Serializable]
public class JengaBlock : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    #region 식별/상태
    public int BlockId { get; private set; }
    public int Layer { get; private set; }
    public int IndexInLayer { get; private set; }
    public bool IsRemoved { get; private set; }

    // 소유자 표준화: 네트워크 타깃팅은 ActorNumber, 게임 로직 키는 UID
    public int OwnerActorNumber { get; private set; }
    public string OwnerUid { get; private set; }
    #endregion

    #region 입력 상태
    private bool _isSelected = false;     // 1차(선택) / 2차(타이밍 시작) 클릭 구분
    private bool _interactable = true;   // 상태에 따라 외부에서 제어
    private bool _busy = false;         // 타이밍 진행 중엔 추가 입력 잠금
    #endregion

    #region 물리/렌더 캐시
    private Rigidbody _rb;
    private Collider _col;
    private Renderer _renderer;
    private MaterialPropertyBlock _mpb;
    private Color _baseColor = Color.white;
    private static readonly int COLOR_ID = Shader.PropertyToID("_Color");
    #endregion

    #region 외부 이벤트
    public static event Action<JengaBlock> OnAnyBlockSelected;      // 1차 클릭
    public static event Action<JengaBlock> OnAnyBlockTimingStart;   // 2차 클릭
    #endregion

    private bool _pendingRemoval = false; // 제거 요청 sent, 서버 승인 대기

    // 사이드 “쌍 제거 안내”를 위한 반대편 하이라이트(세션 시작 전, 보기용)
    private JengaBlock _pairedTargetPreview;

    private void Awake()
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
    }

    // 필요한 최소 구성(Rigidbody/Collider/Renderer)이 없다면 안전하게 보강
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

    // 턴 전환/게임 상태에 따라 외부에서 호출
    public void SetInteractable(bool canInteract)
    {
        _interactable = canInteract && !IsRemoved;
        if (!_interactable) ClearSelection();
    }

    #region Pointer Handlers

    /// <summary>
    /// 젠가 클릭 시 처리
    /// 1차 클릭: 블록 선택
    /// 2차 클릭: 타이밍 시작
    /// </summary>
    public void OnPointerClick(PointerEventData eventData) // 클릭/탭 완료 이벤트
    {
        if (!_interactable || _busy || _pendingRemoval || IsRemoved) return; 

        // 타이밍 매니저가 이미 실행 중이면 무시
        if (JengaTimingManager.Instance._isTimingActive) return;

        // 내 타워의 블록만 조작 가능
        if (OwnerActorNumber != PhotonNetwork.LocalPlayer.ActorNumber) return;

        var tower = JengaTowerManager.Instance?.GetPlayerTower(OwnerActorNumber);

        if (tower == null) return;

        //  1) 세션 중인 레이어라면: 기대 사이드만 허용
        if (tower.IsPairSessionActiveOn(Layer))
        {
            if (!tower.CanRemoveBlock(this)) return; // 기대 사이드가 아니면 불가

            if (!_isSelected)
            {
                _isSelected = true; Highlight(true);
                OnAnyBlockSelected?.Invoke(this);
            }
            else
            {
                if (_busy || IsRemoved) return;
                _isSelected = false; Highlight(false);
                OnAnyBlockTimingStart?.Invoke(this);
                _busy = true;
            }
            return;
        }

        // 2) 평상시: CanRemoveBlock == true → 센터만(3개 상태)
        if (tower.CanRemoveBlock(this))
        {
            if (!_isSelected)
            {
                _isSelected = true; Highlight(true);
                OnAnyBlockSelected?.Invoke(this);
            }
            else
            {
                if (_busy || IsRemoved) return;
                _isSelected = false; Highlight(false);
                OnAnyBlockTimingStart?.Invoke(this);
                _busy = true;
            }
            return;
        }

        // 3) 평상시인데 CanRemoveBlock==false 이면서 "사이드 + 3개 상태" → 세션 예고(보기용
        var aliveInLayer = tower.allBlocks
            .FindAll(x => x.Layer == Layer && !x.IsRemoved);

        bool threeAlive = aliveInLayer.Count == 3;
        bool isSide = (IndexInLayer == 0 || IndexInLayer == 2);

        if (threeAlive && isSide)
        {
            // 세션 예고: 반대편 사이드 하이라이트(시각 안내)
            _pairedTargetPreview = tower.GetOppositeSideInLayer(Layer, IndexInLayer);
            if (_pairedTargetPreview == null || _pairedTargetPreview.IsRemoved) return;

            _isSelected = true;
            Highlight(true);
            _pairedTargetPreview.Highlight(true);
            OnAnyBlockSelected?.Invoke(this); // UI : "사이드 연속 제거" 안내
            return;
        }

        #region Don't use this code
        //if (!tower.CanRemoveBlock(this)) return;

        //// 제거 가능 블록인지 검사
        //var canRemove = tower.GetRemovableBlocks().Contains(this);
        //if (!canRemove) return;

        //if (!_isSelected)
        //{
        //    // 1차 클릭: 선택
        //    _isSelected = true;
        //    Highlight(true);
        //    OnAnyBlockSelected?.Invoke(this); // UI: 다시 클릭하면 타이밍 시작
        //    // ToDo : 클릭 시 사운드 재생
        //}
        //else
        //{
        //    // 2차 클릭: 타이밍 로직 시작
        //    if (_busy || IsRemoved) return; // 이미 타이밍 중이거나 제거된 블록은 무시
        //    _isSelected = false;
        //    Highlight(false);
        //    OnAnyBlockTimingStart?.Invoke(this); // JengaTimingManager에서 타이밍 UI 시작

        //    _busy = true;
        //}
        #endregion
    }

    public void OnPointerDown(PointerEventData eventData) // 마우스 클릭 시작 / 터치 시작
    {
        if (!_interactable || _busy || _pendingRemoval || IsRemoved) return;
        if (OwnerActorNumber != PhotonNetwork.LocalPlayer.ActorNumber) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        var tower = JengaTowerManager.Instance?.GetPlayerTower(OwnerActorNumber);
        if (tower != null && tower.CanRemoveBlock(this))
        {
            Highlight(true);
        }
    }

    public void OnPointerUp(PointerEventData eventData) // 마우스 버튼 뗌 / 터치 종료
    {
        if (!_isSelected) Highlight(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_interactable || _pendingRemoval || IsRemoved) return;
        if (OwnerActorNumber != PhotonNetwork.LocalPlayer.ActorNumber) return;

        var tower = JengaTowerManager.Instance?.GetPlayerTower(OwnerActorNumber);
        if (tower != null && tower.CanRemoveBlock(this) && !_isSelected)
        {
            Highlight(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!_interactable || IsRemoved) return;
        if (!_isSelected) Highlight(false);
    }
    #endregion


    #region 타이밍 완료 콜백 → 마스터 승인 요청
    /// <summary>
    /// 타이밍 매니저에서 호출되는 콜백
    /// - 여기서는 점수 간이 계산만 하고, “마스터에 제거 요청”만 보낸다.
    /// - 최종 적용(제거/애니)은 마스터 승인 후 브로드캐스트에서 처리.
    /// </summary>
    public void ApplyTimingResult(bool success, float accuracy)
    {
        _busy = false;
        Highlight(false);

        _pairedTargetPreview?.Highlight(false); // 세션 예고 하이라이트 해제
        _pairedTargetPreview = null;

        if (IsRemoved) return;

        if (!success)
        {
             // 실패 사실을 마스터에게 요청 (누구 타워인지도 함께)
             JengaNetworkManager.Instance?.RequestTowerCollapse_MasterAuth(OwnerActorNumber);
        }

        // 성공 처리: 블록 제거 ‘요청’만 마스터에게 보냄
        _pendingRemoval = true;

        var playerId = PhotonNetwork.LocalPlayer.ActorNumber;

        int baseScore = 10;
        int bonusScore = Mathf.RoundToInt(Mathf.Clamp01(accuracy) * 10f);
        int totalScore = baseScore + bonusScore;

        // 타이밍 실패 시 타워 붕괴
        var tower = JengaTowerManager.Instance?.GetPlayerTower(OwnerActorNumber);
        if (tower == null) return;

        // 마스터 승인 루트로 변경
        JengaNetworkManager.Instance?.RequestBlockRemoval_MasterAuth(
            OwnerActorNumber, BlockId, totalScore, accuracy
        );

        // 1) 첫 사이드 성공(3개 상태에서 사이드였음) → 세션 시작
        //    로컬은 아직 제거 적용 전이므로 "현재도 3개"로 보일 확률이 높음.
        //    인덱스 기반으로만 판단해도 충분(센터는 IndexInLayer==1 이고, 사이드는 0/2).
        var isSide = (IndexInLayer == 0 || IndexInLayer == 2);
        if (isSide && !tower.IsPairSessionActiveOn(Layer))
        {
            // 첫 사이드 성공 → 해당 레이어 세션 시작(센터 잠금 + 반대편 사이드만 허용)
            tower.BeginPairSession(Layer, IndexInLayer);
        }
        else
        {
            // 2) 세션 중 기대 사이드 성공 → 세션 종료
            var expected = tower.GetExpectedSide(Layer);
            if (expected.HasValue && IndexInLayer == expected.Value)
            {
                tower.EndPairSession(Layer);
            }
        }
    }
    #endregion

    #region 네트워크 수신 시 실제 적용 (JengaNetworkManager.RPC_ApplyBlockRemoval에서 호출)
    public void RemoveWithAnimation(bool isSuccess = true)
    {
        if (IsRemoved && !gameObject.activeSelf) return;
        IsRemoved = true;

        if (isSuccess)
        {
            StartCoroutine(RemoveAnimationSuccess());
        }
        else
        {
            StartCoroutine(RemoveAnimationFail());
        }
    }

    public void RemoveImmediately()
    {
        if (IsRemoved) return;
        IsRemoved = true;

        gameObject.SetActive(false);
    }

    private IEnumerator RemoveAnimationSuccess()
    {
        // 임시: 간단하게 바로 사라지기
        yield return new WaitForSeconds(0.1f);
        gameObject.SetActive(false);

        // ToDo : 나중에 캐릭터 발사 연출로 교체 예정
    }

    private IEnumerator RemoveAnimationFail()
    {
        if (_rb) _rb.isKinematic = false;

        var tower = GetComponentInParent<JengaTower>();
        Vector3 dir = Vector3.up;
        if (tower) dir = (transform.position - tower.transform.position).normalized;

        _rb?.AddForce(dir * 5f, ForceMode.Impulse);
        _rb?.AddTorque(UnityEngine.Random.insideUnitSphere * 2f, ForceMode.Impulse);

        yield return new WaitForSeconds(3f);
        gameObject.SetActive(false);
    }
    #endregion

    #region 시각 보조
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
        _pairedTargetPreview?.Highlight(false);
        _pairedTargetPreview = null;
    }
    #endregion
}