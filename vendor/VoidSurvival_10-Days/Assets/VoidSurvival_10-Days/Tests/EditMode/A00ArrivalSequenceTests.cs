using NUnit.Framework;

public class A00ArrivalSequenceTests
{
    [Test]
    public void FormatBiosignScan_MatchesSpec()
    {
        string expected =
            "> BIOSIGN SCAN / A0-0 CAPSULE ARRAY\n" +
            ">   ACTIVE ....... 1\n" +
            ">   VIABLE ....... 7\n" +
            ">   LOST ......... 46";
        Assert.AreEqual(expected, A00ArrivalSequence.FormatBiosignScan(1, 7, 46));
    }

    [Test]
    public void FormatFinalTerminalDisplay_MatchesSpec()
    {
        string expected =
            "> REENTRY ETA .... 9D 00H\n" +
            "> DESCENT READINESS : 2 / 6\n" +
            "> ARCHIVE ......... NOT FOUND";
        Assert.AreEqual(expected, A00ArrivalSequence.FormatFinalTerminalDisplay("REENTRY ETA .... 9D 00H", 2, 6));
    }
}
