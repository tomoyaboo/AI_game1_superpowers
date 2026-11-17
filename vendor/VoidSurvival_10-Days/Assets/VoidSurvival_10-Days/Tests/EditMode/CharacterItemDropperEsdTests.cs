using NUnit.Framework;

public class CharacterItemDropperEsdTests
{
    [Test]
    public void ApplyEsdBonus_MultipliesRate()
    {
        Assert.AreEqual(0.6f, CharacterItemDropper.ApplyEsdBonus(0.4f, 1.5f), 0.001f);
    }

    [Test]
    public void ApplyEsdBonus_NeverExceedsOne()
    {
        Assert.LessOrEqual(CharacterItemDropper.ApplyEsdBonus(0.9f, 1.5f), 1.0f);
    }
}
