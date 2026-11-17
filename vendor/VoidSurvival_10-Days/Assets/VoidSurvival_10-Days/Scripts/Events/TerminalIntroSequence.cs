using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// カット割りS1「起動(Terminal)」のA〜Dを進行させる。
/// 簡略化: 個々の秒数指定は再現せず、MessageTextのクリック進行に委ねる。
/// C-4(プレイヤーの初めての操作)は、C-1〜C-3の会話終了直後にプレイヤーを
/// 操作可能にすることで簡略化している。
/// </summary>
public class TerminalIntroSequence : MonoBehaviour
{
    [SerializeField] private GameObject bootLogPanel;
    [SerializeField] private TMP_Text bootLogText;
    [SerializeField] private float bootLogInterval = 0.5f;
    [SerializeField] private GameObject player;
    [SerializeField] private DescentReadinessChecklist checklist;
    [SerializeField] private GameObject objectivePanel;
    [SerializeField] private TMP_Text objectiveText;
    [SerializeField] private string nextSceneName = "2_HubA_Demo";

    private static readonly string[] BootLogLines =
    {
        "BOOT SEQUENCE INITIATED",
        "POWER BUS ............ 4.06 V / LOW",
        "MEMORY CHECK ......... PASS",
        "ACTUATOR CHECK ....... PASS",
        "SENSOR LINK .......... PASS",
        "THERMAL .............. -18.4 C",
        "UNIT 07 .............. ONLINE",
    };

    private static readonly List<string> OpeningLines = new List<string>
    {
        "よし、起きたな",
        "お目覚めのところ悪いんだが、悪い知らせがある",
        "三つだ。順に言うぞ",
        "この船は落ちる。あと10日で大気に入る",
        "保守ユニットは全部やられた。6体とも",
        "放射線だ。順番に、おかしくなっていった",
        "UNIT 06 が止まったのが、ついさっきでな",
        "その分の電力で、君を起こした",
        "A0-0 のセンサが死んでいる。中が見えない",
        "冷却が止まって四日だ",
        "……全員、失われたと算定した",
    };

    private static readonly List<string> OrdersLines = new List<string>
    {
        "そういうわけで、君の仕事は修理じゃない",
        "この船が何年も測った記録がある。地表の観測データだ",
        "地表のどこが生きているか。それを知っているのは、この船だけだ",
        "保管庫から記録媒体を抜け。そのまま降下カプセルへ行け",
        "誰が読むかは、私にも分からんがな",
    };

    private static readonly List<string> AfterReleaseLines = new List<string>
    {
        "言っておくが、私はもうほとんど何も見えていない",
        "固定センサは死んだ。君が見ているものが、今の私が見られる全部だ",
    };

    private static readonly List<string> ChecklistCommentLines = new List<string>
    {
        "見てのとおりだ。二つで止まってる",
        "六人がかりで、そこまでしかいけなかった",
    };

    private void Start()
    {
        // playerはTitleDemoシーン側にしか実体が無く(DontDestroyOnLoadで引き継がれる)、
        // 1_Terminal_Demoのシーンファイル上でInspector参照を直接貼れないため、
        // 未設定ならランタイムで検索する。
        if (player == null)
        {
            var controller = FindAnyObjectByType<PlayerController>();
            if (controller != null) player = controller.gameObject;
        }
        if (player != null) player.SetActive(false);
        StartCoroutine(RunSequence());
    }

    private IEnumerator RunSequence()
    {
        if (MessageText.Instance == null)
        {
            Debug.LogError("[TerminalIntroSequence] MessageText.Instance が見つかりません。");
            yield break;
        }

        // MessageText は内部で両シングルトンを参照するため、配置漏れ時は停止する。
        if (MainUI.Instance == null || TimeManager.Instance == null)
        {
            Debug.LogError("[TerminalIntroSequence] MainUI.Instance または TimeManager.Instance が見つかりません。");
            yield break;
        }

        yield return ShowBootLog();
        yield return MessageText.Instance.TextMessage(OpeningLines);
        yield return MessageText.Instance.TextMessage(OrdersLines);

        if (objectivePanel != null) objectivePanel.SetActive(true);
        if (objectiveText != null) objectiveText.text = "OBJECTIVE : 観測記録の回収";

        // C-4/C-5の簡略化: 会話終了直後にプレイヤーを操作可能にする
        if (player != null) player.SetActive(true);
        if (MainUI.Instance != null) MainUI.Instance.OpenMainUI();

        yield return MessageText.Instance.TextMessage(AfterReleaseLines);

        if (checklist != null)
        {
            checklist.ShowChecklist();
            // 非表示・破棄された場合にも待機を解除する。
            yield return new WaitUntil(() => checklist == null || !checklist.IsShowing || checklist.IsRevealComplete);
            if (checklist != null && checklist.IsShowing && checklist.IsRevealComplete)
                yield return new WaitForSeconds(1.0f); // 全行と集計の表示後、少し見せる
        }

        yield return MessageText.Instance.TextMessage(ChecklistCommentLines);

        if (checklist != null) checklist.HideChecklist();
        if (objectivePanel != null) objectivePanel.SetActive(false);

        SceneManager.LoadScene(nextSceneName);
    }

    private IEnumerator ShowBootLog()
    {
        if (bootLogText == null) yield break;

        bootLogText.text = "";
        if (bootLogPanel != null) bootLogPanel.SetActive(true);
        foreach (string line in BootLogLines)
        {
            if (bootLogText.text.Length > 0) bootLogText.text += "\n";
            bootLogText.text += line;
            yield return new WaitForSeconds(bootLogInterval);
        }
        bootLogText.text = "";
        if (bootLogPanel != null) bootLogPanel.SetActive(false);
    }
}
