using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// カット割りS4「帯電を待つ」のN節をZONE-N初回訪問完了後に進行させる。
/// 台詞はクリックで進め、各節の指定秒数に満たない分は会話後に待つ。
/// 終了後はシーン遷移せず、プレイヤーの自由行動に委ねる。
/// </summary>
public class HubAChargeWaitSequence : MonoBehaviour
{
    private static readonly List<string> EnergyNeededLines = new List<string>
    {
        "爆薬がいる。爆薬には、電気の塊がいる",
        "……電気の塊が、いる",
    };

    private static readonly List<string> ChargingExplainLines = new List<string>
    {
        "面の温度差を見ろ。日向側と日陰側で、開いてきてる",
        "そうなると機体に電位差ができて、放電する",
        "この船が壊れたのも、元はそれでな",
    };

    private static readonly List<string> RetractionLines = new List<string>
    {
        "……さっき、相手にするなと言ったな",
        "撤回する",
        "電気の塊は、あいつらからしか取れない。壊せ",
    };

    private static readonly List<string> ObservationLines = new List<string>
    {
        "左上の数字を見ろ。MAGだ",
        "あれが小さいとき、外の連中は電気を帯びてる",
        "観測値を見ながら戦え。数字が戻ったら、ただの壊れた機体だ。取れるものは無い",
    };

    private static readonly List<string> RadiationRiskLines = new List<string>
    {
        "ただし、同じところで線量も上がる。……さっき君が食らったやつだ",
        "同じ条件だ。分けられん",
    };

    private static readonly List<string> ShelterLines = new List<string>
    {
        "行くなら、覚悟していけ",
        "戻る場所は覚えておけよ。岩陰でいい",
    };

    private static readonly List<string> FadingReminderLines = new List<string>
    {
        "……戻る場所は、覚えて……おけ",
    };

    private void Start()
    {
        StartCoroutine(RunSequence());
    }

    private IEnumerator RunSequence()
    {
        if (FlagData.Instance == null || !FlagData.Instance.zoneNFirstVisitComplete) yield break;
        // HUB-Aへ何度戻ってきても、この台詞は最初の1回だけ再生する。
        if (FlagData.Instance.hubAChargeWaitSequencePlayed) yield break;
        FlagData.Instance.hubAChargeWaitSequencePlayed = true;

        // N-1〜N-6: 前言撤回と、帯電の観測・リスク・退避先の説明。
        yield return ShowLines(EnergyNeededLines, 6f);
        yield return ShowLines(ChargingExplainLines, 8f);
        yield return ShowLines(RetractionLines, 10f);
        yield return ShowLines(ObservationLines, 10f);
        yield return ShowLines(RadiationRiskLines, 8f);
        yield return ShowLines(ShelterLines, 5f);

        // N-7: 一拍置いて、途切れながら同じ忠告を繰り返す。
        yield return new WaitForSeconds(2f);
        yield return ShowLines(FadingReminderLines, 5f);
    }

    private MessageText activeMessageText;

    private void OnDisable()
    {
        // 台詞表示の途中でシーン遷移・中断が起きても、ダイアログを開いたまま
        // TimeManagerを一時停止させ続けない。
        StopAllCoroutines();
        if (activeMessageText != null && activeMessageText.gameObject.activeSelf &&
            MainUI.Instance != null && TimeManager.Instance != null)
            activeMessageText.CloseDialog();
        activeMessageText = null;
    }

    private IEnumerator ShowLines(List<string> lines, float minimumDuration)
    {
        var messageText = MessageText.Instance;
        if (messageText == null)
        {
            Debug.LogError("[HubAChargeWaitSequence] MessageText.Instance が見つかりません。該当台詞をスキップします。");
            yield break;
        }
        // MessageText.OpenDialog/CloseDialogが参照する依存先も確認する。
        if (MainUI.Instance == null || TimeManager.Instance == null)
        {
            Debug.LogError("[HubAChargeWaitSequence] MainUI.Instance または TimeManager.Instance が見つかりません。該当台詞をスキップします。");
            yield break;
        }

        float startedAt = Time.time;
        activeMessageText = messageText;
        yield return messageText.TextMessage(lines);
        activeMessageText = null;
        float remaining = minimumDuration - (Time.time - startedAt);
        if (remaining > 0f) yield return new WaitForSeconds(remaining);
    }
}
