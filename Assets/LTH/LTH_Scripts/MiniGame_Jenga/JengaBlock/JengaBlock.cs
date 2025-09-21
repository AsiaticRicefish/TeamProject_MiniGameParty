using System;
using System.Collections;
using System.Collections.Generic;
using InputBlocker;
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
public class JengaBlock : MonoBehaviour, IPointerClickHandler
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

    #region 캐시
    private Rigidbody _rb;
    private Collider _col;
    private BlockOutlineURP _outline;
    #endregion

    #region 외부 이벤트
    public static event Action<JengaBlock> OnAnyBlockSelected;      // 1차 클릭
    public static event Action<JengaBlock> OnAnyBlockTimingStart;   // 2차 클릭
    #endregion

    private bool _pendingRemoval = false; // 제거 요청 sent, 서버 승인 대기
    public bool IsCurrentlySelected => _isSelected;
    private JengaBlock _pairedTargetPreview;

    [SerializeField] private bool previewOppositeOnSide = false;

    private void EnsureCaches()
    {
        if (_rb == null) _rb = GetComponent<Rigidbody>() ?? gameObject.AddComponent<Rigidbody>();
        if (_col == null) _col = GetComponent<Collider>() ?? gameObject.AddComponent<BoxCollider>();
        if (_outline == null) _outline = GetComponent<BlockOutlineURP>() ?? gameObject.AddComponent<BlockOutlineURP>();
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
        Highlight(false);

        // 로컬 BoxCollider 기준으로 클릭면 생성
        ClickFaceBuilder.AddFacesFromBox(this,
            marginXRatio: 0.02f,
            marginYRatio: 0.12f,
            thickness: 0.0015f,
            surfaceEps: 0.0005f);
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
        if (InputManager.Instance && InputManager.Instance.IsBlocked(InputType.Interaction))
            return;

        if (JengaTowerManager.Instance != null &&
         JengaTowerManager.Instance.IsArenaMuted(OwnerActorNumber))
            return;

        if (!_interactable || _busy || _pendingRemoval || IsRemoved) return; 

        // 타이밍 매니저가 이미 실행 중이면 무시
        if (JengaTimingManager.Instance._isTimingActive) return;

        // 내 타워의 블록만 조작 가능
        if (OwnerActorNumber != PhotonNetwork.LocalPlayer.ActorNumber) return;

        var tower = JengaTowerManager.Instance?.GetPlayerTower(OwnerActorNumber);

        if (tower == null) return;

        // 모든 경로에서 최상단 보호층 전역 차단 (1차/2차 모두)
        if (tower.IsLayerTopProtected(Layer))
        {
            // 혹시 이전에 1차 선택이 켜졌다면 정리
            if (_isSelected)
            {
                _isSelected = false;
                Highlight(false);
                if (previewOppositeOnSide) _pairedTargetPreview?.Highlight(false);
                _pairedTargetPreview = null;
            }
            return;
        }


        //  1) 세션 중인 레이어라면: 사이드만 허용
        if (tower.IsPairSessionActiveOn(Layer))
        {
            if (!tower.CanRemoveBlock(this))
            {
                return;
            }

            if (!_isSelected)
            {
                _isSelected = true; 
                Highlight(true);

                // 클릭 사운드
                SoundManager.Instance.PlaySFX("Click");

                OnAnyBlockSelected?.Invoke(this);
            }
            else
            {
                if (_busy || IsRemoved) return;
                _isSelected = false; 
                Highlight(false);
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
                _isSelected = true; 
                Highlight(true);
                // 클릭 사운드
                SoundManager.Instance.PlaySFX("Click");
                OnAnyBlockSelected?.Invoke(this);
            }
            else
            {
                if (_busy || IsRemoved) return;
                _isSelected = false; 
                Highlight(false);
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
            if (!_isSelected)
            {
                if (previewOppositeOnSide)
                {
                    _pairedTargetPreview = tower.GetOppositeSideInLayer(Layer, IndexInLayer);
                    if (_pairedTargetPreview && !_pairedTargetPreview.IsRemoved)
                        _pairedTargetPreview.Highlight(true);
                }

                _isSelected = true;
                Highlight(true);
                // 클릭 사운드
                SoundManager.Instance.PlaySFX("Click");
                OnAnyBlockSelected?.Invoke(this);
            }
            else
            {
                // 2차 클릭: 타이밍 게임 시작
                _isSelected = false;
                Highlight(false);
                if (previewOppositeOnSide) _pairedTargetPreview?.Highlight(false);
                _pairedTargetPreview = null;

                OnAnyBlockTimingStart?.Invoke(this);
                _busy = true;
            }
            return;
        }
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
            return;
        }

        if (_pendingRemoval) return;
        // 성공 처리: 블록 제거 ‘요청’만 마스터에게 보냄
        _pendingRemoval = true;

        var playerId = PhotonNetwork.LocalPlayer.ActorNumber;

        int baseScore = 10;
        int bonusScore = Mathf.RoundToInt(Mathf.Clamp01(accuracy) * 10f);
        int totalScore = baseScore + bonusScore;

        // 마스터 승인 루트로 변경
        JengaNetworkManager.Instance?.RequestBlockRemoval_MasterAuth(
            OwnerActorNumber, BlockId, totalScore, accuracy
        );
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

    #region 시각 보조 (테두리만)
    public void Highlight(bool on)
    {
        EnsureCaches();
        if (_outline == null) return;
        if (on) _outline.Show(); else _outline.Hide();
    }
    public void PulseOnce(float duration = 0.22f, float scaleMul = 1.15f)
    {
        EnsureCaches();
    }

    private void ClearSelection()
    {
        _isSelected = false;
        Highlight(false);
        _pairedTargetPreview?.Highlight(false);
        _pairedTargetPreview = null;
    }

    // 젠가 선택 시 나오는 UI 취소시 1차 선택 상태를 강제로 초기화 시키는 용도
    public void ForceClearSelectionForOverlay()
    {
        // 외부(UI 오버레이)에서 1차 선택 상태를 강제로 초기화할 때만 사용
        _isSelected = false;
        Highlight(false);

        if (_pairedTargetPreview != null)
        {
            _pairedTargetPreview.Highlight(false);
            _pairedTargetPreview = null;
        }
    }

    #endregion

    // 제거 거절 수신 시 로컬 상태 원복
    public void OnRemovalDenied(string reason = null)
    {
        // 입력 상태 플래그 원복
        _busy = false;
        _pendingRemoval = false;

        // 선택/프리뷰 하이라이트 원복
        if (_isSelected)
        {
            _isSelected = false;
            Highlight(false);
        }

        if (_pairedTargetPreview != null)
        {
            _pairedTargetPreview.Highlight(false);
            _pairedTargetPreview = null;
        }

        // 이미 제거된 상태면 안전 종료
        if (IsRemoved) return;

        if (!string.IsNullOrEmpty(reason))
            Debug.LogWarning($"[JengaBlock] Removal denied. block={BlockId}, reason={reason}");
    }

}