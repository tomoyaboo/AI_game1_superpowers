# ESD(衛星帯電・放電)実装 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **この計画はCodex CLI（`codex exec`）が実行する。** Claude Code（オーケストレーター）が
> 本ファイルを渡し、Codexが実装、Claude Codeが差分レビューとコンパイル/テスト実行結果の
> 確認（Unityバッチモードは直接実行）を行う。

**Goal:** 面間の温度差(ΔT)が大きく、かつ磁場が弱いときにESD（帯電・放電）状態を発生させ、
電源施設にダメージを与える（→Task3で接続した加工速度バフが`IsBroken`で0になる既存経路を
通じて作業台が遅くなる）。帯電時はエネルギー素材のドロップ率が上がる。

## スコープ

**含む**:
- `HKDataDisplay` にΔT（全温度センサの最大-最小）を新規公開
- ESD発生条件の判定（ΔT閾値 + 磁場弱い）
- ESD発生中の電源施設(PowerSupply)への継続ダメージ
- ESD発生中、エネルギー素材アイテムのドロップ率上昇

**含まない（別タスク）**:
- 「ドローンが帯電状態になり、触れるとダメージ」という接触ダメージ・見た目の変更
  — 新規の当たり判定・ビジュアルが必要なため別タスク
- 青白いスパーク等の視覚演出
- 「屋内退避で対策」— ZONE-Nのエリア分割タスク(#8)で対応

**Architecture:** `HKDataDisplay` は現在、表示用に「変動が大きい上位N件」の温度センサしか
保持していない。ESDの判定には**全ての**温度センサが必要なため、別途「全温度センサ列」を
保持し、現在行の最大-最小からΔTを計算する処理を追加する。新規コンポーネント
`EsdEffectController`（`SeuEffectController`と同じ構成）がΔTと磁場状態を定期的に確認し、
`IsEsdActive` を公開する。`CharacterItemDropper` はこれを読んでエネルギー素材の
ドロップ率を上げる（Task3で追加した `EffectiveDropRate` の呼び出し結果にさらに
倍率をかける形。既存の2引数シグネチャ・既存テストは変更しない）。

**Tech Stack:** Unity 2023.1.22f1, C#, Unity Test Framework

**Spec:** `docs/superpowers/specs/2026-11-17-voidsurvival-ai-collab-design.md`
（§5 タスク#6に対応）。背景: `docs/VoidSurvival_DemoSpec_v0.3.md` §6.2, §6.4, §9.3
（git管理外）

## Global Constraints

- 作業ディレクトリは `vendor/VoidSurvival_10-Days/`
- upstream への push・merge は行わない
- **ΔTの閾値は20℃（DemoSpec §6.2「閾値は20℃前後から調整」の値をそのまま仮値とする）**
- **`CharacterItemDropper.EffectiveDropRate(float, string)` の既存シグネチャ・既存の
  呼び出し箇所・既存テスト(Task3で追加した4件)は変更しない。** ESDによる補正は
  その結果に対してさらに掛け算する、別の新規メソッドとして追加すること
- 接触ダメージ・見た目の変更には手を出さない
- Unityバッチモードのコンパイル確認・テスト実行は **Claude Codeが直接実行する**。
  Codexは実装のみ行い、コミットもしない

## Review Focus

- **`HKDataDisplay` のΔT計算が、既存の表示用`_tempCols`(上位N件)ではなく、
  全ての温度センサ列を対象にしていること**（5面全部を見ないと「面間の最大差」に
  ならないため）
- **データ未ロード時・該当センサ列が見つからない場合にΔTが異常値(NaN等)にならないこと**
  （データが無ければ0を返す等、安全側に倒すこと）
- **電源施設への継続ダメージが、施設が既に破壊されている(`IsBroken`)場合に
  さらに負のダメージを与えようとして例外や意味のない処理にならないこと**
- **ドロップ率補正が既存の `CharacterItemDropperCurrentTests`（4件）を壊していないこと**
- **既存のテスト(Task1〜6aで追加した28件)が引き続きパスすること**

---

### Task 1: `HKDataDisplay` にΔT(面間温度差)を公開

**Files:**
- Modify: `Assets/VoidSurvival_10-Days/Scripts/PlayfabTest/HKData/HKDataDisplay.cs`
- Test: `Assets/VoidSurvival_10-Days/Tests/EditMode/HKDataDisplayThermalDeltaTests.cs`

**Interfaces:**
- Produces:
  - `HKDataDisplay.CurrentThermalDelta { get; }`（新規プロパティ、float）
  - `HKDataDisplay.ComputeThermalDelta(Newtonsoft.Json.Linq.JObject row, System.Collections.Generic.List<string> tempCols) -> float`
    （`internal static`）

- [ ] **Step 1: 失敗するテストを書く**

`Assets/VoidSurvival_10-Days/Tests/EditMode/HKDataDisplayThermalDeltaTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json.Linq;

public class HKDataDisplayThermalDeltaTests
{
    [Test]
    public void ComputeThermalDelta_MultipleColumns_ReturnsMaxMinusMin()
    {
        var row = new JObject
        {
            ["Panel1 (°C)"] = 10.0,
            ["Panel2 (°C)"] = 30.0,
            ["Panel3 (°C)"] = 15.0,
        };
        var cols = new List<string> { "Panel1 (°C)", "Panel2 (°C)", "Panel3 (°C)" };

        Assert.AreEqual(20f, HKDataDisplay.ComputeThermalDelta(row, cols), 0.001f);
    }

    [Test]
    public void ComputeThermalDelta_NoValidColumns_ReturnsZero()
    {
        var row = new JObject();
        var cols = new List<string> { "Missing (°C)" };

        Assert.AreEqual(0f, HKDataDisplay.ComputeThermalDelta(row, cols), 0.001f);
    }

    [Test]
    public void ComputeThermalDelta_EmptyColumnList_ReturnsZero()
    {
        var row = new JObject { ["Panel1 (°C)"] = 10.0 };
        Assert.AreEqual(0f, HKDataDisplay.ComputeThermalDelta(row, new List<string>()), 0.001f);
    }
}
```

- [ ] **Step 2: Unityバッチモードでテストを実行し、失敗を確認**

- [ ] **Step 3: `HKDataDisplay.cs` に実装**

既存の `_tempCols`（表示用、上位N件）はそのまま残し、別に全列を保持するフィールドを
追加する:

```csharp
private List<string> _allTempCols = new List<string>();
public float CurrentThermalDelta { get; private set; } = 0f;
```

`ParseHKData` 内、既存の `_tempCols = PickMostVariable(...)` の近くに追加:

```csharp
_allTempCols = cols.Where(c => c.Contains("°C") || c.Contains("℃")).ToList();
```

`ShowRow(int i)` 内、既存の `var row = (JObject)_rows[i];` の直後に追加:

```csharp
CurrentThermalDelta = ComputeThermalDelta(row, _allTempCols);
```

純粋関数を追加:

```csharp
internal static float ComputeThermalDelta(JObject row, List<string> tempCols)
{
    double min = double.MaxValue, max = double.MinValue;
    bool any = false;
    foreach (var c in tempCols)
    {
        var tok = row[c];
        if (tok == null || tok.Type == JTokenType.Null) continue;
        if (double.TryParse(tok.ToString(), out double v))
        {
            if (v < min) min = v;
            if (v > max) max = v;
            any = true;
        }
    }
    return any ? (float)(max - min) : 0f;
}
```

- [ ] **Step 4: Unityバッチモードでコンパイル確認・テスト実行**

Expected: コンパイル成功、`HKDataDisplayThermalDeltaTests` の3件がPassed。
既存の28件(Task1〜6a)も引き続きPassedであること。

---

### Task 2: `Item` にエネルギー素材判定フィールドを追加

**Files:**
- Modify: `Assets/VoidSurvival_10-Days/Scripts/Data/Item.cs`

- [ ] **Step 1: フィールドを追加**

`Item` 抽象クラスに、Task6aで追加した `isShield` の直後に追加:

```csharp
[Tooltip("ESD発生中にドロップ率が上がる(帯電で得られる)エネルギー素材かどうか")]
public bool isEnergyMaterial = false;
```

- [ ] **Step 2: Commitは行わない**

---

### Task 3: `EsdEffectController` の実装

**Files:**
- Create: `Assets/VoidSurvival_10-Days/Scripts/EsdEffectController.cs`
- Test: `Assets/VoidSurvival_10-Days/Tests/EditMode/EsdEffectControllerTests.cs`

**Interfaces:**
- Produces:
  - `EsdEffectController.Instance.IsEsdActive { get; }`（bool）
  - `EsdEffectController.IsEsdCondition(float thermalDelta, float threshold, string mag1State, string mag2State, string mag3State) -> bool`
    （`internal static`）
- Consumes: `HKDataDisplay.Instance.CurrentThermalDelta`,
  `DataManager.Instance.{Mag1State,Mag2State,Mag3State}`,
  `FacilityManager.Instance.GetFacility(FacilityManager.FacilityType.PowerSupply)`

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using NUnit.Framework;

public class EsdEffectControllerTests
{
    [Test]
    public void IsEsdCondition_DeltaAboveThresholdAndMagWeak_ReturnsTrue()
    {
        Assert.IsTrue(EsdEffectController.IsEsdCondition(25f, 20f, "LOW", "NORMAL", "NORMAL"));
    }

    [Test]
    public void IsEsdCondition_DeltaBelowThreshold_ReturnsFalse()
    {
        Assert.IsFalse(EsdEffectController.IsEsdCondition(15f, 20f, "LOW", "NORMAL", "NORMAL"));
    }

    [Test]
    public void IsEsdCondition_MagNotWeak_ReturnsFalse()
    {
        Assert.IsFalse(EsdEffectController.IsEsdCondition(25f, 20f, "NORMAL", "NORMAL", "NORMAL"));
    }

    [Test]
    public void IsEsdCondition_DeltaExactlyAtThreshold_ReturnsFalse()
    {
        // 閾値そのものは「大きい」に含めない(厳密に超えた場合のみ)
        Assert.IsFalse(EsdEffectController.IsEsdCondition(20f, 20f, "LOW", "NORMAL", "NORMAL"));
    }
}
```

- [ ] **Step 2: Unityバッチモードでテストを実行し、失敗を確認**

- [ ] **Step 3: `EsdEffectController.cs` を実装**

```csharp
using UnityEngine;

/// <summary>
/// ESD(衛星帯電・放電)の発生を管理するシングルトン。
/// 面間の温度差(ΔT)が大きく、かつ磁場が弱いときに発生中とみなし、
/// 電源施設に継続ダメージを与える。
///
/// 既知の簡略化: 「触れるとダメージ」等の接触判定・見た目の変更は本タスクでは実装しない。
/// </summary>
public class EsdEffectController : MonoBehaviour
{
    public static EsdEffectController Instance { get; private set; }

    public bool IsEsdActive { get; private set; }

    [Header("ESD調整パラメータ(仮値。バランス調整は別途)")]
    [SerializeField] private float checkInterval = 1f;
    [SerializeField] private float thermalDeltaThreshold = 20f;
    [SerializeField] private float facilityDamagePerCheck = 2f;

    private float _timer;
    private FacilityBase _powerSupply;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (FacilityManager.Instance != null)
        {
            _powerSupply = FacilityManager.Instance.GetFacility(FacilityManager.FacilityType.PowerSupply);
        }
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < checkInterval) return;
        _timer = 0f;
        Evaluate();
    }

    private void Evaluate()
    {
        if (HKDataDisplay.Instance == null || DataManager.Instance == null)
        {
            IsEsdActive = false;
            return;
        }

        IsEsdActive = IsEsdCondition(
            HKDataDisplay.Instance.CurrentThermalDelta, thermalDeltaThreshold,
            DataManager.Instance.Mag1State, DataManager.Instance.Mag2State, DataManager.Instance.Mag3State);

        if (IsEsdActive && _powerSupply != null && !_powerSupply.IsBroken)
        {
            _powerSupply.DamageFacility(facilityDamagePerCheck);
        }
    }

    /// <summary>ΔTが閾値を超え、かつ磁場のいずれか1つでも弱いならESD発生条件とみなす。</summary>
    internal static bool IsEsdCondition(float thermalDelta, float threshold, string mag1State, string mag2State, string mag3State)
    {
        bool magWeak = mag1State == "LOW" || mag2State == "LOW" || mag3State == "LOW";
        return thermalDelta > threshold && magWeak;
    }
}
```

- [ ] **Step 4: Unityバッチモードでコンパイル確認・テスト実行**

Expected: コンパイル成功、`EsdEffectControllerTests` の4件がPassed。

---

### Task 4: ESD発生中のエネルギー素材ドロップ率上昇

**Files:**
- Modify: `Assets/VoidSurvival_10-Days/Scripts/Character/CharacterItemDropper.cs`
- Test: `Assets/VoidSurvival_10-Days/Tests/EditMode/CharacterItemDropperEsdTests.cs`

**Interfaces:**
- Produces: `CharacterItemDropper.ApplyEsdBonus(float baseRate, float multiplier) -> float`
  （`internal static`。既存の `EffectiveDropRate(float, string)` のシグネチャは変更しない）

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using NUnit.Framework;

public class CharacterItemDropperEsdTests
{
    [Test]
    public void ApplyEsdBonus_MultipliesRate()
    {
        Assert.AreEqual(0.6f, CharacterItemDropper.ApplyEsdBonus(0.4f, 1.5f), 0.001f);
    }

    [Test]
    public void ApplyEsdBonus_NeverExceedsOne()
    {
        Assert.LessOrEqual(CharacterItemDropper.ApplyEsdBonus(0.9f, 1.5f), 1.0f);
    }
}
```

- [ ] **Step 2: Unityバッチモードでテストを実行し、失敗を確認**

- [ ] **Step 3: `CharacterItemDropper.cs` に実装**

既存フィールドの近くに追加:

```csharp
[Tooltip("ESD発生中、エネルギー素材のドロップ率に掛ける倍率(仮値)")]
[SerializeField] private float esdDropRateMultiplier = 1.5f;
```

既存の `internal static float EffectiveDropRate(...)` の直後に追加:

```csharp
internal static float ApplyEsdBonus(float baseRate, float multiplier)
{
    return Mathf.Clamp01(baseRate * multiplier);
}
```

`DropIfNeeded()` 内、既存の
`if (Random.Range(0, 1f) >= EffectiveDropRate(drop.dropRate, currentState)) continue;`
を次のように変更する:

```csharp
float rate = EffectiveDropRate(drop.dropRate, currentState);
if (drop.item != null && drop.item.isEnergyMaterial
    && EsdEffectController.Instance != null && EsdEffectController.Instance.IsEsdActive)
{
    rate = ApplyEsdBonus(rate, esdDropRateMultiplier);
}
if (Random.Range(0, 1f) >= rate) continue;
```

（既存の `EffectiveDropRate(float, string)` の呼び出しと中身は変更しない。
ESD補正はその結果に対してさらに掛けるだけ）

- [ ] **Step 4: Unityバッチモードでコンパイル確認・テスト実行**

Expected: コンパイル成功、`CharacterItemDropperEsdTests` の2件がPassed。
既存の `CharacterItemDropperCurrentTests`（4件）も変更なくPassedであること。
既存の全テスト(Task1〜6a、28件)も引き続きPassedであること。

---

## 完了後の報告事項（Codex → Claude Code）

- 変更/作成したファイル一覧
- 作業中に見つかった、本プラン記載以外の問題（あれば報告のみ）
- `EsdEffectController`/`SeuEffectController` のシーンへの配置は本プランの範囲外
  （手動作業）である旨の確認
