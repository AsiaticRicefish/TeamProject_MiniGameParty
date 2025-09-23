using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LDH_MainGame;
using TMPro;
using UnityEngine;

namespace LDH_UI
{
    public class UI_SlotMachine_Row : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private RectTransform rowListRect;
        
        [Header("Visual")]
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private int fontSize = 64;
        [SerializeField] private Color fontColor = Color.white;

        [Header("Spin Params")]
        [SerializeField] private float accelTime = 0.35f;     // 가속 구간
        [SerializeField] private float minCruiseTime = 0.6f;  // 최고속 유지 최소 시간
        [SerializeField] private float maxCruiseTime = 1.2f;  // 최고속 유지 최대 시간
        [SerializeField] private float decelTime = 0.55f;     // 감속 구간
        [SerializeField] private float cellsPerSecondAtMax = 9f; // 최고속: 초당 몇 칸을 지나가게 할지
        [SerializeField] private int   extraLapsMin = 2;      // 최소 몇 바퀴(=전체 개수 기준) 더 돌고 멈출지
        [SerializeField] private int   extraLapsMax = 3;
        
        public bool   rowStopped { get; private set; } = true;
        public string stoppedSlot { get; private set; }
        
        
        private readonly List<string> _candidates = new();
        private readonly List<TMP_Text> _elements = new();
        private float _cellHeight; // 한 칸(한 항목)의 높이 = 뷰포트 높이
        private float _totalHeight; // 컨테이너 총 높이 = cellHeight * count
        
        /// <summary>
        /// 후보 게임들로 행 구성 (뷰포트=부모 RectTransform 높이를 각 칸 높이로 사용)
        /// </summary>
        public async UniTask SetCandidates(List<MiniGameInfo> gameList)
        {
            
            if (gameList == null || gameList.Count == 0)
            {
                Debug.LogWarning("[SlotRow] empty candidate list");
                return;
            }
            
            // 초기화
            foreach (var t in _elements)
                if (t) Destroy(t.gameObject);
            _elements.Clear();
            _candidates.Clear();
            stoppedSlot = null;
            
            // 부모(뷰포트)의 높이가 ‘한 칸’이 됨
            var viewport = (RectTransform)rowListRect.parent;
            Canvas.ForceUpdateCanvases();
            await UniTask.Yield();
            _cellHeight = viewport.rect.height;
         
            // 컨테이너 앵커/피벗: 상단 고정
            rowListRect.anchorMin = new Vector2(0, 1);
            rowListRect.anchorMax = new Vector2(1, 1);
            rowListRect.pivot     = new Vector2(0.5f, 1);
            
            // 전체 높이 = 칸 수 × 한 칸 높이
            _totalHeight = _cellHeight * gameList.Count;
            rowListRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _totalHeight);
            rowListRect.anchoredPosition = Vector2.zero; // 맨 위에서 시작 (0번째 아이템이 보이게)

            // 항목들 생성 & 배치 (각 칸은 viewport 높이만큼)
            for (int i = 0; i < gameList.Count; i++)
            {
                var info = gameList[i];
                _candidates.Add(info.id);

                var go = new GameObject($"Item_{i}_{info.gameName}", typeof(RectTransform), typeof(TMP_Text));
                var rt = go.GetComponent<RectTransform>();
                var tmp = go.GetComponent<TMP_Text>();
                rt.SetParent(rowListRect, false);
                rt.SetAsLastSibling();

                // 텍스트 표기
                tmp.font = font;
                tmp.fontSize = fontSize;
                tmp.color = fontColor;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.enableAutoSizing = false;
                tmp.text = info.gameName;

                _elements.Add(tmp);
            }
            rowStopped = true;
            
        }

        
        /// <summary>
        /// 회전 시작: 랜덤 대상(혹은 외부에서 지정한 인덱스)에 맞춰 스무스하게 멈춤
        /// </summary>
        public void StartRotating(int? forceTargetIndex = null)
        {
            if (_candidates.Count == 0) return;
            
            stoppedSlot = "";
            RotateAsync().Forget();
        }

        private async UniTask RotateAsync(int? forceTargetIndex = null)
        {
            rowStopped = false;     // spining
 
            // 1) 목표 인덱스 결정
            int targetIndex = forceTargetIndex ?? Random.Range(0, _candidates.Count);

            // 2) “충분히 돈 뒤” 목표 인덱스에 스냅
            int laps = Random.Range(extraLapsMin, extraLapsMax + 1);
            float startY = WrapY(rowListRect.anchoredPosition.y);
            float targetY = startY + (laps * _candidates.Count + targetIndex) * _cellHeight;
            
            // 속도: 가속(accel) -> 순항(cruise) -> 감속(decel)
            float cruiseTime = Random.Range(minCruiseTime, maxCruiseTime);
            float maxSpeed = cellsPerSecondAtMax * _cellHeight; // px/sec

            float t = 0f;
            float y = startY;
            // a) 가속
            while (t < accelTime)
            {
                t += Time.deltaTime;
                float k = t / accelTime;              // 0→1
                float v = maxSpeed * EaseOutCubic(k); // 0→maxSpeed
                y += v * Time.deltaTime;
                rowListRect.anchoredPosition = new Vector2(0, -WrapY(y));
                await UniTask.Yield();
            }
            
            // b) 순항
            t = 0f;
            while (t < cruiseTime)
            {
                t += Time.deltaTime;
                y += maxSpeed * Time.deltaTime;
                rowListRect.anchoredPosition = new Vector2(0, -WrapY(y));
                await UniTask.Yield();
            }
            // c) 감속(+ 정확히 목표 위치로)
            // 감속 동안 선형으로 목표 y까지 보정 (ease로 감속)
            float decelStartY = y;
            float decelDistance = targetY - decelStartY;

            t = 0f;
            while (t < decelTime)
            {
                t += Time.deltaTime;
                float k = t / decelTime;               // 0→1
                float eased = EaseOutCubic(k);         // 감속 느낌
                float cur = decelStartY + decelDistance * eased;
                rowListRect.anchoredPosition = new Vector2(0, -WrapY(cur));
                await UniTask.Yield();
            }

            // 스냅(정확히 칸 경계에)
            float finalY = startY + (laps * _candidates.Count + targetIndex) * _cellHeight;
            float snapped = Mathf.Round(finalY / _cellHeight) * _cellHeight;
            rowListRect.anchoredPosition = new Vector2(0, -WrapY(snapped));

            stoppedSlot = _candidates[targetIndex];
            rowStopped = true;
            
        }
        // 컨테이너가 아주 많이 이동해도 화면에선 반복되게 보이도록 모듈러 처리
        private float WrapY(float y)
        {
            if (_totalHeight <= 0f) return 0f;
            // 음수까지 커버되는 안전 모듈러
            float m = y % _totalHeight;
            if (m < 0) m += _totalHeight;
            return m;
        }

        // 부드러운 감속 곡선
        private static float EaseOutCubic(float x)
        {
            // 1 - (1-x)^3
            float k = 1f - x;
            return 1f - k * k * k;
        }
    }
}