/// <summary>
/// プレイヤーが遮蔽物(ShelterZone)の中にいるかどうかを保持する共有状態。
/// 複数のエリアが重なる可能性があるため参照カウント方式にする。
/// </summary>
public static class PlayerShelterState
{
    private static int _overlapCount = 0;

    public static bool IsSheltered => _overlapCount > 0;

    public static void Enter()
    {
        _overlapCount++;
    }

    public static void Exit()
    {
        _overlapCount = UnityEngine.Mathf.Max(0, _overlapCount - 1);
    }
}
