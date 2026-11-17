using NUnit.Framework;

public class EnemySpawnerRespawnTests
{
    [Test]
    public void ShouldRespawn_AliveCountBelowTarget_ReturnsTrue()
    {
        Assert.IsTrue(EnemySpawner.ShouldRespawn(aliveCount: 2, targetCount: 3));
    }

    [Test]
    public void ShouldRespawn_AliveCountEqualsTarget_ReturnsFalse()
    {
        Assert.IsFalse(EnemySpawner.ShouldRespawn(aliveCount: 3, targetCount: 3));
    }

    [Test]
    public void ShouldRespawn_AliveCountAboveTarget_ReturnsFalse()
    {
        // 天候が変わって目標数が減った場合など。既存の生存体を消す必要はないが、
        // 新たに補充もしない。
        Assert.IsFalse(EnemySpawner.ShouldRespawn(aliveCount: 5, targetCount: 3));
    }
}
