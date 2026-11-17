# 移動系の残課題(C-1, C-2, C-4) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **この計画はCodex CLI（`codex exec`）が実行する。** Claude Code（オーケストレーター）が
> 本ファイルを渡し、Codexが実装、Claude Codeが差分レビューとコンパイル/テスト実行結果の
> 確認（Unityバッチモードは直接実行）を行う。

**Goal:** `docs/ai-handoff.md` で見つかった移動系の課題のうち、C-1（スタミナ消費の
フレーム依存）、C-2（PCキーボードのダッシュ割当）、C-4（Active中の慣性ゼロ化）を解消する。

## スコープ

**含む**: C-1, C-2, C-4（いずれも `PlayerController.cs` 内の修正）

**含まない**:
- C-5（斜め入力時のアニメーション、企画判断が必要なため`docs/ai-handoff.md`の
  未決事項のまま）
- C-3（SEU）は Task 6a で既に対応済み
- C-6（自動テスト基盤）は Task 2 で対応済み

**Architecture:** 各課題は独立した小さな修正。C-1はフレームごとの整数消費を
秒間レートの蓄積方式に変更。C-2は既存の`DashButton.IsDashing`（UIトグル）に加えて
キーボード入力も「ダッシュ要求」として扱う。C-4は`IsMovable`がfalseのときに
velocityを明示的に0にする。判定ロジックは`internal static`な純粋関数に切り出し、
`PlayerController`は`public class`なので既存の`InternalsVisibleTo`がそのまま使える。

**Tech Stack:** Unity 2023.1.22f1, C#, Unity Test Framework

**Spec:** `docs/ai-handoff.md` §3(課題C-1, C-2, C-4), §4.2(対応方針)。
`docs/superpowers/specs/2026-11-17-voidsurvival-ai-collab-design.md` §5 タスク#9。

## Global Constraints

- 作業ディレクトリは `vendor/VoidSurvival_10-Days/`
- upstream への push・merge は行わない
- **既存の `moveSpeed`（5f）、ダッシュ倍率（2倍）等の既存パラメータは変更しない**
- `Input.GetKey` 等、実際のキー入力を使う行は本体に残してよいが、
  **判定ロジック自体は入力値を引数で受け取る純粋関数に切り出し、テストすること**
  （`Input.GetKey` 自体はEditModeでテストしない）
- Unityバッチモードのコンパイル確認・テスト実行は **Claude Codeが直接実行する**。
  Codexは実装のみ行い、コミットもしない

## Review Focus

- **C-1の修正で、ダッシュ時間が同じならフレームレートに関わらずスタミナ消費量が
  ほぼ同じになること**（60fpsと30fpsで極端に差が出ないこと）
- **C-2の修正で、既存のモバイル用`DashButton`のトグル挙動が変わっていないこと**
  （キーボード入力を追加するだけで、既存の分岐を置き換えないこと）
- **C-4の修正で、`IsMovable`がtrueの間の既存の移動挙動が一切変わっていないこと**
- **既存のテスト(Task1〜7で追加した37件)が引き続きパスすること**

---

### Task 1(C-1): ダッシュ中のスタミナ消費をフレームレート非依存にする

**Files:**
- Modify: `Assets/VoidSurvival_10-Days/Scripts/Character/PlayerController.cs`
- Test: `Assets/VoidSurvival_10-Days/Tests/EditMode/PlayerControllerStaminaDrainTests.cs`

**Interfaces:**
- Produces: `PlayerController.CalculateStaminaDrain(float accumulator, float drainPerSecond, float deltaTime) -> (int amountToConsume, float remainingAccumulator)`
  （`internal static`）

**現状:** `_status.ConsumeStamina(1)` が `Update()` で毎フレーム呼ばれており、
60fpsなら1秒に60消費、30fpsなら1秒に30消費と、フレームレートに比例してしまう。

- [ ] **Step 1: 失敗するテストを書く**

`Assets/VoidSurvival_10-Days/Tests/EditMode/PlayerControllerStaminaDrainTests.cs`:

```csharp
using NUnit.Framework;

public class PlayerControllerStaminaDrainTests
{
    [Test]
    public void CalculateStaminaDrain_AccumulatesBelowOne_ConsumesNothingYet()
    {
        var (amount, remaining) = PlayerController.CalculateStaminaDrain(
            accumulator: 0f, drainPerSecond: 10f, deltaTime: 0.05f); // 60fps相当
        Assert.AreEqual(0, amount);
        Assert.AreEqual(0.5f, remaining, 0.001f);
    }

    [Test]
    public void CalculateStaminaDrain_AccumulatorReachesOne_ConsumesOne()
    {
        var (amount, remaining) = PlayerController.CalculateStaminaDrain(
            accumulator: 0.6f, drainPerSecond: 10f, deltaTime: 0.1f); // 0.6 + 1.0 = 1.6
        Assert.AreEqual(1, amount);
        Assert.AreEqual(0.6f, remaining, 0.001f);
    }

    [Test]
    public void CalculateStaminaDrain_SameElapsedTime_SameTotalConsumptionRegardlessOfFrameRate()
    {
        // 1秒間、60fps(dt=1/60)で60回更新した場合と、30fps(dt=1/30)で30回更新した場合で
        // 合計消費量がほぼ一致することを確認する(フレームレート非依存)。
        float drainPerSecond = 10f;

        float acc60 = 0f; int total60 = 0;
        for (int i = 0; i < 60; i++)
        {
            var (amount, remaining) = PlayerController.CalculateStaminaDrain(acc60, drainPerSecond, 1f / 60f);
            total60 += amount;
            acc60 = remaining;
        }

        float acc30 = 0f; int total30 = 0;
        for (int i = 0; i < 30; i++)
        {
            var (amount, remaining) = PlayerController.CalculateStaminaDrain(acc30, drainPerSecond, 1f / 30f);
            total30 += amount;
            acc30 = remaining;
        }

        Assert.AreEqual(total60, total30);
    }
}
```

- [ ] **Step 2: Unityバッチモードでテストを実行し、失敗を確認**

- [ ] **Step 3: `PlayerController.cs` に実装**

```csharp
[Tooltip("ダッシュ中のスタミナ消費速度(秒間)。仮値。バランス調整は別途")]
[SerializeField] private float dashStaminaPerSecond = 10f;
private float _staminaDrainAccumulator = 0f;
```

```csharp
/// <summary>
/// 蓄積方式でスタミナ消費量を計算する純粋関数。小数点分は次フレームに持ち越す。
/// </summary>
internal static (int amountToConsume, float remainingAccumulator) CalculateStaminaDrain(
    float accumulator, float drainPerSecond, float deltaTime)
{
    float updated = accumulator + drainPerSecond * deltaTime;
    int whole = Mathf.FloorToInt(updated);
    return (whole, updated - whole);
}
```

`Update()` 内の既存の `_status.ConsumeStamina(1);` を次のように変更する:

```csharp
var (amountToConsume, remaining) = CalculateStaminaDrain(_staminaDrainAccumulator, dashStaminaPerSecond, Time.deltaTime);
_staminaDrainAccumulator = remaining;
if (amountToConsume > 0)
{
    _status.ConsumeStamina(amountToConsume);
}
```

（ダッシュしていないフレームでは `_staminaDrainAccumulator` は変更しない。
既存の `if (_moveInput.magnitude > 0 && DashButton.IsDashing && _status.IsDashing)` の
分岐の中でのみ呼ぶこと）

- [ ] **Step 4: Unityバッチモードでコンパイル確認・テスト実行**

Expected: コンパイル成功、`PlayerControllerStaminaDrainTests` の3件がPassed。

---

### Task 2(C-2): PCキーボードでのダッシュ入力を追加

**Files:**
- Modify: `Assets/VoidSurvival_10-Days/Scripts/Character/PlayerController.cs`
- Test: `Assets/VoidSurvival_10-Days/Tests/EditMode/PlayerControllerDashInputTests.cs`

**Interfaces:**
- Produces: `PlayerController.IsDashRequested(bool dashButtonToggled, bool keyboardHeld) -> bool`
  （`internal static`）

**現状:** ダッシュの入力元は `DashButton.IsDashing`（画面タッチ用トグル）のみ。
PCでキーボードのみの操作だとダッシュを使う手段がない。

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using NUnit.Framework;

public class PlayerControllerDashInputTests
{
    [Test]
    public void IsDashRequested_DashButtonOnly_ReturnsTrue()
    {
        Assert.IsTrue(PlayerController.IsDashRequested(dashButtonToggled: true, keyboardHeld: false));
    }

    [Test]
    public void IsDashRequested_KeyboardOnly_ReturnsTrue()
    {
        Assert.IsTrue(PlayerController.IsDashRequested(dashButtonToggled: false, keyboardHeld: true));
    }

    [Test]
    public void IsDashRequested_Neither_ReturnsFalse()
    {
        Assert.IsFalse(PlayerController.IsDashRequested(dashButtonToggled: false, keyboardHeld: false));
    }
}
```

- [ ] **Step 2: Unityバッチモードでテストを実行し、失敗を確認**

- [ ] **Step 3: `PlayerController.cs` に実装**

```csharp
[Tooltip("PCキーボードでダッシュする際のキー")]
[SerializeField] private KeyCode dashKey = KeyCode.LeftShift;

internal static bool IsDashRequested(bool dashButtonToggled, bool keyboardHeld)
{
    return dashButtonToggled || keyboardHeld;
}
```

`Update()` 内の既存の条件式
`if (_moveInput.magnitude > 0 && DashButton.IsDashing && _status.IsDashing)`
を次のように変更する:

```csharp
bool dashRequested = IsDashRequested(DashButton.IsDashing, Input.GetKey(dashKey));
if (_moveInput.magnitude > 0 && dashRequested && _status.IsDashing)
```

（`DashButton` 側のモバイル用トグル実装は変更しない。キーボード入力を
OR条件で追加するだけ）

- [ ] **Step 4: Unityバッチモードでコンパイル確認・テスト実行**

Expected: コンパイル成功、`PlayerControllerDashInputTests` の3件がPassed。

---

### Task 3(C-4): Active状態中の慣性を打ち切る

**Files:**
- Modify: `Assets/VoidSurvival_10-Days/Scripts/Character/PlayerController.cs`

**現状:** `if (_status.IsMovable) { ... velocity = ... }` の分岐で、
`IsMovable` が false（攻撃中等）になった瞬間、直前のvelocityが変更されず
残ったままになり、慣性でわずかに動き続けてしまう。

- [ ] **Step 1: `PlayerController.cs` を修正**

既存の

```csharp
if (_status.IsMovable)
{
    // キャラクターのアニメーションを更新
    _renderer.SetDirection(_moveInput);

    // プレイヤーを動かす
    _rigidbody.velocity = _moveInput * _moveSpeed;
}
```

を次のように変更する:

```csharp
if (_status.IsMovable)
{
    // キャラクターのアニメーションを更新
    _renderer.SetDirection(_moveInput);

    // プレイヤーを動かす
    _rigidbody.velocity = _moveInput * _moveSpeed;
}
else
{
    // 移動不可状態(攻撃中等)になった際、直前の慣性で動き続けないようにする
    _rigidbody.velocity = Vector2.zero;
}
```

（この修正は `Rigidbody2D` の実際の動きに関わる確認であり、EditModeの
自動テストでは検証できない。手動のコード査読とコンパイル確認で確認する）

- [ ] **Step 2: Unityバッチモードでコンパイル確認**

Expected: コンパイル成功、`error CS` 0件。既存の全テスト(Task1〜7、37件)も
引き続きPassedであること（新規テストはTask1〜2で追加した6件のみ、合計43件）。

---

## 完了後の報告事項（Codex → Claude Code）

- 変更/作成したファイル一覧
- 作業中に見つかった、本プラン記載以外の問題（あれば報告のみ）
