// Assets/KYG/KYG_Scripts/Game/MeteorTapSceneController.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KYG.Framework;
using UnityEngine;

namespace KYG
{
    
public class MeteorTapSceneController : BaseGameSceneController
{
    [Header("Scene Components")]
    [SerializeField] private KYG.MeteorTapMiniGame meteorTap;  // 필수
    [SerializeField] private MonoBehaviour[] otherComponents;  // 선택: 다른 IGameComponent/ICoroutineGameComponent 컴포넌트들

    protected override string GameType => "MeteorTap";

    /// <summary>
    /// 모든 매니저들이 깨어날 때까지 대기(프로젝트 상황에 맞게 확장).
    /// </summary>
    protected override IEnumerator WaitForManagersAwake()
    {
        // 필요 시 특정 싱글톤 대기 예시
        // yield return WaitForSingletonReady(typeof(KYG.TurnManager));
        // yield return WaitForSingletonReady(typeof(KYG.ShootingGameManager));
        yield return null;
    }

    /// <summary>
    /// 의존성 순서가 있는 컴포넌트들을 순차 초기화
    /// </summary>
    protected override IEnumerator InitializeSequentialManagers()
    {
        var seq = new List<IGameComponent>();

        if (meteorTap != null) seq.Add(meteorTap);

        // otherComponents 중 IGameComponent만 선별
        if (otherComponents != null)
        {
            foreach (var mb in otherComponents)
                if (mb is IGameComponent gc) seq.Add(gc);
        }

        yield return InitializeComponentsSafely(seq); // Base 제공 유틸
    }

    /// <summary>
    /// 독립적인(비동기) 컴포넌트들을 병렬 초기화
    /// </summary>
    protected override IEnumerator InitializeParallelManagers()
    {
        var parallel = new List<ICoroutineGameComponent>();

        if (meteorTap != null) parallel.Add(meteorTap);

        if (otherComponents != null)
        {
            foreach (var mb in otherComponents)
                if (mb is ICoroutineGameComponent cc) parallel.Add(cc);
        }

        yield return InitializeCoroutineComponentsSafely(parallel); // Base 제공 유틸
    }

    /// <summary>
    /// 모든 플레이어 초기화 완료 후, 마스터가 StartGame RPC를 날리면 호출됨.
    /// - 최초 턴 세팅/배너 표기 등 실제 게임 시작 신호
    /// </summary>
    protected override void NotifyGameStart()
    {
        // 예: 마스터가 첫 턴을 설정하고, 각 클라에서 InitTurnWithEnding 호출 트리거
        // 여기서는 TurnManager가 기존과 동일하게 흐름을 생성한다는 가정.
        // 필요 시, 로컬 폴백도 고려 가능.
        Debug.Log("[MeteorTap] GameStart!");
    }
}
}
