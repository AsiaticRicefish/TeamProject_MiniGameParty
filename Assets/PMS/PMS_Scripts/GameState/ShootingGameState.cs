using ShootingScene;
using PMS_Util;
public abstract class ShootingGameState : IGameState
{
    public abstract SH_GameStateType GameStateType { get; }
    //public abstract string Name { get; }

    public virtual void Enter() { }
    public virtual void Update() { }
    public virtual void Exit() { }
}
