using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LDH_Util;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LDH_UI
{
    public abstract class UI_Base : MonoBehaviour
    {
        
        [SerializeField] protected Define_LDH.UIAreaType _area = Define_LDH.UIAreaType.Default;
        [SerializeField] protected CanvasGroup cg;
        [SerializeField] protected bool interactable;
        [SerializeField] protected bool blocksRaycasts;
        
        
        // Private 변수
        protected bool _isVisible = false;          // visible 상태
        protected bool _isAnimating = false;        // 애니메이션 실행 중인지 여부
        private int _animVersion; // 재진입/취소 경쟁 방지용
        private CancellationTokenSource _cts;   // 현재 실행 중인 트랜지션을 취소하기 위한 토큰 소스 (취소 신호를 만들고 보내는 주체)
        
        // 프로퍼티
        public bool IsVisible => _isVisible; // 가시성 조회용
        public Define_LDH.UIAreaType Area => _area;
        
        // 이벤트
        public event Action<UI_Base> OnCloseRequested;   
        
        
        protected void Awake() => Init();
        protected void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _animVersion++; // 이전 작업들이 finally에서 실행되지 않도록 버전 증가
            OnCloseRequested = null;
        }
        
        

        #region UI Show/Close (Public API)
        
        
        //UI의 show 애니메이션 연출 완료까지 대기
        public async UniTask ShowAsync()  => await PlayVisibilityAsync(true);
       
        /// <summary>
        /// UIManager만 사용 가능
        /// UI의 close 애니메이션 연출 완료까지 대기
        /// </summary>
        public async UniTask CloseAsync()  => await PlayVisibilityAsync(false);
        
        
        /// <summary>
        /// UI 내부 버튼 및 외부에서 UI를 닫기 요청하는 메서드
        /// 실제 닫기는 UIMananger에서 처리
        /// </summary>
        public void RequestClose() => OnCloseRequested?.Invoke(this);
        
        #endregion

        #region Core Visibility Logic

        // 자식 -> 부모 순서로 레이아웃 강제 갱신
        private async UniTask ForceReBuildLayout(Transform root, CancellationToken ct)
        {
            if (!root) return;

            RectTransform targetRect = null;
            if (root.TryGetComponent<LayoutGroup>(out var layout))
            {
                 targetRect = layout.GetComponent<RectTransform>();
            }
            else
            {
                var layoutChild = root.GetComponentInChildren<LayoutGroup>(true);
                targetRect = layoutChild?.GetComponent<RectTransform>();
            }

            
            if(targetRect!=null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(targetRect);
            
            // 프레임 끝까지 한 번 흘려 레이아웃/메시 갱신을 보장
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, ct);
            Canvas.ForceUpdateCanvases();
            
        }
        
        private async UniTask PlayVisibilityAsync(bool visible)
        {
            // 1) 이전 애니메이션이 돌고 있다면 정상적으로 취소시킨다.(매 실행마다 이전 토큰을 Cancel+Dispose하고 새로운 토큰 소스를 만든다.)
            _cts?.Cancel();
            _cts?.Dispose();
            
            // 2) 현재 실행용 토큰 소스를 새로 생성한다. 
            // MonoBehaviour가 파괴될 때 자동으로 Cancel 될 수 있도록 하는 토큰으로 생성 (여러 토큰을 묶어서 그 중 하나라도 취소되면 함께 취소되는 토큰 소스)
            var destroyCt = this.GetCancellationTokenOnDestroy();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(destroyCt);

            
            var ct = _cts.Token;  // 취소 신호를 받는 구독자, 취소신호를 보내면 OperationCanceledException을 던지며 즉시 중단됨

            int myVersion = ++_animVersion;
            
            // 같은 상태로의 중복 요청이면 무시
            if (_isAnimating && _isVisible == visible) return;

            _isAnimating = true;
            SetInteractable(false); //애니메이션 동안 입력/레이캐스트 잠금

            try
            {
                if (visible)
                {
                    if (gameObject!=null && !gameObject.activeSelf) gameObject.SetActive(true);
                        gameObject.SetActive(true);
                    
                    
                    // 레이아웃 강제 갱신을 위해 추가
                    await ForceReBuildLayout(transform, ct);
                    await OnShowAsync(ct);
                    _isVisible = true;
                }
                else
                {
                    await OnCloseAsync(ct);
                    _isVisible = false;
                }
            }
            catch (OperationCanceledException)
            {
                //정상 취소
                //이전 연출을 끊는 과정에서 진입
                //에러가 아니기 때문에 따로 처리할 부분 없음
            }
            catch (Exception ex)
            {
                // 애니메이션 내부 예외는 로깅
                Debug.LogException(ex);
            }
            finally
            {
                if (myVersion == _animVersion && gameObject!=null && cg)
                    SetInteractable(_isVisible); // 입력/레이캐스트 복구
                
              
                _isAnimating = false;
            }
            

        }
        

        /// <summary>
        /// CanvasGroup이 있을 경우, 인터랙션/레이캐스트를 on/off 한다.
        /// (없다면 아무 일도 하지 않음)
        /// </summary>
        protected virtual void SetInteractable(bool value)
        {
            if (gameObject==null || !cg) return;
            cg.interactable = value && interactable;
            cg.blocksRaycasts = value && blocksRaycasts;
        }
  
        #endregion


        #region Override
        
        // 초기화
        protected virtual void Init()
        {
            if (cg == null)
            {
                cg = GetComponent<CanvasGroup>();
                if (cg == null)
                    cg = this.AddComponent<CanvasGroup>();
            }

            cg.alpha = 0f;
            _isVisible = false;
            SetInteractable(false);
            
        }

        /// <summary>
        /// Show 애니메이션. 기본 구현은 애니 없이 즉시 완료.
        /// 파생 클래스에서 DOTween/Animator 등을 이용해 구현.
        /// </summary>
        protected virtual UniTask OnShowAsync(CancellationToken ct) => UniTask.CompletedTask;

        /// <summary>
        /// Close 애니메이션. 기본 구현은 애니 없이 즉시 완료.
        /// </summary>
        protected virtual UniTask OnCloseAsync(CancellationToken ct) => UniTask.CompletedTask;

        
        #endregion
    
        #region Event Binding

        /// <summary>
        /// 지정된 GameObject에 UI 이벤트(Click, Enter, Exit, Drag)를 바인딩하는 정적 메서드.
        /// UI_EventHandler 컴포넌트를 자동으로 추가하며, 중복 바인딩을 방지하기 위해 먼저 이벤트를 제거한 후 등록.
        /// </summary>
        /// <param name="uiGameObject">이벤트를 바인딩할 대상 GameObject</param>
        /// <param name="eventAction">실행할 이벤트 핸들러</param>
        /// <param name="eventType">바인딩할 이벤트 종류(Default : Click)</param>
        public static void BindUIEvent(GameObject uiGameObject, Action<PointerEventData> eventAction,
            Define_LDH.UIEvent eventType = Define_LDH.UIEvent.Click)
        {
            var eventHandler = Util_LDH.GetOrAddComponent<UI_EventHandler>(uiGameObject);

            switch (eventType)
            {
                case Define_LDH.UIEvent.Click:
                    eventHandler.OnClickHandler -= eventAction;
                    eventHandler.OnClickHandler += eventAction;
                    break;
                case Define_LDH.UIEvent.PointEnter:
                    eventHandler.OnEnterHandler -= eventAction;
                    eventHandler.OnEnterHandler += eventAction;
                    break;
                case Define_LDH.UIEvent.PointExit:
                    eventHandler.OnExitHandler -= eventAction;
                    eventHandler.OnExitHandler += eventAction;
                    break;
                case Define_LDH.UIEvent.Drag:
                    eventHandler.OnDragHandler -= eventAction;
                    eventHandler.OnDragHandler += eventAction;
                    break;
            }
        }

        #endregion
    }
}