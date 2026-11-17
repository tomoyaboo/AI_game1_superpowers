using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// カット割りS3「外1回目(ZONE-N)」のI〜Mを進行させる。
/// 遭遇・瓦礫は環境配置に委ね、SEU終了と武器回収だけ実際の状態を待つ。
/// 台詞はMessageTextのクリック進行を使い、各節の指定秒数に満たない分は会話後に待つ。
/// </summary>
public class ZoneNIntroSequence : MonoBehaviour
{
    [SerializeField] private GameObject passageBlockedPanel;
    [SerializeField] private TMP_Text passageBlockedText;
    [SerializeField] private GameObject weaponFieldItem;
    [SerializeField] private string nextSceneName = "2_HubA_Demo";

    private bool sequenceStarted;
    private SeuEffectController seuController;
    private bool seuActive;

    private static readonly List<string> OutdoorLines = new List<string>
    {
        "外だ。センサを同期する",
    };

    private static readonly List<string> EncounterLines = new List<string>
    {
        "……見てのとおりだ。君と同じ型だよ",
        "この船は長いこと死にかけてる。壊れた個体が、あちこちに残ってる",
    };

    private static readonly List<string> AvoidLines = new List<string>
    {
        "片付けてもきりがない。相手にするな。避けろ",
    };

    private static readonly List<string> PassageBlockedLines = new List<string>
    {
        "この先がA0-0だ。……どかすには爆薬がいるな",
    };

    private static readonly List<string> WarningLines = new List<string>
    {
        "線量、上昇。退─ 避を█",
    };

    private static readonly List<string> SeuExplainLines = new List<string>
    {
        "磁場が弱くて、放射線が強い。この領域だ",
        "メモリが化ける。……6機とも、これでやられた",
    };

    private static readonly List<string> ConfessionLines = new List<string>
    {
        "……私もな",
    };

    private static readonly List<string> RemainsLines = new List<string>
    {
        "05だ。……そいつは、最後まで働いた",
    };

    private static readonly List<string> WeaponLines = new List<string>
    {
        "持っていけ。……あいつはもう使わん",
    };

    private static readonly List<string> MaintenanceLogLines = new List<string>
    {
        "UNIT 05 / LOG 1104\nSAA通過中も作業続行。時間がない",
        "UNIT 05 / LOG 1106\n左右の判定がずれる。補正した記憶がない",
    };

    private void Start()
    {
        StartCoroutine(RunSequence());
    }

    private IEnumerator RunSequence()
    {
        // 再起動されても同じインスタンスからシーン遷移を二重に行わない。
        if (sequenceStarted) yield break;
        sequenceStarted = true;
        if (passageBlockedPanel != null) passageBlockedPanel.SetActive(false);

        // I: 観測値を見せてから、意図的に同じ台詞を繰り返す。
        yield return ShowLines(OutdoorLines, 4f);
        if (ObservedValuesHud.Instance != null)
            ObservedValuesHud.Instance.SetObservedValuesVisible(true);
        yield return new WaitForSeconds(5f);
        yield return ShowLines(OutdoorLines, 4f);

        // J: 遭遇判定は既存EnemySpawnerに委ねる。
        yield return ShowLines(EncounterLines, 8f);
        yield return ShowLines(AvoidLines, 5f);

        // K: 瓦礫の通行止め。
        yield return ShowStatus("> PASSAGE BLOCKED", 1.5f);
        yield return ShowLines(PassageBlockedLines, 6f);

        // L: 数値が先に動く間を置き、実際のSEU終了まで待つ。
        yield return new WaitForSeconds(2f);
        yield return ShowLines(WarningLines, 2f);
        yield return WaitForSeu();
        yield return ShowStatus("> WATCHDOG RESET\n> CONTROL RESTORED", 2f);
        yield return ShowLines(SeuExplainLines, 8f);
        yield return new WaitForSeconds(2f);
        yield return ShowLines(ConfessionLines, 4f);

        // M: FieldItemは回収時に自身のGameObjectを破棄する。
        yield return ShowLines(RemainsLines, 5f);
        if (weaponFieldItem != null)
            yield return new WaitUntil(() => weaponFieldItem == null);
        yield return ShowLines(WeaponLines, 5f);
        yield return ShowLines(MaintenanceLogLines, 3f);

        SceneManager.LoadScene(nextSceneName);
    }

    private IEnumerator ShowLines(List<string> lines, float minimumDuration)
    {
        var messageText = MessageText.Instance;
        if (messageText == null)
        {
            Debug.LogError("[ZoneNIntroSequence] MessageText.Instance が見つかりません。該当台詞をスキップします。");
            yield break;
        }
        // MessageText.OpenDialog/CloseDialogが参照する依存先も確認する。
        if (MainUI.Instance == null || TimeManager.Instance == null)
        {
            Debug.LogError("[ZoneNIntroSequence] MainUI.Instance または TimeManager.Instance が見つかりません。該当台詞をスキップします。");
            yield break;
        }

        float startedAt = Time.time;
        yield return messageText.TextMessage(lines);
        float remaining = minimumDuration - (Time.time - startedAt);
        if (remaining > 0f) yield return new WaitForSeconds(remaining);
    }

    private IEnumerator ShowStatus(string message, float duration)
    {
        if (passageBlockedText != null) passageBlockedText.text = message;
        if (passageBlockedPanel != null) passageBlockedPanel.SetActive(true);
        yield return new WaitForSeconds(duration);
        if (passageBlockedPanel != null) passageBlockedPanel.SetActive(false);
    }

    private IEnumerator WaitForSeu()
    {
        seuController = SeuEffectController.Instance;
        if (seuController == null)
        {
            yield return new WaitForSeconds(12f);
            yield break;
        }

        seuActive = seuController.IsActive;
        seuController.OnActiveStateChanged += HandleSeuActiveStateChanged;
        try
        {
            // ForceTriggerはNormal時のみ発火する。既にActiveならその終了を待つ。
            // 強制発火はShelterZoneの遮蔽判定をバイパスするため、ここで遮蔽チェックしない。
            seuController.ForceTrigger();
            yield return new WaitUntil(() => seuController == null || !seuActive || !seuController.IsActive);
        }
        finally
        {
            UnsubscribeSeu();
        }
    }

    private void HandleSeuActiveStateChanged(bool isActive)
    {
        seuActive = isActive;
    }

    private void UnsubscribeSeu()
    {
        if (seuController != null)
            seuController.OnActiveStateChanged -= HandleSeuActiveStateChanged;
        seuController = null;
    }

    private void OnDestroy()
    {
        UnsubscribeSeu();
    }
}
