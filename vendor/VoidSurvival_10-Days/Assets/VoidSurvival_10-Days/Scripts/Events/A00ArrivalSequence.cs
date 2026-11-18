using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// カット割りS5「到達」のQ〜Tを進行し、DEMO ENDで終了する。
/// 台詞はクリック進行。指定された沈黙は会話の所要時間とは別に確保する。
/// </summary>
[DefaultExecutionOrder(10000)] // CinemachineBrainのLateUpdate後にシェイクを適用する。
public class A00ArrivalSequence : MonoBehaviour
{
    [SerializeField] private GameObject explosionFlashPanel;
    [SerializeField] private GameObject statusPanel;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject humanNpc;
    [SerializeField] private GameObject blackoutPanel;
    [SerializeField] private TMP_Text demoEndText;

    private bool sequenceStarted;
    private Transform shakingCamera;
    private Vector3 cameraOriginalPosition;
    private Vector3 cameraShakeOffset;
    private MessageText activeMessageText;

    private static readonly List<string> ConfusionLines = new List<string>
    {
        "……なに",
        "……なんで、開いてるの",
    };

    private static readonly List<string> RecognitionLines = new List<string>
    {
        "……あなた、保守の……？",
        "……なんで、わたしを起こしたの",
    };

    private static readonly List<string> EtaQuestionLines = new List<string>
    {
        "……9日って、なに",
        "何が、9日なの",
    };

    private void Awake()
    {
        if (explosionFlashPanel != null) explosionFlashPanel.SetActive(false);
        if (statusPanel != null) statusPanel.SetActive(false);
        if (humanNpc != null) humanNpc.SetActive(false);
        if (blackoutPanel != null) blackoutPanel.SetActive(false);
        if (demoEndText != null) demoEndText.gameObject.SetActive(false);
    }

    private void Start()
    {
        StartCoroutine(RunSequence());
    }

    private IEnumerator RunSequence()
    {
        // 同じインスタンスで台詞・DEMO ENDを二重に再生しない。
        if (sequenceStarted) yield break;
        sequenceStarted = true;

        // Q: 白飛びは短時間で必ず解除する。
        if (explosionFlashPanel != null) explosionFlashPanel.SetActive(true);
        yield return new WaitForSeconds(0.3f);
        if (explosionFlashPanel != null) explosionFlashPanel.SetActive(false);
        yield return ShakeCamera();
        yield return ShowStatus("> PASSAGE CLEARED", 3f);
        yield return ShowStatus("> OBC — NO RESPONSE", 3f);

        // R-1〜R-4: 説明のテキストを載せない絵の間。
        yield return new WaitForSeconds(6f);
        yield return ShowStatus("> CRYO-LOCK — RELEASING\n> ......\n> RELEASED", 5f);
        if (humanNpc != null) humanNpc.SetActive(true);
        yield return new WaitForSeconds(4f);
        yield return ShowLines(ConfusionLines, 5f);
        yield return ShowLines(RecognitionLines, 5f);
        // R-8: 会話をゆっくり進めても、この沈黙は削らない。
        yield return new WaitForSeconds(5f);

        // S: 求められていないスキャンと、説明できないETA。
        yield return ShowStatus(FormatBiosignScan(1, 7, 46), 6f);
        yield return ShowLines(new List<string> { "……46？" }, 5f);
        if (ObservedValuesHud.Instance != null)
            ObservedValuesHud.Instance.SetEtaVisible(true);

        // 時刻が未初期化なら数値を捏造せず、最後の端末にも未取得を示す。
        string etaLine = "REENTRY ETA .... --";
        if (TimeManager.Instance != null)
        {
            var eta = TimeManager.Instance.GetReentryEta();
            etaLine = ObservedValuesHud.FormatEta(eta.days, eta.hours);
            yield return ShowStatus($"> {etaLine}", 4f);
        }
        yield return ShowLines(EtaQuestionLines, 5f);
        yield return new WaitForSeconds(5f); // S-5: 主人公は答えられない。
        yield return ShowLines(new List<string> { "……ねえ。誰か、説明できる人はいないの" }, 4f);
        // S-7: 台詞が閉じた後、何も表示せず4秒黙る。短縮しない。
        yield return new WaitForSeconds(4f);

        // T: ETAはS-3で取得した文字列を再利用する。
        yield return ShowLines(new List<string> { "……あなたが、来たの" }, 5f);
        yield return ShowLines(new List<string> { "……ぼろぼろじゃない" }, 5f);
        yield return ShowLines(new List<string> { "……ありがとう", "何が、なんだか。分からないけど" }, 4f);
        yield return ShowStatus(FormatFinalTerminalDisplay(etaLine, 2, 6), 3f);
        if (explosionFlashPanel != null) explosionFlashPanel.SetActive(false);
        if (blackoutPanel != null) blackoutPanel.SetActive(true);
        yield return new WaitForSeconds(1f);
        if (demoEndText != null)
        {
            demoEndText.text = "DEMO END";
            demoEndText.gameObject.SetActive(true);
        }
        // デモの終端。シーン遷移しない。
    }

    private IEnumerator ShowLines(List<string> lines, float minimumDuration)
    {
        var messageText = MessageText.Instance;
        if (messageText == null)
        {
            Debug.LogError("[A00ArrivalSequence] MessageText.Instance が見つかりません。該当台詞をスキップします。");
            yield break;
        }
        // MessageText.OpenDialog/CloseDialogが参照する依存先も確認する。
        if (MainUI.Instance == null || TimeManager.Instance == null)
        {
            Debug.LogError("[A00ArrivalSequence] MainUI.Instance または TimeManager.Instance が見つかりません。該当台詞をスキップします。");
            yield break;
        }

        float startedAt = Time.time;
        activeMessageText = messageText;
        yield return messageText.TextMessage(lines);
        activeMessageText = null;
        float remaining = minimumDuration - (Time.time - startedAt);
        if (remaining > 0f) yield return new WaitForSeconds(remaining);
    }

    private IEnumerator ShowStatus(string message, float duration)
    {
        if (statusText != null) statusText.text = message;
        if (statusPanel != null) statusPanel.SetActive(true);
        yield return new WaitForSeconds(duration);
        if (statusPanel != null) statusPanel.SetActive(false);
    }

    private IEnumerator ShakeCamera()
    {
        var mainCamera = Camera.main;
        if (mainCamera == null) yield break;

        shakingCamera = mainCamera.transform;
        cameraOriginalPosition = shakingCamera.localPosition;
        // Z方向を変えず、数フレームだけ小さく揺らす。
        for (int frame = 0; frame < 6; frame++)
        {
            if (shakingCamera == null) yield break;
            Vector2 offset = Random.insideUnitCircle * 0.1f;
            cameraShakeOffset = new Vector3(offset.x, offset.y, 0f);
            yield return null;
        }
        RestoreCamera();
    }

    private void LateUpdate()
    {
        // コルーチンから直接動かすとCinemachineに描画前に上書きされる。
        if (shakingCamera != null)
            shakingCamera.localPosition = cameraOriginalPosition + cameraShakeOffset;
    }

    private void RestoreCamera()
    {
        if (shakingCamera != null) shakingCamera.localPosition = cameraOriginalPosition;
        shakingCamera = null;
    }

    private void OnDisable()
    {
        // 白飛びやシェイクの途中で無効化されても、画面を覆い続けない。
        StopAllCoroutines();
        // MessageTextは永続UI。中断時も自分が表示した会話だけを閉じ、
        // 戻る履歴とタイマー停止を次のシーンへ持ち越さない。
        if (activeMessageText != null && activeMessageText.gameObject.activeSelf &&
            MainUI.Instance != null && TimeManager.Instance != null)
            activeMessageText.CloseDialog();
        activeMessageText = null;
        if (explosionFlashPanel != null) explosionFlashPanel.SetActive(false);
        if (statusPanel != null) statusPanel.SetActive(false);
        RestoreCamera();
    }

    internal static string FormatBiosignScan(int active, int viable, int lost)
    {
        return "> BIOSIGN SCAN / A0-0 CAPSULE ARRAY\n" +
            $">   ACTIVE ....... {active}\n" +
            $">   VIABLE ....... {viable}\n" +
            $">   LOST ......... {lost}";
    }

    internal static string FormatFinalTerminalDisplay(string etaLine, int readinessComplete, int readinessTotal)
    {
        return $"> {etaLine}\n" +
            $"> DESCENT READINESS : {readinessComplete} / {readinessTotal}\n" +
            "> ARCHIVE ......... NOT FOUND";
    }
}
