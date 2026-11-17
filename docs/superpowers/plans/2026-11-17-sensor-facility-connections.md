# 衛星データ→施設の接続 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **この計画はCodex CLI（`codex exec`）が実行する。** Claude Code（オーケストレーター）が
> タスクごとに本ファイルを渡し、Codexが実装、Claude Codeが差分レビューとコンパイル/テスト
> 実行結果の確認（Unityバッチモードは直接実行）を行う。

**Goal:** 衛星データ（電圧・電流の LOW/NORMAL/HIGH 状態）を、既存の施設・ドロップ機構に
実際に反映させる。DemoSpec §5.6/§11.3で指摘されている「データ側は独立して動いており、
実際にゲームに効いているのは天候分岐の2箇所だけ」という穴のうち、電圧→作業台の加工速度、
電流→敵のドロップ率の2点を解消する。

**Architecture:** 既存の `PowerSupply`（施設）が `DynamicBuff`（評価時に毎回呼ばれる
`Func<float>`）経由で `OthersBuffType.WorkbenchEfficiency` を供給し、
`WorkbenchManager` がそれを使って加工時間を計算している（既存の配線）。この
`DynamicBuff` の評価式に電圧状態を追加するだけで済む。電流→ドロップ率は、
そのような既存の配線が無いため、`CharacterItemDropper` 内で直接
`HKDataDisplay.Instance.CurrentState` を読んで倍率をかける。
どちらも文字列↔数値の対応表部分を `internal static` な純粋関数として切り出し、
EditModeテストで固定する。

**Tech Stack:** Unity 2023.1.22f1, C#, Unity Test Framework（Task 2で導入済み）

**Spec:** `docs/superpowers/specs/2026-11-17-voidsurvival-ai-collab-design.md`
（§5 タスク#3に対応）。前提資料: `docs/VoidSurvival_DemoSpec_v0.3.md`
§5.6（git管理外、ローカル参照のみ）, §11.3

## Global Constraints

- 作業ディレクトリは `vendor/VoidSurvival_10-Days/`
- upstream（`NakaAwjr/VoidSurvival_10-Days`）への push・merge は行わない
- **本タスクのスコープは「電圧→作業台の加工速度」「電流→敵のドロップ率」の2点のみ。**
  天候→エネルギー素材出現、ジャイロ→通信難易度、温度差→帯電判定、磁場+放射線→SEU、
  予報グラフは別タスクで扱う（本プランでは変更しない）
- `HKDataDisplay.Instance` は非同期(PlayFab)でロードされるため、**参照する箇所は
  必ず null チェックすること**（既存コードの `if (TimeManager.Instance == null) return;`
  と同じ慣習に合わせる）
- 具体的な係数（%）は本プランで決めた値をそのまま採用してよい（ゲームバランスの
  最終調整は別途行う前提の仮値であることをコード上のコメントに明記する）
- Unityバッチモードのコンパイル確認・テスト実行は、**Claude Codeが直接実行する**
  （CodexのサンドボックスではUnityライセンスのIPC接続に失敗することが判明しているため）。
  Codexは実装のみ行い、コミットもしない

## Review Focus

- **`HKDataDisplay.Instance` が null の場合にクラッシュしないこと**:
  データ未ロード中（ゲーム開始直後）でも例外を出さないこと
- **既存のLevel依存の計算を壊していないこと**: `PowerSupply` のWorkbenchEfficiency計算は
  `levelEffect + voltageEffect` のように既存項を残したまま加算する形にすること
  （既存項を置き換えない）
- **ドロップ率の倍率が0〜1の範囲を超えないこと**: `Mathf.Clamp01` 等でガードすること
- **既存のテスト（Task1, Task2で追加した2件）が引き続きパスすること**

---

### Task 1: 電圧→作業台の加工速度

**Files:**
- Modify: `Assets/VoidSurvival_10-Days/Scripts/Facilities/PowerSpupply.cs`
- Test: `Assets/VoidSurvival_10-Days/Tests/EditMode/PowerSupplyVoltageTests.cs`

**Interfaces:**
- Produces: `PowerSupply.VoltageEfficiencyDelta(string voltageState) -> float`
  （`internal static`。テストから直接呼べるようにする）

- [ ] **Step 1: 失敗するテストを書く**

`Assets/VoidSurvival_10-Days/Tests/EditMode/PowerSupplyVoltageTests.cs`:

```csharp
using NUnit.Framework;

public class PowerSupplyVoltageTests
{
    [Test]
    public void VoltageEfficiencyDelta_Low_ReturnsPositiveDelta()
    {
        Assert.AreEqual(0.20f, PowerSupply.VoltageEfficiencyDelta("LOW"), 0.001f);
    }

    [Test]
    public void VoltageEfficiencyDelta_High_ReturnsNegativeDelta()
    {
        Assert.AreEqual(-0.10f, PowerSupply.VoltageEfficiencyDelta("HIGH"), 0.001f);
    }

    [Test]
    public void VoltageEfficiencyDelta_Normal_ReturnsZero()
    {
        Assert.AreEqual(0f, PowerSupply.VoltageEfficiencyDelta("NORMAL"), 0.001f);
    }

    [Test]
    public void VoltageEfficiencyDelta_UnknownState_ReturnsZero()
    {
        Assert.AreEqual(0f, PowerSupply.VoltageEfficiencyDelta("UNKNOWN"), 0.001f);
    }
}
```

（`PowerSupply` クラスは現在 `class PowerSupply : FacilityBase`（`public`修飾子なし=internal）
のまま。テストが同じ `VoidSurvival.Runtime` アセンブリに居るわけではない
（`VoidSurvival.Tests.EditMode` は別アセンブリ）ため、`VoltageEfficiencyDelta` と
`PowerSupply` クラス自体を `internal` のままテストから呼べるようにするには、
`Assets/VoidSurvival_10-Days/Scripts/VoidSurvival.Runtime.asmdef` と同じ階層にある
`AssemblyInfo.cs` 等に
`[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("VoidSurvival.Tests.EditMode")]`
を追加する必要がある。既存に `AssemblyInfo.cs` がなければ
`Assets/VoidSurvival_10-Days/Scripts/AssemblyInfo.cs` として新規作成すること）

- [ ] **Step 2: Unityバッチモードでテストを実行し、失敗を確認**

（この時点では `VoltageEfficiencyDelta` が存在しないためコンパイルエラーになるはずで、
これが「失敗」の確認になる。コンパイル確認コマンドは直前のプラン
`docs/superpowers/plans/2026-11-17-unity-test-framework-setup.md` の Global Constraints
参照）

- [ ] **Step 3: `PowerSpupply.cs` に実装**

`PowerSupply` クラスに `internal static` メソッドを追加:

```csharp
// 電圧状態による作業台効率への補正(仮値。バランス調整は別途)
// LOW: 電圧不足で作業台が遅くなる / HIGH: 電圧十分で若干速くなる
internal static float VoltageEfficiencyDelta(string voltageState)
{
    switch (voltageState)
    {
        case "LOW": return 0.20f;
        case "HIGH": return -0.10f;
        default: return 0f; // NORMAL or unknown
    }
}
```

既存の `WorkbenchEfficiency` の `DynamicBuff` の評価式を、既存の `levelEffect` に
`voltageEffect` を加算する形に変更する:

```csharp
new DynamicBuff(() =>
{
    if (IsBroken) return 0f;
    float levelEffect = -0.15f * Level;
    float voltageEffect = HKDataDisplay.Instance != null
        ? VoltageEfficiencyDelta(HKDataDisplay.Instance.VoltageState)
        : 0f;
    return levelEffect + voltageEffect;
})
{
    BuffType = OthersBuffType.WorkbenchEfficiency,
    OperationType = BuffOperationType.Multiply,
    Description = "WorkbenchEfficiency affected by Level and Voltage"
},
```

（`Description` の文言は既存の `+ (15 * Level) + "%"` のような動的文字列から変更して良い。
他の3つのDynamicBuff(スタミナ関連・施設ダメージ)は変更しないこと）

- [ ] **Step 4: Unityバッチモードでコンパイル確認・テスト実行**

Expected: コンパイル成功、`PowerSupplyVoltageTests` の4件が Passed。
既存の `ItemManagerTests`/`FlagDataTests` も引き続き Passed であること。

---

### Task 2: 電流→敵のドロップ率

**Files:**
- Modify: `Assets/VoidSurvival_10-Days/Scripts/Character/CharacterItemDropper.cs`
- Test: `Assets/VoidSurvival_10-Days/Tests/EditMode/CharacterItemDropperCurrentTests.cs`

**Interfaces:**
- Produces: `CharacterItemDropper.CurrentDropRateMultiplier(string currentState) -> float`
  （`internal static`）

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using NUnit.Framework;

public class CharacterItemDropperCurrentTests
{
    [Test]
    public void CurrentDropRateMultiplier_High_ReturnsBoostedMultiplier()
    {
        Assert.AreEqual(1.2f, CharacterItemDropper.CurrentDropRateMultiplier("HIGH"), 0.001f);
    }

    [Test]
    public void CurrentDropRateMultiplier_Low_ReturnsReducedMultiplier()
    {
        Assert.AreEqual(0.8f, CharacterItemDropper.CurrentDropRateMultiplier("LOW"), 0.001f);
    }

    [Test]
    public void CurrentDropRateMultiplier_Normal_ReturnsOne()
    {
        Assert.AreEqual(1.0f, CharacterItemDropper.CurrentDropRateMultiplier("NORMAL"), 0.001f);
    }

    [Test]
    public void EffectiveDropRate_NeverExceedsOne()
    {
        Assert.LessOrEqual(
            CharacterItemDropper.EffectiveDropRate(0.9f, "HIGH"), 1.0f);
    }
}
```

- [ ] **Step 2: Unityバッチモードでテストを実行し、失敗を確認**

（`CurrentDropRateMultiplier`/`EffectiveDropRate` が存在しないためコンパイルエラーになる
ことを確認）

- [ ] **Step 3: `CharacterItemDropper.cs` に実装**

```csharp
// 電流状態によるドロップ率への倍率補正(仮値。バランス調整は別途)
internal static float CurrentDropRateMultiplier(string currentState)
{
    switch (currentState)
    {
        case "HIGH": return 1.2f;
        case "LOW": return 0.8f;
        default: return 1.0f; // NORMAL or unknown
    }
}

internal static float EffectiveDropRate(float baseDropRate, string currentState)
{
    return Mathf.Clamp01(baseDropRate * CurrentDropRateMultiplier(currentState));
}
```

`DropIfNeeded()` 内の既存行:
```csharp
if (Random.Range(0, 1f) >= drop.dropRate) continue;
```
を次のように変更する:
```csharp
string currentState = HKDataDisplay.Instance != null ? HKDataDisplay.Instance.CurrentState : "NORMAL";
if (Random.Range(0, 1f) >= EffectiveDropRate(drop.dropRate, currentState)) continue;
```

（`currentState` の取得はループの外、`DropIfNeeded()` の先頭付近で1回だけ行うこと。
ループ内で毎回 `HKDataDisplay.Instance` を参照する必要はない）

- [ ] **Step 4: Unityバッチモードでコンパイル確認・テスト実行**

Expected: コンパイル成功、`CharacterItemDropperCurrentTests` の4件が Passed。
既存の全テスト(Task1, Task2, 本タスクのTask1)も引き続き Passed であること。

---

### Task 3: Commit

全タスクのコンパイル・テストが成功した後、Claude Codeがまとめてコミットする
（Codexはコミットしない。このステップはCodexの作業対象外）。

## 完了後の報告事項（Codex → Claude Code）

- 変更/作成したファイル一覧
- `AssemblyInfo.cs` を新規作成したか、既存ファイルに追記したか
- 計画に明記した係数(0.20, -0.10, 1.2, 0.8)以外の値を使った場合、その理由
- 作業中に見つかった、本プラン記載以外の問題（あれば報告のみ）
