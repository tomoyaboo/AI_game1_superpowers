using NUnit.Framework;

public class PlayerShelterStateTests
{
    [SetUp]
    public void Reset()
    {
        // テスト間で静的状態が残らないよう、0になるまでExitしておく
        while (PlayerShelterState.IsSheltered) PlayerShelterState.Exit();
    }

    [Test]
    public void IsSheltered_Initially_False()
    {
        Assert.IsFalse(PlayerShelterState.IsSheltered);
    }

    [Test]
    public void Enter_SetsSheltered_True()
    {
        PlayerShelterState.Enter();
        Assert.IsTrue(PlayerShelterState.IsSheltered);
    }

    [Test]
    public void Exit_AfterSingleEnter_SetsSheltered_False()
    {
        PlayerShelterState.Enter();
        PlayerShelterState.Exit();
        Assert.IsFalse(PlayerShelterState.IsSheltered);
    }

    [Test]
    public void OverlappingZones_StillShelteredUntilAllExited()
    {
        // 2つの遮蔽物エリアが重なっている状況を想定(参照カウント方式)
        PlayerShelterState.Enter();
        PlayerShelterState.Enter();
        PlayerShelterState.Exit();
        Assert.IsTrue(PlayerShelterState.IsSheltered, "1つ目のエリアから出てもまだ2つ目の中にいる");
        PlayerShelterState.Exit();
        Assert.IsFalse(PlayerShelterState.IsSheltered, "両方から出たら遮蔽されていない");
    }

    [Test]
    public void Exit_WithoutEnter_DoesNotGoNegative()
    {
        // 対応するEnterが無いExitが呼ばれても、カウントが負にならずShelteredはfalseのまま
        PlayerShelterState.Exit();
        Assert.IsFalse(PlayerShelterState.IsSheltered);
    }
}
