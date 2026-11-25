/// <summary>
/// 弧月の近接攻撃を受け取る側が実装するインターフェース。
/// 現行プロジェクトには汎用の敵HP/ダメージ受信APIが見つからないため、
/// 敵側のHPシステムへ接続する境界として独立させている。
/// </summary>
public interface IKogetsuDamageReceiver
{
    void ReceiveKogetsuDamage(KogetsuDamageInfo damageInfo);
}
