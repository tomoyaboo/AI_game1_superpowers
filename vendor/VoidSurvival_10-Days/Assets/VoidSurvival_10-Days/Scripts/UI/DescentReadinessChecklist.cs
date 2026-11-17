using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// カット割りS1-D「DESCENT READINESS」の全画面チェックリスト表示。
/// 6行を0.5秒間隔で上から順に表示する。
/// </summary>
public class DescentReadinessChecklist : MonoBehaviour
{
    [System.Serializable]
    public struct ChecklistItem
    {
        public string label;
        public string unitName;
        /// <summary>"完了" / "中断" / "未着手" のいずれか。</summary>
        public string status;
    }

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text linesText;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private float lineInterval = 0.5f;

    [SerializeField]
    private List<ChecklistItem> items = new List<ChecklistItem>
    {
        new ChecklistItem { label = "降下カプセル 点検", unitName = "UNIT 01", status = "完了" },
        new ChecklistItem { label = "切離機構 確認", unitName = "UNIT 02", status = "完了" },
        new ChecklistItem { label = "姿勢制御 較正", unitName = "UNIT 03", status = "中断" },
        new ChecklistItem { label = "電源系 修復", unitName = "UNIT 04", status = "中断" },
        new ChecklistItem { label = "熱制御 確認", unitName = "UNIT 05", status = "未着手" },
        new ChecklistItem { label = "人員 覚醒", unitName = "UNIT 06", status = "未着手" },
    };

    public bool IsShowing { get; private set; }
    public bool IsRevealComplete { get; private set; }

    private Coroutine revealCoroutine;

    public void ShowChecklist()
    {
        if (revealCoroutine != null) StopCoroutine(revealCoroutine);
        IsRevealComplete = false;
        if (linesText != null)
        {
            linesText.text = "";
            linesText.richText = true;
        }
        if (summaryText != null) summaryText.text = "";
        if (panel != null) panel.SetActive(true);
        IsShowing = true;
        revealCoroutine = StartCoroutine(RevealLines());
    }

    public void HideChecklist()
    {
        if (revealCoroutine != null) StopCoroutine(revealCoroutine);
        revealCoroutine = null;
        IsShowing = false;
        IsRevealComplete = false;
        if (panel != null) panel.SetActive(false);
    }

    private IEnumerator RevealLines()
    {
        var shown = new List<string>();
        int completeCount = 0;
        foreach (var item in items)
        {
            string color = item.status == "完了" ? "green" : "red";
            shown.Add($"<color={color}>{FormatLine(item.label, item.unitName, item.status)}</color>");
            if (item.status == "完了") completeCount++;
            if (linesText != null) linesText.text = string.Join("\n", shown);
            yield return new WaitForSeconds(lineInterval);
        }
        if (summaryText != null) summaryText.text = FormatSummary(completeCount, items.Count);
        IsRevealComplete = true;
        revealCoroutine = null;
    }

    // カット割り原文の各行をそのまま再現する(ドット数は行ごとに異なる。
    // 元のテキストの見た目の桁揃えをそのまま踏襲しているため、計算式にせず
    // 原文の文字列をそのまま組み立てること)。
    internal static string FormatLine(string label, string unitName, string status)
    {
        string mark = status == "完了" ? "[✓]" : "[ ]";
        string dots = label switch
        {
            "降下カプセル 点検" => ".......",
            "切離機構 確認" => "..........",
            "姿勢制御 較正" => "..........",
            "電源系 修復" => "............",
            "熱制御 確認" => "............",
            "人員 覚醒" => "..............",
            _ => "......",
        };
        return $"{mark} {label} {dots} {unitName} / {status}";
    }

    internal static string FormatSummary(int completeCount, int totalCount)
    {
        return $"READY : {completeCount} / {totalCount}";
    }
}
