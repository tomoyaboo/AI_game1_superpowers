# 共通HUD(観測値3行・ETA表示) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **この計画はCodex CLI（`codex exec`）が実行する。** Claude Code（オーケストレーター）が
> 本ファイルを渡し、Codexが実装、Claude Codeが差分レビューとコンパイル/テスト実行結果の
> 確認（Unityバッチモードは直接実行）を行う。

**Goal:** カット割り（`docs/VoidSurvival_カット割り_2.md`）のS3/S4で繰り返し使われる
「画面左上の観測値3行（RAD/MAG/ΔT）」と、S2末尾で初めて出る「REENTRY ETA」表示を、
**どのシーンでも使い回せる共通HUDコンポーネント**として実装する。本タスクは
**バックエンドのロジックとコンポーネントのみ**（Codexが実装）。実際のCanvas/Text配置と
`UIManager.prefab`への組み込みはUnity Editorでの作業のため、Claude Codeが別途
バッチスクリプトで行う。

## スコープ

**含む**:
- `DataManager` に生の放射線値(RAD)・磁場の平均値(MAG)を公開
- `TimeManager` に残り日数・時間(ETA)を計算するメソッドを追加
- `ObservedValuesHud` コンポーネント(表示用テキストのフォーマット・表示/非表示の
  切り替えAPI)

**含まない（別タスク）**:
- 実際のCanvas/TextMeshProUGUIの配置、`UIManager.prefab`への組み込み
  （Claude Codeが本タスク完了後に別途行う）
- 各シーン・各カットでの表示/非表示の実際の呼び出し（S1〜S5のストーリー実装タスクで
  それぞれ接続する）

**Architecture:** 既存の `ShowStatus.cs`（HP/スタミナ表示）と同じパターンに合わせる
（`[SerializeField] TMP_Text` フィールドを `Update()` で書き換えるだけの単純な
コンポーネント）。表示する値のフォーマット部分は `internal static` な純粋関数に
切り出し、テストする。

**Tech Stack:** Unity 2023.1.22f1, C#, TextMeshPro, Unity Test Framework

**Spec:** `docs/superpowers/specs/2026-11-17-voidsurvival-ai-collab-design.md`
（新規タスク。カット割りI-4, L-1, N-4, H-2, S-3 に対応）

## Global Constraints

- 作業ディレクトリは `vendor/VoidSurvival_10-Days/`
- upstream への push・merge は行わない
- 既存の `DataManager`/`TimeManager` の既存プロパティ・メソッドの挙動は変更しない。
  追加のみ
- シーンファイル(.unity)・プレハブ(.prefab)は変更しない（別途Claude Codeが行う）
- Unityバッチモードのコンパイル確認・テスト実行は **Claude Codeが直接実行する**。
  Codexは実装のみ行い、コミットもしない

## Review Focus

- **データ未ロード時に例外を出さず、既定値(0等)を返すこと**
- **ETAが負の値にならないこと**（`maxDays` を超えて時間が進んだ場合、日数0・時間0で
  止まること。マイナス表示は見栄えが悪いため）
- **既存のテスト(現時点で48件)が引き続きパスすること**

---

### Task 1: `DataManager` に生の観測値を公開

**Files:**
- Modify: `Assets/VoidSurvival_10-Days/Scripts/PlayfabTest/DataManager.cs`

**Interfaces:**
- Produces: `DataManager.CurrentRadiationSignalCount { get; }` (int),
  `DataManager.CurrentMagMagnitude { get; }` (float)

- [ ] **Step 1: プロパティを追加**

既存の `RadiationState` 等のプロパティの近くに追加:

```csharp
public int CurrentRadiationSignalCount { get; private set; } = 0;
public float CurrentMagMagnitude { get; private set; } = 0f;
```

- [ ] **Step 2: `Update()` 内で値を更新**

既存の `Update()` 内、`UpdateStates(...)` の呼び出しの直前に追加:

```csharp
if (cRad != null) CurrentRadiationSignalCount = cRad.SignalCount;
CurrentMagMagnitude = SensorClassifier.AverageMagMagnitude(cMag1, cMag2, cMag3);
```

（`SensorClassifier.AverageMagMagnitude` は既存の
`Assets/VoidSurvival_10-Days/Scripts/PlayfabTest/PublicData/SensorClassifier.cs` に
既にある公開メソッド。無効なセンサがあればNaNを返すので、表示側でNaN対策が必要
— Task3で対応する）

- [ ] **Step 3: Unityバッチモードでコンパイル確認**

Expected: コンパイル成功、`error CS` 0件。

---

### Task 2: `TimeManager` にETA計算を追加

**Files:**
- Modify: `Assets/VoidSurvival_10-Days/Scripts/TimeManager.cs`
- Test: `Assets/VoidSurvival_10-Days/Tests/EditMode/TimeManagerEtaTests.cs`

**Interfaces:**
- Produces: `TimeManager.CalculateReentryEta(float elapsedTime, int maxDays, float minutesPerDay) -> (int days, int hours)`
  （`internal static`。ゲーム内の「1日」は `minutesPerDay` 分かけて進む実時間ではなく、
  `_elapsedTime` はゲーム内分換算の値であることに注意。既存の `GetHour()`/`GetDay()`等と
  同じ「1日=1440ゲーム内分」の前提で計算する）

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using NUnit.Framework;

public class TimeManagerEtaTests
{
    [Test]
    public void CalculateReentryEta_AtStart_ReturnsMaxDaysZeroHours()
    {
        var (days, hours) = TimeManager.CalculateReentryEta(elapsedTime: 0f, maxDays: 10, minutesPerDay: 30f);
        Assert.AreEqual(10, days);
        Assert.AreEqual(0, hours);
    }

    [Test]
    public void CalculateReentryEta_HalfwayThroughDay_ReturnsPartialHours()
    {
        // 1440ゲーム内分のうち720分経過(半日) -> 残り9日12時間
        var (days, hours) = TimeManager.CalculateReentryEta(elapsedTime: 720f, maxDays: 10, minutesPerDay: 30f);
        Assert.AreEqual(9, days);
        Assert.AreEqual(12, hours);
    }

    [Test]
    public void CalculateReentryEta_PastMaxDays_NeverGoesNegative()
    {
        var (days, hours) = TimeManager.CalculateReentryEta(elapsedTime: 999999f, maxDays: 10, minutesPerDay: 30f);
        Assert.AreEqual(0, days);
        Assert.AreEqual(0, hours);
    }

    [Test]
    public void CalculateReentryEta_ExactlyOneDayElapsed_ReturnsOneDayLess()
    {
        var (days, hours) = TimeManager.CalculateReentryEta(elapsedTime: 1440f, maxDays: 10, minutesPerDay: 30f);
        Assert.AreEqual(9, days);
        Assert.AreEqual(0, hours);
    }
}
```

- [ ] **Step 2: Unityバッチモードでテストを実行し、失敗を確認**

- [ ] **Step 3: `TimeManager.cs` に実装**

```csharp
internal static (int days, int hours) CalculateReentryEta(float elapsedTime, int maxDays, float minutesPerDay)
{
    float totalMinutes = maxDays * 1440f;
    float remainingMinutes = Mathf.Max(0f, totalMinutes - elapsedTime);
    int days = Mathf.FloorToInt(remainingMinutes / 1440f);
    int hours = Mathf.FloorToInt((remainingMinutes % 1440f) / 60f);
    return (days, hours);
}

public (int days, int hours) GetReentryEta()
{
    return CalculateReentryEta(_elapsedTime, maxDays, minutesPerDay);
}
```

（`minutesPerDay` パラメータは実時間換算に使っているフィールドで、計算自体には
使わないが、関数のテスト容易性のために引数として受け取る形にする。既存の
`GetDay()`/`GetHour()`等のロジックは変更しない）

- [ ] **Step 4: Unityバッチモードでコンパイル確認・テスト実行**

Expected: コンパイル成功、`TimeManagerEtaTests` の4件がPassed。

---

### Task 3: `ObservedValuesHud` コンポーネントの実装

**Files:**
- Create: `Assets/VoidSurvival_10-Days/Scripts/UI/ObservedValuesHud.cs`
- Test: `Assets/VoidSurvival_10-Days/Tests/EditMode/ObservedValuesHudTests.cs`

**Interfaces:**
- Produces:
  - `ObservedValuesHud.FormatObservedValues(int rad, float mag, float deltaT) -> string`
  - `ObservedValuesHud.FormatEta(int days, int hours) -> string`
  （いずれも `internal static`）
  - `ObservedValuesHud.SetObservedValuesVisible(bool visible)`,
    `ObservedValuesHud.SetEtaVisible(bool visible)`

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using NUnit.Framework;

public class ObservedValuesHudTests
{
    [Test]
    public void FormatObservedValues_NormalValues_ReturnsExpectedFormat()
    {
        string result = ObservedValuesHud.FormatObservedValues(32, 47.3f, 11.2f);
        Assert.AreEqual("RAD 32 / MAG 47 / ΔT 11", result);
    }

    [Test]
    public void FormatObservedValues_NaNMag_ShowsDashInsteadOfCrashing()
    {
        string result = ObservedValuesHud.FormatObservedValues(32, float.NaN, 11.2f);
        Assert.AreEqual("RAD 32 / MAG -- / ΔT 11", result);
    }

    [Test]
    public void FormatEta_TenDaysZeroHours_ReturnsExpectedFormat()
    {
        string result = ObservedValuesHud.FormatEta(10, 0);
        Assert.AreEqual("REENTRY ETA .... 10D 00H", result);
    }

    [Test]
    public void FormatEta_SingleDigitHours_PadsWithZero()
    {
        string result = ObservedValuesHud.FormatEta(9, 5);
        Assert.AreEqual("REENTRY ETA .... 9D 05H", result);
    }
}
```

- [ ] **Step 2: Unityバッチモードでテストを実行し、失敗を確認**

- [ ] **Step 3: `ObservedValuesHud.cs` を実装**

既存の `ShowStatus.cs` と同じ構成パターンに合わせる:

```csharp
using TMPro;
using UnityEngine;

/// <summary>
/// カット割りS3/S4で使う観測値3行(RAD/MAG/ΔT)と、S2末尾で初めて出るETA表示を
/// まとめた共通HUD。UIManager.prefab配下に配置され、全シーンで使い回す想定。
/// 表示/非表示はストーリー進行側(各シーンの実装)から呼び出して制御する。
/// </summary>
public class ObservedValuesHud : MonoBehaviour
{
    public static ObservedValuesHud Instance { get; private set; }

    [SerializeField] private GameObject observedValuesPanel;
    [SerializeField] private TMP_Text observedValuesText;
    [SerializeField] private GameObject etaPanel;
    [SerializeField] private TMP_Text etaText;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // 初期状態は両方非表示(ストーリー進行で個別に見せる)
        if (observedValuesPanel != null) observedValuesPanel.SetActive(false);
        if (etaPanel != null) etaPanel.SetActive(false);
    }

    private void Update()
    {
        if (DataManager.Instance == null || HKDataDisplay.Instance == null || TimeManager.Instance == null) return;

        if (observedValuesText != null)
        {
            observedValuesText.text = FormatObservedValues(
                DataManager.Instance.CurrentRadiationSignalCount,
                DataManager.Instance.CurrentMagMagnitude,
                HKDataDisplay.Instance.CurrentThermalDelta);
        }

        if (etaText != null)
        {
            var (days, hours) = TimeManager.Instance.GetReentryEta();
            etaText.text = FormatEta(days, hours);
        }
    }

    public void SetObservedValuesVisible(bool visible)
    {
        if (observedValuesPanel != null) observedValuesPanel.SetActive(visible);
    }

    public void SetEtaVisible(bool visible)
    {
        if (etaPanel != null) etaPanel.SetActive(visible);
    }

    internal static string FormatObservedValues(int rad, float mag, float deltaT)
    {
        string magText = float.IsNaN(mag) ? "--" : Mathf.RoundToInt(mag).ToString();
        return $"RAD {rad} / MAG {magText} / ΔT {Mathf.RoundToInt(deltaT)}";
    }

    internal static string FormatEta(int days, int hours)
    {
        return $"REENTRY ETA .... {days}D {hours:00}H";
    }
}
```

- [ ] **Step 4: Unityバッチモードでコンパイル確認・テスト実行**

Expected: コンパイル成功、`ObservedValuesHudTests` の4件がPassed。
既存の全テスト(48件)も引き続きPassedであること(合計56件)。

---

## 完了後の報告事項（Codex → Claude Code）

- 変更/作成したファイル一覧
- 作業中に見つかった、本プラン記載以外の問題（あれば報告のみ）
- 実際のCanvas/Text配置・`UIManager.prefab`への組み込みは本プランの範囲外（Claude
  Codeが別途行う）である旨の確認
