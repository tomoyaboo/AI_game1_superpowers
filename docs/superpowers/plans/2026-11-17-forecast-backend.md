# 予報バックエンド + 中継局解除フラグ Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **この計画はCodex CLI（`codex exec`）が実行する。** Claude Code（オーケストレーター）が
> 本ファイルを渡し、Codexが実装、Claude Codeが差分レビューとコンパイル/テスト実行結果の
> 確認（Unityバッチモードは直接実行）を行う。

**Goal:** 予報端末・中継局（DemoSpec §7）のうち、**UI・シーン配置を除いたバックエンド部分**
だけを実装する。「現在値も将来値も同じ関数で計算する」という核心設計（§7.2）を
`DataManager`/`HKDataDisplay` に実装し、中継局の解除状態を `FlagData` に持たせる。

## スコープ

**含む**:
- `DataManager`/`HKDataDisplay` に「任意の時刻/オフセットでの状態を計算する」メソッドを追加
  （現在値の計算ロジックと完全に同じ関数を使う。DemoSpec §7.2の核心設計）
- 中継局1〜3の解除状態を `FlagData`/`FlagSaveData` に追加（§7.9「解除状態はFlagDataに
  持たせる」に対応。Task1で永続化の仕組みは既にできている）

**含まない（別タスク。UI・シーン配置が必要なため）**:
- 予報端末の画面（タブ2枚: 予報/生データ）のUI構築
- 中継局のGameObject配置、`InteractAction` を継承した中継局コンポーネント、
  通信ミニゲームとの接続
- 通信UIのプレハブ化（§7.9で指摘されている別の既知課題）

**Architecture:** `DataManager.Update()` は現在、現在時刻に対応するセンサーデータを検索して
分類している。この「検索+分類」のうち分類部分を `ClassifyAll(...)` という内部メソッドに
切り出し、`UpdateStates()`（現在値用、プロパティに書き込む）と新規
`SampleStatesAt(int targetTotalSec)`（任意時刻用、構造体を返すだけ）の両方がそれを呼ぶ
形にする。`HKDataDisplay` も同様に `ClassifyRow(JObject row)` を切り出す。

**Tech Stack:** Unity 2023.1.22f1, C#, Unity Test Framework

**Spec:** `docs/superpowers/specs/2026-11-17-voidsurvival-ai-collab-design.md`
（§5 タスク#7に対応）。背景: `docs/VoidSurvival_DemoSpec_v0.3.md` §7.2, §7.4, §7.6, §7.9
（git管理外）

## Global Constraints

- 作業ディレクトリは `vendor/VoidSurvival_10-Days/`
- upstream への push・merge は行わない
- **既存の `Update()`/`UpdateStates()`/`ShowRow()` の「現在値」としての挙動・UIの表示内容は
  変更しない。** 新規メソッドは追加するだけで、既存の呼び出し経路は変えない
- シーンファイル(.unity)は変更しない
- Unityバッチモードのコンパイル確認・テスト実行は **Claude Codeが直接実行する**。
  Codexは実装のみ行い、コミットもしない

## Review Focus

- **`SampleStatesAt`/`SampleStatesAtIndexOffset` が呼び出し元のインスタンス状態
  （`GyroState`等の現在値プロパティ）を変更しないこと**（副作用のない読み取り専用で
  あること）
- **データ未ロード時に `SampleStatesAt` 等を呼んでも例外を出さず、既定値(default)を
  返すこと**
- **`FlagSaveData` への新規フィールド追加が、既存の4フィールドのセーブ/ロードを
  壊していないこと**（Task1のテスト`FlagDataTests`が引き続きPassすること）
- **既存のテスト(Task1〜6bで追加した37件)が引き続きパスすること**

---

### Task 1: `FlagData` に中継局解除フラグを追加

**Files:**
- Modify: `Assets/VoidSurvival_10-Days/Scripts/Data/FlagData.cs`
- Modify: `Assets/VoidSurvival_10-Days/Scripts/SaveData/FlagSaveData.cs`

**現状:** `FlagData`/`FlagSaveData` は既存の4項目（`craftTutorial`等）のみ。

- [ ] **Step 1: `FlagSaveData.cs` にフィールドを追加**

既存の4フィールドの直後に追加:

```csharp
public bool RelayStation1Connected;
public bool RelayStation2Connected;
public bool RelayStation3Connected;
```

- [ ] **Step 2: `FlagData.cs` にフィールドを追加し、Save/Load処理に組み込む**

既存の `destructionRock`/`findSomeone` の直後に追加:

```csharp
[Header("中継局(予報の解除状態)")]
public bool relayStation1Connected = false; // 磁場→帯電の予報
public bool relayStation2Connected = false; // 放射線→SEUの予報
public bool relayStation3Connected = false; // 電流・温度→日照/食の予報
```

既存の `ToSaveData()`/`FromSaveData()` に、既存の4項目と同じパターンで追加:

```csharp
// ToSaveData() 内
RelayStation1Connected = relayStation1Connected,
RelayStation2Connected = relayStation2Connected,
RelayStation3Connected = relayStation3Connected,

// FromSaveData(saveData) 内
relayStation1Connected = saveData.RelayStation1Connected;
relayStation2Connected = saveData.RelayStation2Connected;
relayStation3Connected = saveData.RelayStation3Connected;
```

- [ ] **Step 3: Unityバッチモードでコンパイル確認・テスト実行**

Expected: コンパイル成功。既存の `FlagDataTests.ToSaveData_FromSaveData_RoundTrips`
（Task1で追加済み）が引き続きPassedであること（新規フィールドのデフォルト値`false`が
往復しても元のアサーションに影響しないことを確認）。新規フィールド自体の往復を
確認する専用テストは追加しなくてよい（既存テストの構造を変えずに確認できるため）。

---

### Task 2: `DataManager` に任意時刻でのセンサー状態計算を追加

**Files:**
- Modify: `Assets/VoidSurvival_10-Days/Scripts/PlayfabTest/DataManager.cs`

**Interfaces:**
- Produces: `DataManager.SampleStatesAt(int targetTotalSec) -> DataManager.SensorStates`,
  `DataManager.GetCurrentTargetTotalSec() -> int`

- [ ] **Step 1: 結果を表す構造体と、分類ロジックの切り出し**

`DataManager` クラス内に追加:

```csharp
public struct SensorStates
{
    public string GyroState;
    public string Mag1State;
    public string Mag2State;
    public string Mag3State;
    public string RadiationState;
}
```

既存の `UpdateStates(GyroData gyro, MagData m1, MagData m2, MagData m3, RadiationData rad, RadiationData prevRad)`
の中身を、新規の内部メソッド `ClassifyAll(...)` に移す（ロジックは変更しない。
値が無い項目は現在の `GyroState` 等のプロパティ値を既定値として使う):

```csharp
private SensorStates ClassifyAll(GyroData gyro, MagData m1, MagData m2, MagData m3, RadiationData rad, RadiationData prevRad)
{
    var states = new SensorStates
    {
        GyroState = GyroState,
        Mag1State = Mag1State,
        Mag2State = Mag2State,
        Mag3State = Mag3State,
        RadiationState = RadiationState
    };
    if (gyro != null) states.GyroState = Classify(Magnitude(gyro), _gyroMagMin, _gyroMagMax);
    if (m1 != null) states.Mag1State = Classify(Magnitude(m1), _mag1MagMin, _mag1MagMax);
    if (m2 != null) states.Mag2State = Classify(Magnitude(m2), _mag2MagMin, _mag2MagMax);
    if (m3 != null) states.Mag3State = Classify(Magnitude(m3), _mag3MagMin, _mag3MagMax);
    if (rad != null)
        states.RadiationState = (prevRad == null) ? "LOW" : ClassifyChange(Mathf.Abs(rad.SignalCount - prevRad.SignalCount));
    return states;
}

private void UpdateStates(GyroData gyro, MagData m1, MagData m2, MagData m3, RadiationData rad, RadiationData prevRad)
{
    var states = ClassifyAll(gyro, m1, m2, m3, rad, prevRad);
    GyroState = states.GyroState;
    Mag1State = states.Mag1State;
    Mag2State = states.Mag2State;
    Mag3State = states.Mag3State;
    RadiationState = states.RadiationState;
}
```

（`UpdateStates` の呼び出し元・呼び出し方は変更しない。中身を委譲するだけ）

- [ ] **Step 2: 現在時刻のオフセット計算を切り出す**

既存の `Update()` 内で `targetTotalSec` を計算している部分
（`gameElapsedMinutes`〜`targetTotalSec`の算出）を、そのまま新規メソッドに移す:

```csharp
public int GetCurrentTargetTotalSec()
{
    if (TimeManager.Instance == null) return _dataStartOffset;
    int gameElapsedMinutes = (TimeManager.Instance.GetHour() * 60) + TimeManager.Instance.GetMinute();
    int rawElapsedSec = Mathf.FloorToInt(gameElapsedMinutes * speedFactor);
    if (loop && _dataSpanSec > 0)
        rawElapsedSec %= (_dataSpanSec + 1);
    return _dataStartOffset + rawElapsedSec;
}
```

`Update()` 側は `int targetTotalSec = GetCurrentTargetTotalSec();` に置き換える
（計算結果は変わらない）。

- [ ] **Step 3: 任意時刻での状態計算メソッドを追加**

```csharp
/// <summary>
/// 任意の時刻(targetTotalSec)でのセンサー状態を計算する。現在値(Update→UpdateStates)と
/// 完全に同じ分類ロジック(ClassifyAll)を使う(DemoSpec §7.2)。
/// インスタンスのGyroState等のプロパティは変更しない(副作用なし)。
/// データ未ロード時は既定値(すべて"NORMAL")を返す。
/// </summary>
public SensorStates SampleStatesAt(int targetTotalSec)
{
    if (_rootData == null) return new SensorStates
    {
        GyroState = "NORMAL", Mag1State = "NORMAL", Mag2State = "NORMAL",
        Mag3State = "NORMAL", RadiationState = "NORMAL"
    };

    var cGyro = _rootData.Gyro?.Where(d => d.TotalSeconds <= targetTotalSec).OrderByDescending(d => d.TotalSeconds).FirstOrDefault();
    var cMag1 = _rootData.Mag1?.Where(d => d.TotalSeconds <= targetTotalSec).OrderByDescending(d => d.TotalSeconds).FirstOrDefault();
    var cMag2 = _rootData.Mag2?.Where(d => d.TotalSeconds <= targetTotalSec).OrderByDescending(d => d.TotalSeconds).FirstOrDefault();
    var cMag3 = _rootData.Mag3?.Where(d => d.TotalSeconds <= targetTotalSec).OrderByDescending(d => d.TotalSeconds).FirstOrDefault();
    var cRad = _rootData.Radiation?.Where(d => d.TotalSeconds <= targetTotalSec).OrderByDescending(d => d.TotalSeconds).FirstOrDefault();

    RadiationData prevRad = null;
    if (cRad != null && _radSorted != null)
    {
        int idx = _radSorted.IndexOf(cRad);
        if (idx > 0) prevRad = _radSorted[idx - 1];
    }

    return ClassifyAll(cGyro, cMag1, cMag2, cMag3, cRad, prevRad);
}
```

- [ ] **Step 4: Unityバッチモードでコンパイル確認**

Expected: コンパイル成功、`error CS` 0件。既存の全テスト(37件)が引き続きPassed。
（`SampleStatesAt`/`GetCurrentTargetTotalSec` は実データ(PlayFabロード済みの`_rootData`)
に依存するため、本タスクではEditModeテストを追加しない。手動のコード査読と
コンパイル確認のみで検証する）

---

### Task 3: `HKDataDisplay` に任意インデックスでの状態計算を追加

**Files:**
- Modify: `Assets/VoidSurvival_10-Days/Scripts/PlayfabTest/HKData/HKDataDisplay.cs`

**Interfaces:**
- Produces: `HKDataDisplay.SampleStatesAtIndexOffset(int offsetSplits) -> HKDataDisplay.HKStates`

- [ ] **Step 1: 結果を表す構造体と、分類ロジックの切り出し**

```csharp
public struct HKStates
{
    public string TempState;
    public string VoltageState;
    public string CurrentState;
    public float ThermalDelta;
}

private HKStates ClassifyRow(JObject row)
{
    return new HKStates
    {
        TempState = _tempCols.Count > 0 ? WorstState(_tempCols, row) : "NORMAL",
        VoltageState = _voltCols.Count > 0 ? WorstState(_voltCols, row) : "NORMAL",
        CurrentState = _currCols.Count > 0 ? WorstState(_currCols, row) : "NORMAL",
        ThermalDelta = ComputeThermalDelta(row, _allTempCols),
    };
}
```

既存の `ShowRow(int i)` 内、`TempState = ...`/`VoltageState = ...`/`CurrentState = ...`/
`CurrentThermalDelta = ...` の4行を次のように置き換える（計算結果は変わらない）:

```csharp
var states = ClassifyRow(row);
TempState = states.TempState;
VoltageState = states.VoltageState;
CurrentState = states.CurrentState;
CurrentThermalDelta = states.ThermalDelta;
```

- [ ] **Step 2: 任意オフセットでの状態計算メソッドを追加**

```csharp
/// <summary>
/// 現在の行から offsetSplits 分ずれた行の状態を計算する(予報用)。現在値(ShowRow)と
/// 完全に同じ分類ロジック(ClassifyRow)を使う(DemoSpec §7.2)。
/// インスタンスのTempState等のプロパティは変更しない(副作用なし)。
/// データ未ロード時は既定値を返す。
/// </summary>
public HKStates SampleStatesAtIndexOffset(int offsetSplits)
{
    if (_rows == null || _rows.Count == 0)
    {
        return new HKStates { TempState = "NORMAL", VoltageState = "NORMAL", CurrentState = "NORMAL", ThermalDelta = 0f };
    }

    int baseIndex = GetIndexFromGameTime();
    int rawIndex = baseIndex + offsetSplits;
    int idx = loop
        ? ((rawIndex % _rows.Count) + _rows.Count) % _rows.Count
        : Mathf.Clamp(rawIndex, 0, _rows.Count - 1);

    return ClassifyRow((JObject)_rows[idx]);
}
```

- [ ] **Step 3: Unityバッチモードでコンパイル確認・テスト実行**

Expected: コンパイル成功、`error CS` 0件。既存の全テスト(37件)が引き続きPassed
（特に `HKDataDisplayThermalDeltaTests` と `EsdEffectControllerTests` — `ComputeThermalDelta`
のシグネチャ・既存呼び出しを変更していないこと）。

---

## 完了後の報告事項（Codex → Claude Code）

- 変更/作成したファイル一覧
- 作業中に見つかった、本プラン記載以外の問題（あれば報告のみ）
- 予報端末UI・中継局のシーン配置が別途必要である旨の確認
