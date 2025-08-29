namespace ShootingScene
{
    public interface IGameState
    {
        void Enter();    // 진입 시 호출
        void Update();   // 매 프레임마다 실행
        void Exit();     // 종료 시 호출
    }
}
