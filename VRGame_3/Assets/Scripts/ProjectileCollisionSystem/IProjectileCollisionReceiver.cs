/// <summary>
/// ProjectileCollisionSystem から衝突通知を受け取るコンポーネントが実装するインターフェース。
/// 衝突後の挙動（停止、軌跡のフェード待ち、破棄、爆発、反射など）は実装側で決める。
/// </summary>
public interface IProjectileCollisionReceiver
{
    void OnProjectileCollision(ProjectileCollisionInfo collisionInfo);
}
