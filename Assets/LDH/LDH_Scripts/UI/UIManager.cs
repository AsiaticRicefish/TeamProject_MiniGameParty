using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using DesignPattern;
using LDH_Util;
using UnityEngine;

namespace LDH_UI
{
    /// <summary>
    /// 게임 내 UI 전체를 총괄하는 매니저.
    /// 전역 UI, 팝업 UI, 캔버스 정렬 및 스타일 테이블 초기화 등을 담당한다.
    /// </summary>
    public class UIManager : CombinedSingleton<UIManager>
    {
        //---- Stack 관리 ----//
        [SerializeField] private int baseOrderScreen = 100;
        [SerializeField] private int baseOrderPopup = 200;
        [SerializeField] private int baseOrderToast = 300;

        private int _orderScreen, _orderPopup, _orderToast;


        private readonly Stack<UI_Popup> _popupStack = new(); // 팝업 UI Stack
        private readonly Dictionary<Type, UI_Base> _screenCache = new(); // Screen 풀

        //---- toast ui ---- //
        private UI_Toast _toast; // 1개 재사용
        private readonly Queue<(string msg, float dur)> _toastQueue = new();
        private bool _isToastShowing;


        //---- UI Root 오브젝트 ---- //
        public UI_Root UIRoot { get; private set; }


        [SerializeField] private string uiRootFolder = "Prefabs/UI";
        [SerializeField] private string screenFolder = "Prefabs/UI/Screen";
        [SerializeField] private string popupFolder = "Prefabs/UI/Popup";
        [SerializeField] private string toastFolder = "Prefabs/UI/Toast";

        protected override void OnAwake() => Init();

        // UI 매니저 초기화
        private void Init()
        {
            _orderScreen = baseOrderScreen;
            _orderPopup = baseOrderPopup;
            _orderToast = baseOrderToast;

            InitUIRoot(); //UI Root를 생성

            InitScreenUIs();

            _toast = CreateToast();


        }

        #region Initialize

        /// <summary>
        /// UI 루트 오브젝트 생성 및 초기화
        /// </summary>
        private void InitUIRoot()
        {
            if (UIRoot != null) return;
            UIRoot = Util_LDH.Instantiate<UI_Root>(Path.Combine(uiRootFolder, "@UIRoot"));
            UIRoot.name = "@UIRoot";
            DontDestroyOnLoad(UIRoot); // 파괴 방지
        }


        /// <summary>
        /// 전역 UI 프리팹들을 로드 및 초기화
        /// </summary>
        private void InitScreenUIs()
        {
            foreach (Define_LDH.ScreenUI uiEnum in Enum.GetValues(typeof(Define_LDH.ScreenUI)))
            {
                // 1. Enum → 이름 → 경로 변환
                string screenUIName = uiEnum.ToString();
                string fullPath = Path.Combine(screenFolder, screenUIName);

                // 2. 프리팹 인스턴스화
                UI_Screen prefab = Resources.Load<UI_Screen>(fullPath);
                    
                UI_Screen ui = Util_LDH.Instantiate<UI_Screen>(prefab, getUIAreaTransform(prefab.Area));

                // 3. Type 얻기 (주의: 네임스페이스 포함 문자열 필요)
                Type uiType = GetUIType(screenUIName);

                if (uiType == null)
                {
                    Debug.LogError($"[{GetType().Name}] 타입을 찾을 수 없습니다: {screenUIName}");
                    continue;
                }

                // 4. 컴포넌트 캐싱 및 비활성화
                _screenCache.Add(uiType, ui);

                ui.OnCloseRequested += HandleCloseRequested;
            }

        }

        /// <summary>
        /// UI 타입 문자열로부터 Type 객체 반환
        /// </summary>
        private Type GetUIType(string name) => Type.GetType($"GameUI.{name}");


        #endregion


        #region Canvas Setting / UI Area

        /// <summary>
        /// 지정한 오브젝트에 Canvas 컴포넌트를 설정하고 정렬 순서를 부여합니다.
        /// </summary>
        public void SetCanvas(GameObject go, Define_LDH.UILayer layer, bool sort = true)
        {
            Canvas canvas = Util_LDH.GetOrAddComponent<Canvas>(go);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;

            if (!sort)
            {
                canvas.sortingOrder = 0;
                return;
            }

            switch (layer)
            {
                case Define_LDH.UILayer.Screen:
                    canvas.sortingOrder = _orderScreen++;
                    break;
                case Define_LDH.UILayer.Popup:
                    canvas.sortingOrder = _orderPopup++;
                    break;
                case Define_LDH.UILayer.Toast:
                    canvas.sortingOrder = _orderToast++;
                    break;
            }
        }


        private Transform getUIAreaTransform(Define_LDH.UIAreaType areaType)
        {
            if (UIRoot == null) return null;
            return areaType switch
            {
                Define_LDH.UIAreaType.Top => UIRoot.TopArea.transform,
                Define_LDH.UIAreaType.Center => UIRoot.CenterArea.transform,
                Define_LDH.UIAreaType.Bottom => UIRoot.BottomArea.transform,
                Define_LDH.UIAreaType.Default => UIRoot.DefaultArea.transform,
                _ => UIRoot.transform,
            };
        }

        #endregion

        #region Screen (재사용)

        public T CreateScreenUI<T>(string name = null) where T : UI_Screen
        {
            name ??= typeof(T).Name;
            var screenPrefab = Resources.Load<T>(Path.Combine(screenFolder, name));
            T screen = Util_LDH.Instantiate<T>(screenPrefab, getUIAreaTransform(screenPrefab.Area));

            //INit에서 각자 알아서 canvas group alpha 처리될꺼니까 관련 로직은 삭제

            screen.OnCloseRequested += HandleCloseRequested;
            _screenCache[typeof(T)] = screen;
            return screen;
        }

        /// <summary>
        /// Enum 기반으로 전역 UI 인스턴스를 가져옵니다.
        /// </summary>
        public UI_Base GetScreenUI(Define_LDH.ScreenUI uiEnum)
        {
            Type type = GetUIType(uiEnum.ToString());
            return _screenCache.GetValueOrDefault(type);
        }

        public T GetScreenUI<T>() where T : UI_Screen
        {
            return _screenCache.TryGetValue(typeof(T), out var ui) ? ui as T : null;
        }


        /// <summary>
        /// 전역 UI를 활성화합니다.
        /// 팝업일 경우 Stack에 Push합니다.
        /// </summary>
        public async UniTask<T> ShowScreenUI<T>(T screen) where T : UI_Screen
        {
            SetCanvas(screen.gameObject, Define_LDH.UILayer.Screen, sort: true);
            await screen.ShowAsync();
            return screen;
        }

        /// <summary>
        /// 전역 UI를 비활성화합니다. (팝업이면 Stack에서 Pop)
        /// </summary>
        public async UniTask CloseScreenUI(UI_Screen screen, bool destroy = false)
        {
            if (screen == null || !screen.IsVisible) return;

            await screen.CloseAsync();
            _orderScreen = Mathf.Max(baseOrderScreen, _orderScreen - 1);

            if (destroy)
            {
                screen.OnCloseRequested -= HandleCloseRequested;
                if (screen) Destroy(screen.gameObject);
                _screenCache.Remove(screen.GetType());
            }
        }

        #endregion


        #region Popup UI

        /// <summary>
        /// 팝업 UI를 생성합니다. (invisible로 생성)
        /// </summary>
        public T CreatePopupUI<T>(string name = null) where T : UI_Popup
        {
            
            name ??= typeof(T).Name;
            var popupPrefab = Resources.Load<T>(Path.Combine(popupFolder, name));
            T popup = Util_LDH.Instantiate<T>(popupPrefab, getUIAreaTransform(popupPrefab.Area));
            
            // 실제 show시에 stack에 push 됨
            // 외부에서 UI 닫기에 대해 UIManager가 처리하도록 구독
            popup.OnCloseRequested += HandleCloseRequested;

            return popup;
        }

        public async UniTask<T> ShowPopupUI<T>(T popup) where T : UI_Popup
        {
            // 정렬 순서 부여
            SetCanvas(popup.gameObject, Define_LDH.UILayer.Popup, sort: true);

            // 최상단으로 Push
            _popupStack.Push(popup);

            await popup.ShowAsync();
            return popup;
        }

        /// <summary>
        /// 특정 팝업을 닫습니다. (최상단일 때만 가능)
        /// </summary>
        public async UniTask ClosePopupUI(UI_Popup popup, bool destory = true)
        {
            if (_popupStack.Count == 0 || _popupStack.Peek() != popup)
            {
                Debug.LogWarning($"[{GetType().Name}] 닫으려는 팝업이 최상단 팝업이 아닙니다.");
                return;
            }

            await popup.CloseAsync();

            _popupStack.Pop();
            _orderPopup = Mathf.Max(baseOrderPopup, _orderPopup - 1);

            if (destory)
            {
                popup.OnCloseRequested -= HandleCloseRequested;
                if (popup) Destroy(popup.gameObject);
            }

        }

        /// <summary>
        /// 최상단 팝업을 닫습니다.
        /// </summary>
        public async UniTask CloseTopPopupUI(bool destroy = true)
        {
            // Stack 비어있을 경우 리턴
            if (_popupStack.Count == 0)
                return;

            // 최상단 팝업 Pop 후 제거
            UI_Popup top = _popupStack.Peek();
            await ClosePopupUI(top, destroy);
        }

        /// <summary>
        /// 모든 팝업을 제거합니다.
        /// </summary>
        public async UniTask CloseAllPopupUI()
        {
            while (_popupStack.Count > 0)
                await CloseTopPopupUI(true);

            _orderPopup = baseOrderPopup;
        }

        
        public Coroutine ClosePopupUI_AsCoroutine(UI_Popup popup, bool destroy = true)
            => StartCoroutine(ClosePopupUI(popup, destroy).ToCoroutine());

        #endregion


        #region Toast (큐)

        public UI_Toast CreateToast(string name = "UI_Toast")
        {
            var toastPrefab = Resources.Load<UI_Toast>(Path.Combine(toastFolder, name));
            UI_Toast toast = Util_LDH.Instantiate<UI_Toast>(toastPrefab, getUIAreaTransform(toastPrefab.Area));
            
            //배치
            Util_LDH.SetCenterBottom(toast.TargetRect, toast.TargetRect.sizeDelta, new Vector2(0f, 60f));

            return toast;
        }
        

        public void EnqueueToast(string message, float duration = 1.5f)
        {
            _toastQueue.Enqueue((message, duration));
            if (!_isToastShowing)
                ProcessToastQueue(this.GetCancellationTokenOnDestroy()).Forget(LogException);
        }

        private static void LogException(Exception ex)
        {
            //정상 취소는 로그 남기지 않기
            if (ex is OperationCanceledException) return;
            
            Debug.LogException(ex);
        }


        private async UniTask ProcessToastQueue(CancellationToken ct)
        {
            _isToastShowing = true;

            try
            {
                if (_toast == null)
                    _toast = CreateToast(); // 1개만 재사용

                while (_toastQueue.Count > 0)
                {
                    var (msg, dur) = _toastQueue.Dequeue();

                    _toast.SetMessage(msg);
                    SetCanvas(_toast.gameObject, Define_LDH.UILayer.Toast, sort: true);
                    await _toast.ShowAsync();

                    await UniTask.Delay(TimeSpan.FromSeconds(dur),
                        cancellationToken: this.GetCancellationTokenOnDestroy());

                    await CloseToastUI(_toast);
                }
            }
            catch (OperationCanceledException)
            {
                //정상 종료
                Debug.Log("[UIManager] Toast 정상 종료");
            }
            catch (Exception ex)
            {
                // 그 외는 비정상 종료
                Debug.LogException(ex);
            }
            finally
            {
                _isToastShowing = false;
            }

           
        }


        public async UniTask CloseToastUI(UI_Toast toast)
        {
            await toast.CloseAsync();
            _orderToast = Mathf.Max(baseOrderToast, _orderToast - 1);
        }

        #endregion


        #region Close 라우팅

        // UI 내부에서 RequestClose()가 발생했을 때 매니저가 받아 처리
        // Close Request 라우팅
        private async void HandleCloseRequested(UI_Base ui)
        {
            switch (ui)
            {
                case UI_Popup p:   await ClosePopupUI(p); break;
                case UI_Screen s:  await CloseScreenUI(s); break;
                case UI_Toast t:   await CloseToastUI(t); break;
                default:
                    // Screen/Popup/Toast 외의 커스텀 타입
                    await ui.CloseAsync();
                    break;
            }
        }

        #endregion

    }
}