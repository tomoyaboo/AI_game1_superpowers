# S2 声(HUB-A) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **この計画はCodex CLI（`codex exec`）が実行する。** Claude Code（オーケストレーター）が
> 本ファイルを渡し、Codexが実装、Claude Codeが差分レビューとコンパイル/テスト実行結果の
> 確認（Unityバッチモードは直接実行）を行う。

**Goal:** カット割り S2「声（HUB-A）」のE〜Hを実装する。保管庫が空という伏線、
既存の通信ミニゲームに繋げた指令書き換えイベント、ETA表示の解放を行う。

## スコープと簡略化

- E-3(クラフト/作業台チュートリアル)は**既存の仕組みをそのまま使う**。新規実装しない
- G-4(通信ミニゲーム)は**既存の`CommunicationDialog`をそのまま使う**。
  改造するのは「最初の1回成功したときに通知するイベント」の追加のみ。既存の
  繰り返し通信・英語の成功メッセージ("Successful communication established!"等)は
  変更しない
- 個々の秒数指定・無音の演出は再現せず、`MessageText`のクリック進行に委ねる
  （S1と同じ簡略化方針）

**Architecture:** `CommunicationDialog`に `OnFirstSuccess`（一度だけ発火する
`System.Action`）を追加する。新規`HubAIntroSequence`がこれを待ち受け、
E→F→G→Hの流れを`MessageText.Instance.TextMessage(...)`で進行させる。
ETA表示の解放は、Task「共通HUD」で実装済みの
`ObservedValuesHud.Instance.SetEtaVisible(true)` を呼ぶだけで良い
（新規UI不要）。

**Tech Stack:** Unity 2023.1.22f1, C#, TextMeshPro, Unity Test Framework

**Spec:** `docs/superpowers/specs/2026-11-17-voidsurvival-ai-collab-design.md`
（新規タスク）。`docs/VoidSurvival_カット割り_2.md` 第2部 S2節(E〜H)。

## Global Constraints

- 作業ディレクトリは `vendor/VoidSurvival_10-Days/`
- upstream への push・merge は行わない
- **`CommunicationDialog`の既存の挙動(成功判定・繰り返し通信・既存の英語メッセージ)は
  一切変更しない。** 追加するのは「最初の1回だけ発火するイベント」のみ
- シーンファイル(.unity)は変更しない（Claude Codeが別途行う）
- 台詞はカット割りの文言をそのまま使う
- Unityバッチモードのコンパイル確認・テスト実行は **Claude Codeが直接実行する**。
  Codexは実装のみ行い、コミットもしない

## Review Focus

- **`OnFirstSuccess`が2回目以降の成功では発火しないこと**（1回限りであること）
- **既存の`OnSuccessfulCommunication()`内の既存処理(successCount加算、
  daily limit判定、既存メッセージ表示、GenerateNewTarget呼び出し)の順序・内容が
  変わっていないこと**
- **`HubAIntroSequence`が`CommunicationDialog`/`MessageText`/`ObservedValuesHud`が
  見つからない場合に例外を出さないこと**
- 既存のテスト(現時点で63件)が引き続きパスすること

---

### Task 1: `CommunicationDialog` に初回成功イベントを追加

**Files:**
- Modify: `Assets/VoidSurvival_10-Days/Scripts/UI/Communication/CommunicationDialog.cs`

**Interfaces:**
- Produces: `CommunicationDialog.OnFirstSuccess` (`public event System.Action`),
  `CommunicationDialog.HasSucceededOnce { get; }` (bool)

- [ ] **Step 1: フィールドとイベントを追加**

既存の `private bool isSuccessful = false;` の近くに追加:

```csharp
public event System.Action OnFirstSuccess;
public bool HasSucceededOnce { get; private set; } = false;
```

- [ ] **Step 2: `OnSuccessfulCommunication()` の先頭に発火処理を追加**

既存の

```csharp
private IEnumerator OnSuccessfulCommunication()
{
    isSuccessful = true;
    successCount++;
    freqHandle.isDraggable = false;
    dirHandle.isDraggable = false;
```

の直後(既存の `if (successCount >= dailyLimit)` より前)に追加:

```csharp
    if (!HasSucceededOnce)
    {
        HasSucceededOnce = true;
        OnFirstSuccess?.Invoke();
    }
```

既存の `if (successCount >= dailyLimit) { ... } else { ... }` 以降は一切変更しない。

- [ ] **Step 3: Unityバッチモードでコンパイル確認**

Expected: コンパイル成功、`error CS` 0件。既存の全テスト(63件)も引き続きPassed
(本タスクは新規テストを追加しない。既存のUnity依存が強いダイアログクラスの
統合的な変更であり、追加した1行の条件分岐自体はテストで固定するほどの
複雑さが無いため)。

---

### Task 2: `HubAIntroSequence` の実装

**Files:**
- Create: `Assets/VoidSurvival_10-Days/Scripts/Events/HubAIntroSequence.cs`

**Interfaces:**
- Consumes: `MessageText.Instance`, `ObservedValuesHud.Instance`,
  `CommunicationDialog`（シーン内を検索して取得）

- [ ] **Step 1: `HubAIntroSequence.cs` を実装**

```csharp
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
        communicationDialog = FindAnyObjectByType<CommunicationDialog>();
        if (communicationDialog != null)
        {
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
```

- [ ] **Step 2: Unityバッチモードでコンパイル確認**

Expected: コンパイル成功、`error CS` 0件。既存の全テスト(63件)も引き続きPassed
（本タスクは新規テストを追加しない。`TerminalIntroSequence`と同様、シーン実行時の
統合的な進行制御のため）。

---

## 完了後の報告事項（Codex → Claude Code）

- 変更/作成したファイル一覧
- 作業中に見つかった、本プラン記載以外の問題（あれば報告のみ）
- シーンへの配置（UI要素の作成、`HubAIntroSequence`の配置、`CommunicationDialog`が
  実際に`2_HubA_Demo`に存在するかの確認）は本プランの範囲外（Claude Codeが別途行う）
  である旨の確認
