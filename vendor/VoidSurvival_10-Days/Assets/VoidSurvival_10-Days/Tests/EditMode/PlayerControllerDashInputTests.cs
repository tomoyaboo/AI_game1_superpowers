using NUnit.Framework;

public class PlayerControllerDashInputTests
{
    [Test]
    public void IsDashRequested_DashButtonOnly_ReturnsTrue()
    {
        Assert.IsTrue(PlayerController.IsDashRequested(dashButtonToggled: true, keyboardHeld: false));
    }

    [Test]
    public void IsDashRequested_KeyboardOnly_ReturnsTrue()
    {
        Assert.IsTrue(PlayerController.IsDashRequested(dashButtonToggled: false, keyboardHeld: true));
    }

    [Test]
    public void IsDashRequested_Neither_ReturnsFalse()
    {
        Assert.IsFalse(PlayerController.IsDashRequested(dashButtonToggled: false, keyboardHeld: false));
    }
}
