using NUnit.Framework;

public class DescentReadinessChecklistTests
{
    [Test]
    public void FormatLine_Complete_ReturnsCheckmarkAndLabel()
    {
        string result = DescentReadinessChecklist.FormatLine("降下カプセル 点検", "UNIT 01", "完了");
        Assert.AreEqual("[✓] 降下カプセル 点検 ....... UNIT 01 / 完了", result);
    }

    [Test]
    public void FormatLine_Suspended_ReturnsBracketAndSuspendedLabel()
    {
        string result = DescentReadinessChecklist.FormatLine("姿勢制御 較正", "UNIT 03", "中断");
        Assert.AreEqual("[ ] 姿勢制御 較正 .......... UNIT 03 / 中断", result);
    }

    [Test]
    public void FormatLine_NotStarted_ReturnsBracketAndNotStartedLabel()
    {
        string result = DescentReadinessChecklist.FormatLine("熱制御 確認", "UNIT 05", "未着手");
        Assert.AreEqual("[ ] 熱制御 確認 ............ UNIT 05 / 未着手", result);
    }

    [Test]
    public void FormatSummary_TwoOfSix_ReturnsExpectedFormat()
    {
        string result = DescentReadinessChecklist.FormatSummary(2, 6);
        Assert.AreEqual("READY : 2 / 6", result);
    }
}
