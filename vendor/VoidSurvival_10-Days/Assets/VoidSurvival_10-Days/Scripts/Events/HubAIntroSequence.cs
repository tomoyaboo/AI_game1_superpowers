using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// カット割りS2「声(HUB-A)」のE〜Hを進行させる。
/// 簡略化: 個々の秒数指定は再現せず、MessageTextのクリック進行に委ねる。
/// E-3(クラフト/作業台チュートリアル)は既存の仕組みに委ね、本シーケンスは
/// 関与しない。
/// </summary>
public class HubAIntroSequence : MonoBehaviour
{
    [SerializeField] private GameObject archivePanel;
    [SerializeField] private TMP_Text archiveText;
    [SerializeField] private GameObject signalPanel;
    [SerializeField] private TMP_Text signalText;
    [SerializeField] private GameObject priorityPanel;
    [SerializeField] private TMP_Text priorityText;
    [SerializeField] private string nextSceneName = "3_ZoneN_Demo";

    private CommunicationDialog communicationDialog;
    private bool firstCommSuccess;

    private static readonly List<string> ArrivalLines = new List<string>
    {
        "作業区画だ。ここはまだ生きてる",
        "保管庫はこの奥だ",
    };

    private static readonly List<string> WorkbenchExplainLines = new List<string>
    {
        "手で組めるものは、手で組め",
        "台がいるのは、細かい加工のほうだ。素材を作り替えるやつはこっちでやれ",
    };

    private static readonly List<string> ArchiveMissingLines = new List<string>
    {
        "……無い",
        "保管庫にあるはずだ。私はそう記録している",
        "……いや。私が最後にそれを見たのは、いつだ",
        "……誰かが持ち出した。六機のうちの誰かか、あるいは",
        "……後回しだ。今は分からん",
    };

    private static readonly List<string> SignalDetectedLines = new List<string>
    {
        "待て。ついでだ",
        "……もう一つ、引っかかってることがある",
        "A0-0の帯域に、信号が乗ってる。四日前からだ",
        "4.2秒ごとに、きっちり同じものを繰り返してる。機械が出してるやつだな",
        "復調系が焼けててな。中身が読めん",
        "自動では中身まで取れん。手で合わせるしかない",
        "合わせてみろ。……無駄骨だったら、すまん",
    };

    private static readonly List<string> AfterBeaconReadLines = new List<string>
    {
        "……待て",
        "四日だ。四日間、そう言い続けてたのか",
        "……算定が、間違っていた",
    };

    private static readonly string HumanVoiceLine = "…だれ……そこに……いるの……";

    private static readonly List<string> AfterPriorityUpdateLines = new List<string>
    {
        "悪かったな。読まずに、決めつけた",
        "四日、鳴りっぱなしだったのにな",
        "発信源はA0-0。カプセルの中からだ",
        "外からしか開かん。安全機構だ",
        "直通路は塞がってる。外を回るしかない",
        "一つ、言っておく",
        "最上位指令は、君の保全より上だ",
        "一度立った以上、止まらん。壊れるまでだ",
        "……六機とも、そうやって出ていった",
        "ああ、それと",
        "君の席は、なくなったな",
    };

    private static readonly List<string> FinalLines = new List<string>
    {
        "行ってくれ。頼んだぞ",
    };

    private void Start()
    {
        // CommunicationDialogはAwake()でgameObject.SetActive(false)になるため、
        // 非活性のオブジェクトも検索対象に含める必要がある。
        communicationDialog = FindAnyObjectByType<CommunicationDialog>(FindObjectsInactive.Include);
        if (communicationDialog != null)
        {
            // 購読する前に既に初回成功済みだった場合(シーン再読込等の想定外ケース)に
            // 待機し続けないようにする。
            firstCommSuccess = communicationDialog.HasSucceededOnce;
            communicationDialog.OnFirstSuccess += () => { firstCommSuccess = true; };
        }
        StartCoroutine(RunSequence());
    }

    private IEnumerator RunSequence()
    {
        if (MessageText.Instance == null)
        {
            Debug.LogError("[HubAIntroSequence] MessageText.Instance が見つかりません。");
            yield break;
        }

        yield return MessageText.Instance.TextMessage(ArrivalLines);
        yield return MessageText.Instance.TextMessage(WorkbenchExplainLines);

        // F節: 保管庫が空(伏線)
        if (archivePanel != null) archivePanel.SetActive(true);
        if (archiveText != null)
        {
            archiveText.text = "> ARCHIVE SLOT 01 ..... EMPTY\n> ARCHIVE SLOT 02 ..... EMPTY\n> ARCHIVE SLOT 03 ..... EMPTY";
        }
        yield return MessageText.Instance.TextMessage(ArchiveMissingLines);
        if (archivePanel != null) archivePanel.SetActive(false);

        // G節: 通信
        yield return MessageText.Instance.TextMessage(SignalDetectedLines);

        if (signalPanel != null) signalPanel.SetActive(true);
        if (signalText != null)
        {
            signalText.text = "> SIGNAL DETECTED — A0-0 BAND\n> PATTERN : REPEATING / 4.2s INTERVAL\n> DEMOD ....... FAILED\n> CONTENT ..... UNREADABLE";
        }

        if (communicationDialog != null)
        {
            communicationDialog.OpenDialog();
            yield return new WaitUntil(() => firstCommSuccess);
            communicationDialog.CloseDialog();
        }
        else
        {
            Debug.LogError("[HubAIntroSequence] CommunicationDialog が見つかりません。処理を続行します。");
        }

        if (signalText != null)
        {
            signalText.text = "> DEMOD ....... OK\n> BEACON : A0-0 / CAP-17 / CRYO-FAIL / LIFE-PRESENT";
        }
        yield return new WaitForSeconds(1.5f);

        yield return MessageText.Instance.TextMessage(AfterBeaconReadLines);

        yield return MessageText.Instance.TextMessage(new List<string> { HumanVoiceLine });

        if (signalPanel != null) signalPanel.SetActive(false);

        if (priorityPanel != null) priorityPanel.SetActive(true);
        if (priorityText != null)
        {
            priorityText.text = "> PRIORITY UPDATE\n>   3. SELF_REPAIR ........ 実行中\n>   ─────────────────────\n>   1. SAVE_HUMAN ......... 検出 → 昇格\n>   3. SELF_REPAIR ........ 保留";
        }
        yield return new WaitForSeconds(1.5f);

        yield return MessageText.Instance.TextMessage(AfterPriorityUpdateLines);

        if (priorityPanel != null) priorityPanel.SetActive(false);

        // H節: 時計が動き出す
        if (ObservedValuesHud.Instance != null)
        {
            ObservedValuesHud.Instance.SetEtaVisible(true);
        }
        yield return MessageText.Instance.TextMessage(FinalLines);

        SceneManager.LoadScene(nextSceneName);
    }
}
