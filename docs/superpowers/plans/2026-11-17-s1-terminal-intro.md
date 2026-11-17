# S1 起動シーケンス(Terminal) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **この計画はCodex CLI（`codex exec`）が実行する。** Claude Code（オーケストレーター）が
> 本ファイルを渡し、Codexが実装、Claude Codeが差分レビューとコンパイル/テスト実行結果の
> 確認（Unityバッチモードは直接実行）を行う。

**Goal:** カット割り（`docs/VoidSurvival_カット割り_2.md`）S1「起動」のうち、
A〜Dの台詞進行・チェックリスト表示・プレイヤー解放・次シーンへの遷移を実装する。
本タスクは**スクリプトのみ**（Codexが実装）。実際のシーンへの配置
（`1_Terminal_Demo.unity`/`0_TitleDemo.unity`への組み込み）はClaude Codeが
別途バッチスクリプトで行う。

## スコープと簡略化（明記した上で進める）

カット割りの精緻なカット単位（A-1〜D-4、個別の秒数指定）をそのまま再現するのは、
既存の`MessageText`（クリックで進める会話システム）の仕組みと相性が悪い部分がある。
以下のように簡略化する:

- 個々の秒数指定(例: 「B-6は3秒の無音」)は再現しない。**クリックで進める**
  既存の`MessageText`のテンポに委ねる
- C-4「プレイヤーの初めての操作」は、独立した「ポートから外れる」専用の
  インタラクトは作らず、**C-1〜C-3の会話が終わった直後にプレイヤーを操作可能にする**
  という簡略化にする
- 英語の起動ログ(A-2)は、別途`DescentReadinessChecklist`と同じ仕組みを使った
  単純な「タイトルを1行ずつ短い間隔で表示して自動的に消える」処理にする
  （クリック不要、`MessageText`は使わない）

これらは**仮の実装方針であり、実際に動かした上で調整が必要**。

**Architecture:** 新規コンポーネント`TerminalIntroSequence`が、既存の
`MessageText.Instance.TextMessage(List<string>)`（クリックで進む会話表示、
既存の`ZONEN.cs`で使われているのと同じ仕組み）を使って台詞を順番に表示する
コルーチンを実行する。チェックリスト表示は新規`DescentReadinessChecklist`
コンポーネント（6行を0.5秒間隔で表示、完了行は緑・未完了行は赤）が担当する。
シーン遷移の判断ロジックも含めるが、実際の`SceneManager.LoadScene`呼び出しは
単純な文字列指定で良い(`SceneMover`のような接触判定は使わない。本シーケンスは
時間経過で自動的に遷移する)。

**Tech Stack:** Unity 2023.1.22f1, C#, TextMeshPro, Unity Test Framework

**Spec:** `docs/superpowers/specs/2026-11-17-voidsurvival-ai-collab-design.md`
（新規タスク）。`docs/VoidSurvival_カット割り_2.md` 第2部 S1節(A〜D)。

## Global Constraints

- 作業ディレクトリは `vendor/VoidSurvival_10-Days/`
- upstream への push・merge は行わない
- 既存の `MessageText`/`Dialog`/`MainUI` の挙動は変更しない（利用するだけ）
- シーンファイル(.unity)は変更しない（Claude Codeが別途行う）
- Unityバッチモードのコンパイル確認・テスト実行は **Claude Codeが直接実行する**。
  Codexは実装のみ行い、コミットもしない

## Review Focus

- **`MessageText.Instance`/`DescentReadinessChecklist` が null の場合に例外を
  出さないこと**（シーンへの配置忘れ時の安全策）
- **台詞の内容はカット割りの文言をそのまま使うこと**（意訳・省略をしない。
  長い場合は複数行に分けてよいが、意味を変えないこと）
- **チェックリストの状態(完了/中断/未着手の3種類)がカット割りD-2と一致していること**
  （降下カプセル点検=完了、切離機構確認=完了、姿勢制御較正=中断、
  電源系修復=中断、熱制御確認=未着手、人員覚醒=未着手。「中断」と「未着手」を
  混同しないこと。チェックマークは「完了」のときだけ`[✓]`、それ以外は`[ ]`）
- **既存のテスト(現時点で59件)が引き続きパスすること**

---

### Task 1: `DescentReadinessChecklist` の実装

**Files:**
- Create: `Assets/VoidSurvival_10-Days/Scripts/UI/DescentReadinessChecklist.cs`
- Test: `Assets/VoidSurvival_10-Days/Tests/EditMode/DescentReadinessChecklistTests.cs`

**Interfaces:**
- Produces:
  - `DescentReadinessChecklist.FormatLine(string label, string unitName, string status) -> string`
    （`status` は `"完了"`/`"中断"`/`"未着手"` のいずれか）
  - `DescentReadinessChecklist.FormatSummary(int completeCount, int totalCount) -> string`
  （いずれも `internal static`）
  - `DescentReadinessChecklist.ShowChecklist()` (コルーチンを開始して1行ずつ表示)
  - `DescentReadinessChecklist.HideChecklist()`

- [ ] **Step 1: 失敗するテストを書く**

```csharp
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
```

- [ ] **Step 2: Unityバッチモードでテストを実行し、失敗を確認**

- [ ] **Step 3: `DescentReadinessChecklist.cs` を実装**

```csharp
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

    public void ShowChecklist()
    {
        if (panel != null) panel.SetActive(true);
        IsShowing = true;
        StartCoroutine(RevealLines());
    }

    public void HideChecklist()
    {
        IsShowing = false;
        if (panel != null) panel.SetActive(false);
    }

    private IEnumerator RevealLines()
    {
        var shown = new List<string>();
        int completeCount = 0;
        foreach (var item in items)
        {
            shown.Add(FormatLine(item.label, item.unitName, item.status));
            if (item.status == "完了") completeCount++;
            if (linesText != null) linesText.text = string.Join("\n", shown);
            yield return new WaitForSeconds(lineInterval);
        }
        if (summaryText != null) summaryText.text = FormatSummary(completeCount, items.Count);
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
```

**注意:** ドット数は行ごとに異なる（カット割り原文の桁揃えをそのまま再現しているため）。
上記コード例の `switch` によるドット数の決め方はそのまま使ってよいし、他の方法
（各 `ChecklistItem` にドット文字列を直接持たせる等）に変えてもよい。
いずれにせよ3件のテストの期待値と文字列が完全一致すること。

- [ ] **Step 4: Unityバッチモードでコンパイル確認・テスト実行**

Expected: コンパイル成功、`DescentReadinessChecklistTests` の3件がPassed。

---

### Task 2: `TerminalIntroSequence` の実装

**Files:**
- Create: `Assets/VoidSurvival_10-Days/Scripts/Events/TerminalIntroSequence.cs`

**Interfaces:**
- Consumes: `MessageText.Instance`, `DescentReadinessChecklist`,
  `MainUI.Instance`

- [ ] **Step 1: `TerminalIntroSequence.cs` を実装**

```csharp
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
    [SerializeField] private GameObject player;
    [SerializeField] private DescentReadinessChecklist checklist;
    [SerializeField] private GameObject objectivePanel;
    [SerializeField] private TMP_Text objectiveText;
    [SerializeField] private string nextSceneName = "2_HubA_Demo";

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
            yield return new WaitUntil(() => !checklist.IsShowing || true); // 表示完了を待つ簡易実装
            yield return new WaitForSeconds(1.0f); // 全行表示後、少し見せる
        }

        yield return MessageText.Instance.TextMessage(ChecklistCommentLines);

        if (checklist != null) checklist.HideChecklist();
        if (objectivePanel != null) objectivePanel.SetActive(false);

        SceneManager.LoadScene(nextSceneName);
    }
}
```

（`yield return new WaitUntil(() => !checklist.IsShowing || true);` は常にtrueを返す
だけの不完全な実装例。計画の趣旨(チェックリストの6行表示が終わるまで待つ)に
合うよう、`DescentReadinessChecklist` 側に「表示完了イベント/フラグ」を追加するか、
単純に `lineInterval * items.Count` 分待つ実装に直してよい。計画からの逸脱ではなく
実装者の裁量とする）

- [ ] **Step 2: Unityバッチモードでコンパイル確認**

Expected: コンパイル成功、`error CS` 0件。既存の全テスト(59件)も引き続きPassed
（本タスクは新規テストを追加しない。`TerminalIntroSequence`はシーン実行時の
統合的な進行制御であり、個々の整形ロジックは既にTask1でテストしているため）。

---

## 完了後の報告事項（Codex → Claude Code）

- 変更/作成したファイル一覧
- `TerminalIntroSequence`のチェックリスト待機部分をどう実装したか
- 作業中に見つかった、本プラン記載以外の問題（あれば報告のみ）
- シーンへの配置は本プランの範囲外（Claude Codeが別途行う）である旨の確認
