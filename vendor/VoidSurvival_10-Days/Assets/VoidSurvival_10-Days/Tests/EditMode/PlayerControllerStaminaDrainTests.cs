using NUnit.Framework;

public class PlayerControllerStaminaDrainTests
{
    [Test]
    public void CalculateStaminaDrain_AccumulatesBelowOne_ConsumesNothingYet()
    {
        var (amount, remaining) = PlayerController.CalculateStaminaDrain(
            accumulator: 0f, drainPerSecond: 10f, deltaTime: 0.05f);
        Assert.AreEqual(0, amount);
        Assert.AreEqual(0.5f, remaining, 0.001f);
    }

    [Test]
    public void CalculateStaminaDrain_AccumulatorReachesOne_ConsumesOne()
    {
        var (amount, remaining) = PlayerController.CalculateStaminaDrain(
            accumulator: 0.6f, drainPerSecond: 10f, deltaTime: 0.1f);
        Assert.AreEqual(1, amount);
        Assert.AreEqual(0.6f, remaining, 0.001f);
    }

    [Test]
    public void CalculateStaminaDrain_SameElapsedTime_SameTotalConsumptionRegardlessOfFrameRate()
    {
        float drainPerSecond = 10f;

        float acc60 = 0f;
        int total60 = 0;
        for (int i = 0; i < 60; i++)
        {
            var (amount, remaining) = PlayerController.CalculateStaminaDrain(acc60, drainPerSecond, 1f / 60f);
            total60 += amount;
            acc60 = remaining;
        }

        float acc30 = 0f;
        int total30 = 0;
        for (int i = 0; i < 30; i++)
        {
            var (amount, remaining) = PlayerController.CalculateStaminaDrain(acc30, drainPerSecond, 1f / 30f);
            total30 += amount;
            acc30 = remaining;
        }

        Assert.AreEqual(total60, total30);
    }
}
