# 天候判定の3軸化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **この計画はCodex CLI（`codex exec`）が実行する。** Claude Code（オーケストレーター）が
> タスクごとに本ファイルを渡し、Codexが実装、Claude Codeが差分レビューとコンパイル/テスト
> 実行結果の確認（Unityバッチモードは直接実行）を行う。

**Goal:** 実際にゲームプレイ（敵スポーン・ドロップ）に使われている天候判定ロジックを、
「合計点が0か満点に張り付き曇り・雨がほとんど出ない」問題のある現行方式から、
明暗/荒れ具合/計器信頼性の3軸独立判定に作り直す。

## 前提の訂正（重要）

当初は DemoSpec §5.3 が名指ししている `WeatherCalculator`（`radiation×2+gyro+mag` の
合計点方式）を直す想定だったが、調査の結果、**`WeatherCalculator`/`WeatherManager`/
`WeatherDataManager`（コード内コメントで「方法B」と呼ばれている）は `WeatherListenerExample.cs`
以外からどこからも参照されておらず、実際のゲームプレイには一切接続されていない**
（未使用の実験コード）。

実際に `EnemySpawner.cs` / `CharacterItemDropper.cs` が参照しているのは
**`WeatherTester.CurrentRuleBasedWeather`**（HIGH/LOW状態の個数で判定する別の簡易ロジック）
である。したがって、**本タスクは `WeatherTester.cs` の判定ロジックを3軸化対象とする。**
`WeatherCalculator.cs`/`WeatherManager.cs`/`WeatherDataManager.cs`/
`optionB_WeatherDataManager.cs` は本タスクでは変更しない（未使用コードの削除・整理は
別途判断が必要なため、今回のスコープ外とする）。

**Architecture:** `WeatherTester` は既に `DataManager`/`HKDataDisplay` から
LOW/NORMAL/HIGHの文字列状態を取得する構造になっている（生の数値ではなく分類済み文字列を
扱う既存の慣習）。この慣習に合わせ、3軸判定も文字列状態を入力とする `internal static`
純粋関数として実装し、`WeatherTester` 本体はその呼び出しだけにする。

- **軸1（明暗/LightPhase）**: 電流状態(`HKDataDisplay.CurrentState`)。`LOW`なら食(Eclipse)、
  それ以外は日照(Sun)
- **軸2（荒れ具合/既存のWeather enumを再利用）**: 放射線 + 磁場3つ(Mag1-3)の状態のうち
  `HIGH`の個数で `Sunny→Cloudy→Rainy→Thunderstorm` の4段階に決める
  （DemoSpecの「3軸独立」方針により、ジャイロ・電流はこの軸の判定材料から外す）
- **軸3（計器信頼性/InstrumentReliability）**: ジャイロ状態(`DataManager.GyroState`)。
  `HIGH`なら乱れ(Disturbed)、それ以外は安定(Stable)

**既存の公開API `CurrentRuleBasedWeather`（`Weather`型）はそのまま残す**
（`EnemySpawner`/`CharacterItemDropper` が参照しているため、型・プロパティ名を
変更すると既存のInspector設定済みデータ（天候ごとのスポーン数・ドロップ表）が
壊れる）。軸1・軸3は新規プロパティとして追加する（今回はまだ誰も消費しないが、
将来のタスク（ジャイロ→通信難易度 等）がすぐ使えるようにしておく）。

**Tech Stack:** Unity 2023.1.22f1, C#, Unity Test Framework

**Spec:** `docs/superpowers/specs/2026-11-17-voidsurvival-ai-collab-design.md`
（§5 タスク#4に対応）。背景: `docs/VoidSurvival_DemoSpec_v0.3.md` §5.3（git管理外）

## Global Constraints

- 作業ディレクトリは `vendor/VoidSurvival_10-Days/`
- upstream への push・merge は行わない
- **`WeatherCalculator.cs`/`WeatherManager.cs`/`WeatherDataManager.cs`/
  `optionB_WeatherDataManager.cs`/`WeatherListenerExample.cs` は変更しない**
  （未使用コードだが、削除や整理は本タスクのスコープ外）
- **`Weather` enum（`Sunny`/`Cloudy`/`Rainy`/`Thunderstorm`）の定義・値の並びは変更しない**
  （既存のInspector設定済みデータとの整合性のため）
- `EnemySpawner.cs`/`CharacterItemDropper.cs` の既存コードは変更しない
  （`WeatherTester.CurrentRuleBasedWeather` という同じ型・同じプロパティ名を
  引き続き提供することで、呼び出し側は無改修で動く）
- Unityバッチモードのコンパイル確認・テスト実行は **Claude Codeが直接実行する**。
  Codexは実装のみ行い、コミットもしない

## Review Focus

- **軸2の判定が「HIGHの個数」だけで決まり、LOW/NORMALの個数には依存しないこと**
  （DemoSpecの意図:「合計点が0か満点に張り付く」問題を避けるため、段階的に反応する設計）
- **`DataManager.Instance`/`HKDataDisplay.Instance` が null、またはデータ未ロードの場合に
  例外を出さないこと**
- **既存の `OnWeatherChanged` イベントの発火条件（軸2の値が変化した時のみ）が
  変わっていないこと**
- **既存のテスト（Task1〜3で追加した合計10件）が引き続きパスすること**

---

### Task 1: 3軸の判定を表す型を追加

**Files:**
- Modify: `Assets/VoidSurvival_10-Days/Scripts/PlayfabTest/PublicData/SensorEnums.cs`

- [ ] **Step 1: enumを追加**

既存の `Level`/`Weather` enumの下に追加（既存の2つは変更しない）:

```csharp
/// <summary>明暗の状態(軸1)。電流状態から判定する。</summary>
public enum LightPhase
{
    Sun,     // 日照
    Eclipse  // 食
}

/// <summary>計器信頼性の状態(軸3)。ジャイロ状態から判定する。</summary>
public enum InstrumentReliability
{
    Stable,    // 安定
    Disturbed  // 乱れ
}
```

- [ ] **Step 2: Commit は行わない**（Codexはコミットしない。全タスク完了後にClaude Codeが行う）

---

### Task 2: `WeatherTester` の3軸判定ロジック

**Files:**
- Modify: `Assets/VoidSurvival_10-Days/Scripts/PlayfabTest/PublicData/WeatherTester.cs`
- Test: `Assets/VoidSurvival_10-Days/Tests/EditMode/WeatherTesterThreeAxisTests.cs`

**Interfaces:**
- Produces:
  - `WeatherTester.EvaluateRoughWeather(string radiationState, string mag1State, string mag2State, string mag3State) -> Weather`
  - `WeatherTester.EvaluateLightPhase(string currentState) -> LightPhase`
  - `WeatherTester.EvaluateInstrumentReliability(string gyroState) -> InstrumentReliability`
  （いずれも `internal static`）
  - `WeatherTester.CurrentLightPhase { get; }`（新規プロパティ）
  - `WeatherTester.CurrentInstrumentReliability { get; }`（新規プロパティ）

- [ ] **Step 1: 失敗するテストを書く**

`Assets/VoidSurvival_10-Days/Tests/EditMode/WeatherTesterThreeAxisTests.cs`:

```csharp
using NUnit.Framework;

public class WeatherTesterThreeAxisTests
{
    // 軸2: 荒れ具合(Weather enum再利用) — HIGHの個数だけで決まる
    [Test]
    public void EvaluateRoughWeather_NoHigh_ReturnsSunny()
    {
        Assert.AreEqual(Weather.Sunny,
            WeatherTester.EvaluateRoughWeather("LOW", "NORMAL", "LOW", "NORMAL"));
    }

    [Test]
    public void EvaluateRoughWeather_OneHigh_ReturnsCloudy()
    {
        Assert.AreEqual(Weather.Cloudy,
            WeatherTester.EvaluateRoughWeather("HIGH", "NORMAL", "LOW", "NORMAL"));
    }

    [Test]
    public void EvaluateRoughWeather_TwoHigh_ReturnsRainy()
    {
        Assert.AreEqual(Weather.Rainy,
            WeatherTester.EvaluateRoughWeather("HIGH", "HIGH", "NORMAL", "NORMAL"));
    }

    [Test]
    public void EvaluateRoughWeather_ThreeHigh_ReturnsThunderstorm()
    {
        Assert.AreEqual(Weather.Thunderstorm,
            WeatherTester.EvaluateRoughWeather("HIGH", "HIGH", "HIGH", "NORMAL"));
    }

    [Test]
    public void EvaluateRoughWeather_AllFourHigh_ReturnsThunderstorm()
    {
        // 4つ全部HIGHでも配列の範囲を超えずThunderstormに留まることを確認
        Assert.AreEqual(Weather.Thunderstorm,
            WeatherTester.EvaluateRoughWeather("HIGH", "HIGH", "HIGH", "HIGH"));
    }

    // 軸1: 明暗
    [Test]
    public void EvaluateLightPhase_CurrentLow_ReturnsEclipse()
    {
        Assert.AreEqual(LightPhase.Eclipse, WeatherTester.EvaluateLightPhase("LOW"));
    }

    [Test]
    public void EvaluateLightPhase_CurrentNormalOrHigh_ReturnsSun()
    {
        Assert.AreEqual(LightPhase.Sun, WeatherTester.EvaluateLightPhase("NORMAL"));
        Assert.AreEqual(LightPhase.Sun, WeatherTester.EvaluateLightPhase("HIGH"));
    }

    // 軸3: 計器信頼性
    [Test]
    public void EvaluateInstrumentReliability_GyroHigh_ReturnsDisturbed()
    {
        Assert.AreEqual(InstrumentReliability.Disturbed,
            WeatherTester.EvaluateInstrumentReliability("HIGH"));
    }

    [Test]
    public void EvaluateInstrumentReliability_GyroNormalOrLow_ReturnsStable()
    {
        Assert.AreEqual(InstrumentReliability.Stable,
            WeatherTester.EvaluateInstrumentReliability("NORMAL"));
        Assert.AreEqual(InstrumentReliability.Stable,
            WeatherTester.EvaluateInstrumentReliability("LOW"));
    }
}
```

（`WeatherTester` は既に `public class` なので、`internal static` メンバーを
テストから呼ぶには Task 3「衛星データ→施設の接続」で追加済みの
`Assets/VoidSurvival_10-Days/Scripts/AssemblyInfo.cs` の
`InternalsVisibleTo("VoidSurvival.Tests.EditMode")` がそのまま使える。
新規作成は不要）

- [ ] **Step 2: Unityバッチモードでテストを実行し、失敗を確認**

（3つの `internal static` メソッドが存在しないためコンパイルエラーになることを確認）

- [ ] **Step 3: `WeatherTester.cs` に実装**

`WeatherTester` クラス内に追加:

```csharp
public LightPhase CurrentLightPhase { get; private set; } = LightPhase.Sun;
public InstrumentReliability CurrentInstrumentReliability { get; private set; } = InstrumentReliability.Stable;

// 軸2(荒れ具合): 放射線+磁場3つのうちHIGHの個数で段階的に決める。
// LOW/NORMALの個数には依存しない(個数に関わらずHIGHが0なら必ずSunny)。
internal static Weather EvaluateRoughWeather(string radiationState, string mag1State, string mag2State, string mag3State)
{
    int highCount = 0;
    foreach (var s in new[] { radiationState, mag1State, mag2State, mag3State })
    {
        if (s == "HIGH") highCount++;
    }
    var levels = new[] { Weather.Sunny, Weather.Cloudy, Weather.Rainy, Weather.Thunderstorm };
    return levels[Math.Min(highCount, levels.Length - 1)];
}

// 軸1(明暗): 電流が低いときは食(Eclipse)とみなす。
internal static LightPhase EvaluateLightPhase(string currentState)
{
    return currentState == "LOW" ? LightPhase.Eclipse : LightPhase.Sun;
}

// 軸3(計器信頼性): ジャイロが高いときは姿勢が乱れているとみなす。
internal static InstrumentReliability EvaluateInstrumentReliability(string gyroState)
{
    return gyroState == "HIGH" ? InstrumentReliability.Disturbed : InstrumentReliability.Stable;
}
```

既存の `LateUpdate()` と `EvaluateWeather(List<string> states)` を置き換える。
`selectedSources`（Inspectorで選ぶ3項目の仕組み）は3軸化により不要になるため、
`LateUpdate()` は次のように直接3軸を評価する形に変更する:

```csharp
private void LateUpdate()
{
    if (dataManager == null || hkDataDisplay == null) return;

    Weather weather = EvaluateRoughWeather(
        dataManager.RadiationState, dataManager.Mag1State, dataManager.Mag2State, dataManager.Mag3State);
    CurrentLightPhase = EvaluateLightPhase(hkDataDisplay.CurrentState);
    CurrentInstrumentReliability = EvaluateInstrumentReliability(dataManager.GyroState);

    if (weather != CurrentRuleBasedWeather)
    {
        CurrentRuleBasedWeather = weather;
        Debug.Log($"[WeatherTester] rad={dataManager.RadiationState} mag1={dataManager.Mag1State} " +
                   $"mag2={dataManager.Mag2State} mag3={dataManager.Mag3State} → Weather: {weather}");
        OnWeatherChanged?.Invoke(weather);
    }
}
```

**この変更に伴い削除してよいもの**: `DataSourceType` enum、`selectedSources`
フィールド、`CollectSelectedStates()`、`GetState(DataSourceType)`、旧
`EvaluateWeather(List<string> states)` メソッド
（3軸化により「Inspectorで3項目選ぶ」仕組みそのものが不要になるため）。
削除する場合は、他にこれらを参照している箇所が無いことを
`grep -rn "DataSourceType\|selectedSources" Assets/VoidSurvival_10-Days/Scripts`
で確認してから行うこと。参照が残っている場合は削除せず報告すること。

- [ ] **Step 4: Unityバッチモードでコンパイル確認・テスト実行**

Expected: コンパイル成功、`WeatherTesterThreeAxisTests` の9件がPassed。
既存の10件(Task1〜3)も引き続きPassedであること。

---

## 完了後の報告事項（Codex → Claude Code）

- 変更/作成したファイル一覧
- `DataSourceType`/`selectedSources`等を削除したか、削除しなかった場合はその理由
  （他箇所からの参照が残っていたか）
- 作業中に見つかった、本プラン記載以外の問題（あれば報告のみ）
