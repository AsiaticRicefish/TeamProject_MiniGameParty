using ShootingScene;

public abstract class ShootingGameState : IGameState
{ 
    public virtual void Enter() { }
    public virtual void Update() { }
    public virtual void Exit() { }
}
