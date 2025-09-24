using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using LDH_MainGame;
using LDH_Util;
using PMS_Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        [SerializeField] private int   minVisualCount = 7;     // 화면상 최소 칸 수(자연스러운 회전용
        [SerializeField] private float accelRatio = 0.13f;     // 총 시간 중 가속 비율
        [SerializeField] private float decelRatio = 0.25f;     // 총 시간 중 감속 비율
        [SerializeField] private float minTotalTime = 0.9f;    // 전체 회전 최소 시간(짧아도 너무 빨라보이지 않게)
        [SerializeField] private float cellsPerSecondAtMax = 18f;
        [SerializeField] private int   extraLapsMin = 2;
        [SerializeField] private int   extraLapsMax = 3;
        
        [Header("Sound")] [SerializeField]
        private Define_LDH.SfxKey pickedMiniSfx = Define_LDH.SfxKey.Main_MiniGamePicked;
        private Define_LDH.SfxKey spinningRoulletSfx = Define_LDH.SfxKey.Main_Roullet;

        
        public bool   rowStopped { get; private set; } = true;
        public string stoppedSlot { get; private set; }
        
        
        private readonly List<string> _ids = new();
        private float _cellHeight; // 한 칸(한 항목)의 높이 = 뷰포트 높이
        private float _totalHeight; // 컨테이너 총 높이 = cellHeight * count
        private float _rawY;                                            // "원시 y" 값: 누적 이동량(랩핑 전 값, 계속 커져도 OK)
        private Tween _spinTween;                                       // 현재 실행 중인 DOTween 트윈(취소/중복 방지용)
        
        private int _lastTickCell = -1;
        private float _lastTickTime = -999f;
        private float _minTickInterval = 0.03f; // 너무 빠를 때 과도재생 방지


        /// <summary>
        /// 후보 게임들로 행 구성 (뷰포트=부모 RectTransform 높이를 각 칸 높이로 사용)
        /// </summary>
        public async UniTask SetCandidates(List<string> originalIds)
        {
            
            if (originalIds == null || originalIds.Count == 0)
            {
                Debug.LogWarning("[SlotRow] empty candidate list");
                return;
            }
            
            // 부모(뷰포트)의 높이가 ‘한 칸’이 됨
            var viewport = (RectTransform)rowListRect.parent;
            Canvas.ForceUpdateCanvases();
            await UniTask.Yield();
            _cellHeight = viewport.rect.height;
            
            // 원본 후보를 반복해서 최소 minVisualCount 이상으로 만든다.
            _ids.Clear();
            int targetCount = Mathf.Max(minVisualCount, originalIds.Count);
            while (_ids.Count < targetCount)
                _ids.AddRange(originalIds);
            if (_ids.Count > targetCount)
                _ids.RemoveRange(targetCount, _ids.Count - targetCount); // 딱 맞춰 자르기(선택)

            // 기존 자식 제거
            Util_LDH.RemoveAllChildren(rowListRect);
            
            // 자식 생성
            // 항목들 생성 & 배치 (각 칸은 viewport 높이만큼)
            for (int i = 0; i < _ids.Count; i++)
            {
                var gameId =_ids[i];;
                string gameName = MainGameManager.Instance.registry.GetGameName(gameId);
                
                var go = new GameObject($"game id :{i} / game name : {gameName}",                     typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
                var rt = go.GetComponent<RectTransform>();
                var tmp = go.GetComponent<TextMeshProUGUI>();
                var le   = go.GetComponent<LayoutElement>();

                rt.SetParent(rowListRect, false);
                rt.SetAsLastSibling();

                // 텍스트 표기
                tmp.font = font;
                tmp.fontSize = fontSize;
                tmp.color = fontColor;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.enableAutoSizing = false;
                tmp.text = gameName;

                // 레이아웃: 한 칸 높이를 보장
                le.preferredHeight = _cellHeight;
                le.minHeight       = _cellHeight;
                le.flexibleHeight  = 0;
  
            }
            
            // 레이아웃 갱신 후 총 높이 계산
            Canvas.ForceUpdateCanvases();
            _totalHeight = _cellHeight * _ids.Count;
            
            // 컨테이너 앵커/피벗: 하단 고정
            rowListRect.anchorMin = new Vector2(0, 0);
            rowListRect.anchorMax = new Vector2(1, 0);
            rowListRect.pivot     = new Vector2(0.5f, 0);
            
            rowListRect.offsetMin = new Vector2(0f, rowListRect.offsetMin.y); // left = 0
            rowListRect.offsetMax = new Vector2(0f, rowListRect.offsetMax.y); // right = 0
            
            
            rowListRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _totalHeight);
            rowListRect.anchoredPosition = Vector2.zero; // 맨 위에서 시작 (0번째 아이템이 보이게)
            // Debug.Log($"cell height : {_cellHeight}, total height : {_totalHeight}");
            
            
            _rawY = 0f;                                                 // 원시 y도 0으로 초기화
            rowStopped = true;
            
        }

        
        /// <summary>
        /// 회전 시작: 랜덤 대상(혹은 외부에서 지정한 인덱스)에 맞춰 스무스하게 멈춤
        /// </summary>
        public void StartRotating(int? forceTargetIndex = null)
        {
            if (_ids.Count == 0) return;                                // 후보가 없으면 무시
            if (_spinTween != null && _spinTween.IsActive())            // 이전 트윈이 돌고 있으면
                _spinTween.Kill();                                      // 중복 트윈 방지 위해 Kill

            rowStopped = false;
            stoppedSlot = null;
            
            // 틱 상태 리셋
            _lastTickCell = -1;       
            _lastTickTime = -999f;
            RotateAsync(forceTargetIndex).Forget();
        }

        private async UniTask RotateAsync(int? forceTargetIndex = null)
        {
            int count = _ids.Count;
            
            // 현재 인덱스
            int currentIndex = Mathf.RoundToInt((Wrap(_rawY) / _cellHeight)) % count;
            if (currentIndex < 0) currentIndex += count;

            // 목표 인덱스
            int target = forceTargetIndex ?? Random.Range(0, count);         // 멈출 칸 결정
            
            // 몇 바퀴를 돌 지
            int laps = Random.Range(extraLapsMin, extraLapsMax + 1);
            
            // 총 이동해야 할 "칸 수" = (추가 바퀴 * 항목수 + 목표 인덱스 - 현재 인덱스)
            int deltaCells = laps * count + (target - currentIndex);            // 총 지나갈 칸 수
            if (deltaCells <= count) deltaCells += count;                     // 한 바퀴 이상 보장
            int minCells = Mathf.Max(8, 2 * count);                           // 최소 N칸 보장
            if (deltaCells < minCells)
                deltaCells += ((minCells - deltaCells + count - 1) / count) * count;
            // 총 이동해야 할 거리(px) = 칸 수 * 한 칸 높이
            float totalDistance = deltaCells * _cellHeight;              // start→end 전체 이동 거리
            

           // 총 시간 계산
           float maxSpeedPx   = cellsPerSecondAtMax * _cellHeight;
           float totalTime    = Mathf.Max(minTotalTime, totalDistance / (maxSpeedPx * 0.75f)); // 평균속도 75% 가정
           float accelTime    = totalTime * accelRatio;         // 가속 구간
           float decelTime    = totalTime * decelRatio;         // 감속 구간
           float cruiseTime   = Mathf.Max(0f, totalTime - accelTime - decelTime);       // 순항 구간
           
           float y0 = _rawY;
           float y1 = y0 + totalDistance * accelRatio;
           float y2 = y1 + totalDistance * (1f - accelRatio - decelRatio);
           float y3 = y0 + totalDistance;

           
           var seq = DOTween.Sequence();                               // 순차 재생 컨테이너
           // a) 가속 구간: y0 → y1, Ease.OutCubic(점점 빨라짐)
           seq.Append( DOVirtual.Float(y0, y1, accelTime, (val) =>     // y 값을 시간에 따라 보간
           {
               _rawY = val;                                            // 원시 y 업데이트(계속 증가)
               ApplyWrappedPosition(_rawY);                            // 화면엔 wrap해서 적용 → “끝없이 도는 착시”
           }).SetEase(Ease.OutQuad) );   
           
           // b) 순항 구간: y1 → y2, Linear(등속)
           seq.Append( DOVirtual.Float(y1, y2, cruiseTime, (val) =>
           {
               _rawY = val;                                            // 등속으로 증가
               ApplyWrappedPosition(_rawY);                            // wrap 적용
           }).SetEase(Ease.Linear) );          
           
           // c) 감속 구간: y2 → y3, Ease.InCubic(점점 느려짐)
           seq.Append( DOVirtual.Float(y2, y3, decelTime, (val) =>
           {
               _rawY = val;                                            // 감속하며 증가
               ApplyWrappedPosition(_rawY);                            // wrap 적용
           }).SetEase(Ease.InQuad) );                                 // 감속 느낌

           // 시퀀스를 이 오브젝트와 연결(파괴 시 자동 Kill)
           seq.SetLink(gameObject);                                    // GameObject가 파괴되면 트윈도 같이 정리
           _spinTween = seq;                                           // 멤버에 보관(중복 방지/필요 시 Kill)
           await seq.AsyncWaitForCompletion();                         // 트윈 종료까지 대기(UniTask)
            
           // 미니게임 선택 완료 효과음
           SoundManager.Instance.PlaySFX(pickedMiniSfx.ToString());

           // ---- 스냅(정확히 칸 경계에 딱 맞추기) ----
           float snapped = Mathf.Round(_rawY / _cellHeight) * _cellHeight; // 가장 가까운 칸 경계로 반올림
           _rawY = snapped;                                            // 원시 y도 스냅 값으로 정리
           ApplyWrappedPosition(_rawY);                                 // 화면 위치 갱신(떨림/블러 방지)

           
           // 최종 멈춘 인덱스 계산: (랩핑된 y / 한 칸 높이)로 화면상 칸 번호 구함
           int finalIndex = Mathf.RoundToInt((Wrap(_rawY) / _cellHeight)) % count; // 0~count-1
           if (finalIndex < 0) finalIndex += count;                    // 음수 보정
           stoppedSlot = _ids[finalIndex];                             // 멈춘 슬롯 id 기록
           rowStopped  = true;                                         // 멈춤 플래그 true
           _spinTween = null;                                          // 트윈 참조 해제
           
           // Debug.Log($"멈춘 인덱스 == 타겟 인덱스여야 한다. : {finalIndex} == {target}");
            
        }
        
        // 화면에 적용할 때는 “원시 y”를 totalHeight로 모듈러해서 0~totalHeight 사이로 보정
        private float Wrap(float rawY)
        {
            if (_totalHeight <= 0f) return 0f;                          // 후보가 없을 때 가드
            float m = rawY % _totalHeight;                              // 모듈러(원시 y를 한 바퀴 범위로 축소)
            if (m < 0) m += _totalHeight;                               // 음수일 수 있으니 보정
            return m;                                                   // 0 ~ totalHeight
        }
        
        // 실제 anchoredPosition에 적용(화면은 -y로 올려 그만큼 아래로 스크롤된 느낌)
        private void ApplyWrappedPosition(float rawY)
        {
            float wrapped = Wrap(rawY);                                 // 0~총높이로 랩핑
            rowListRect.anchoredPosition = new Vector2(0, -wrapped);    // anchored Y에 wrapped 적용 → “무한히 도는 연출”
            
            
            //----- 칸 경계를 지날때 마다 소리가 나도록 ---- // 
            // 현재 칸 인덱스
            if (_ids.Count == 0 || _cellHeight <= 0f) return;
            int cell = Mathf.FloorToInt(wrapped / _cellHeight);

            if (cell != _lastTickCell)
            {
                // 과도재생 방지 (아주 고속 구간에서 too many ticks 방지)
                float t = Time.unscaledTime;
                if (t - _lastTickTime >= _minTickInterval)
                {
                    // 룰렛 도는 반복음 재생
                    SoundManager.Instance.PlaySFX(spinningRoulletSfx.ToString());
                    _lastTickTime = t;
                }

                _lastTickCell = cell;

            }
            
        }
        
    }
}