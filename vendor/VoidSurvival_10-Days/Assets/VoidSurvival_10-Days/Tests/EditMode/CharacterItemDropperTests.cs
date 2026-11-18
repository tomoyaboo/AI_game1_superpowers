using NUnit.Framework;

public class CharacterItemDropperTests
{
    [Test]
    public void ShouldAttemptEnergyMaterialDrop_NonEnergyMaterial_AlwaysTrue()
    {
        Assert.IsTrue(CharacterItemDropper.ShouldAttemptEnergyMaterialDrop(isEnergyMaterial: false, esdActive: false));
        Assert.IsTrue(CharacterItemDropper.ShouldAttemptEnergyMaterialDrop(isEnergyMaterial: false, esdActive: true));
    }

    [Test]
    public void ShouldAttemptEnergyMaterialDrop_EnergyMaterialWithoutEsd_False()
    {
        Assert.IsFalse(CharacterItemDropper.ShouldAttemptEnergyMaterialDrop(isEnergyMaterial: true, esdActive: false));
    }

    [Test]
    public void ShouldAttemptEnergyMaterialDrop_EnergyMaterialWithEsd_True()
    {
        Assert.IsTrue(CharacterItemDropper.ShouldAttemptEnergyMaterialDrop(isEnergyMaterial: true, esdActive: true));
    }
}
