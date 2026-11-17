# 遮蔽物ゾーン(ShelterZone) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **この計画はCodex CLI（`codex exec`）が実行する。** Claude Code（オーケストレーター）が
> 本ファイルを渡し、Codexが実装、Claude Codeが差分レビューとコンパイル/テスト実行結果の
> 確認（Unityバッチモードは直接実行）を行う。

**Goal:** DemoSpec §8.3「ZONE-N のエリア分割」のうち、**コードだけで安全に実装できる
「遮蔽物にいる間はSEU判定をしない」という仕組み**を用意する。実際のZONE-Nの
手前/中間/奥のレイアウト・遮蔽物の配置はシーン編集が必要なため別作業とする。

## スコープ

**含む**: トリガーコライダーで「プレイヤーが遮蔽物の中にいるか」を検知する
汎用コンポーネント`ShelterZone`、それを読んでSEU判定をスキップする
`SeuEffectController`側の接続。

**含まない（シーン編集が必要なため別作業）**:
- ZONE-Nの実際のマップレイアウト（手前/中間/奥のエリア分け）
- 遮蔽物オブジェクトの配置、`ShelterZone`コンポーネントの実際の設置
- 中継局2・3基目の配置（Task7で解除フラグのみ実装済み）

**Architecture:** `ShelterZone` は `Collider2D`(Trigger)を持つ任意のGameObjectに
付けるコンポーネント。プレイヤーがトリガーに入ったら共有の静的状態
`PlayerShelterState.IsSheltered` を true にし、出たら false にする（同時に複数の
遮蔽物エリアが重なる可能性があるため、参照カウント方式にする）。
`SeuEffectController.TryTrigger()` はこれを読んで、遮蔽中は判定自体をスキップする
（DemoSpec §8.3「その中にいる間はSEUの判定をしない」に対応）。

**Tech Stack:** Unity 2023.1.22f1, C#, Unity Test Framework

**Spec:** `docs/superpowers/specs/2026-11-17-voidsurvival-ai-collab-design.md`
（§5 タスク#8に対応）。背景: `docs/VoidSurvival_DemoSpec_v0.3.md` §8.3（git管理外）

## Global Constraints

- 作業ディレクトリは `vendor/VoidSurvival_10-Days/`
- upstream への push・merge は行わない
- シーンファイル(.unity)は変更しない
- `SeuEffectController` の既存の判定条件（SAA条件・確率計算）は変更しない。
  遮蔽判定は「そもそも判定を実行するかどうか」の前段として追加するだけ
- Unityバッチモードのコンパイル確認・テスト実行は **Claude Codeが直接実行する**。
  Codexは実装のみ行い、コミットもしない

## Review Focus

- **複数の遮蔽物エリアが重なっている場合でも、正しく動作すること**
  （片方から出ても、もう片方にまだいるなら遮蔽中のままであること。
  参照カウント方式で対応する）
- **`ShelterZone` に触れるオブジェクトがプレイヤー以外の場合（敵等）に
  誤って状態を変更しないこと**（タグで判定すること）
- **既存のテスト(Task1〜7で追加した37件、Task9で追加予定の6件と合わせて計43件)が
  引き続きパスすること**

---

### Task 1: `PlayerShelterState` と `ShelterZone` の実装

**Files:**
- Create: `Assets/VoidSurvival_10-Days/Scripts/PlayerShelterState.cs`
- Create: `Assets/VoidSurvival_10-Days/Scripts/ShelterZone.cs`
- Test: `Assets/VoidSurvival_10-Days/Tests/EditMode/PlayerShelterStateTests.cs`

**Interfaces:**
- Produces: `PlayerShelterState.IsSheltered { get; }`,
  `PlayerShelterState.Enter()`, `PlayerShelterState.Exit()`

- [ ] **Step 1: 失敗するテストを書く**

`Assets/VoidSurvival_10-Days/Tests/EditMode/PlayerShelterStateTests.cs`:

```csharp
using NUnit.Framework;

public class PlayerShelterStateTests
{
    [SetUp]
    public void Reset()
    {
        // テスト間で静的状態が残らないよう、0になるまでExitしておく
        while (PlayerShelterState.IsSheltered) PlayerShelterState.Exit();
    }

    [Test]
    public void IsSheltered_Initially_False()
    {
        Assert.IsFalse(PlayerShelterState.IsSheltered);
    }

    [Test]
    public void Enter_SetsSheltered_True()
    {
        PlayerShelterState.Enter();
        Assert.IsTrue(PlayerShelterState.IsSheltered);
    }

    [Test]
    public void Exit_AfterSingleEnter_SetsSheltered_False()
    {
        PlayerShelterState.Enter();
        PlayerShelterState.Exit();
        Assert.IsFalse(PlayerShelterState.IsSheltered);
    }

    [Test]
    public void OverlappingZones_StillShelteredUntilAllExited()
    {
        // 2つの遮蔽物エリアが重なっている状況を想定(参照カウント方式)
        PlayerShelterState.Enter();
        PlayerShelterState.Enter();
        PlayerShelterState.Exit();
        Assert.IsTrue(PlayerShelterState.IsSheltered, "1つ目のエリアから出てもまだ2つ目の中にいる");
        PlayerShelterState.Exit();
        Assert.IsFalse(PlayerShelterState.IsSheltered, "両方から出たら遮蔽されていない");
    }

    [Test]
    public void Exit_WithoutEnter_DoesNotGoNegative()
    {
        // 対応するEnterが無いExitが呼ばれても、カウントが負にならずShelteredはfalseのまま
        PlayerShelterState.Exit();
        Assert.IsFalse(PlayerShelterState.IsSheltered);
    }
}
```

- [ ] **Step 2: Unityバッチモードでテストを実行し、失敗を確認**

- [ ] **Step 3: `PlayerShelterState.cs` を実装**

```csharp
/// <summary>
/// プレイヤーが遮蔽物(ShelterZone)の中にいるかどうかを保持する共有状態。
/// 複数のエリアが重なる可能性があるため参照カウント方式にする。
/// </summary>
public static class PlayerShelterState
{
    private static int _overlapCount = 0;

    public static bool IsSheltered => _overlapCount > 0;

    public static void Enter()
    {
        _overlapCount++;
    }

    public static void Exit()
    {
        _overlapCount = UnityEngine.Mathf.Max(0, _overlapCount - 1);
    }
}
```

- [ ] **Step 4: `ShelterZone.cs` を実装**

```csharp
using UnityEngine;

/// <summary>
/// プレイヤーがこのトリガーの中にいる間、PlayerShelterState.IsSheltered を true にする。
/// ZONE-Nの遮蔽物オブジェクトにColliderD2D(IsTrigger)と一緒に付ける。
/// </summary>
public class ShelterZone : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
        {
            PlayerShelterState.Enter();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
        {
            PlayerShelterState.Exit();
        }
    }
}
```

- [ ] **Step 5: Unityバッチモードでコンパイル確認・テスト実行**

Expected: コンパイル成功、`PlayerShelterStateTests` の5件がPassed。

---

### Task 2: `SeuEffectController` を遮蔽状態に対応させる

**Files:**
- Modify: `Assets/VoidSurvival_10-Days/Scripts/SeuEffectController.cs`

**現状:** `TryTrigger()` は屋外/屋内を問わず判定している
（Task6aで「既知の簡略化」として明記していた箇所）。

- [ ] **Step 1: `SeuEffectController.cs` の `TryTrigger()` を修正**

既存の

```csharp
private void TryTrigger()
{
    if (DataManager.Instance == null || _playerStatus == null) return;

    bool saa = IsSaaCondition(...);
```

を次のように変更する（先頭に1行追加するだけ）:

```csharp
private void TryTrigger()
{
    if (DataManager.Instance == null || _playerStatus == null) return;
    if (PlayerShelterState.IsSheltered) return; // 遮蔽物の中にいる間は判定しない

    bool saa = IsSaaCondition(...);
```

クラスのXMLコメントにある「既知の簡略化: 「屋外にいる」条件は未実装」の記述を、
実装したことに合わせて更新する（コメントを削除し、「遮蔽物の中にいる間は判定しない」
という旨に書き換える）。

- [ ] **Step 2: Unityバッチモードでコンパイル確認・テスト実行**

Expected: コンパイル成功。既存の `SeuEffectControllerTests`（6件）が引き続きPassed
（遮蔽状態のデフォルトは `false` のため、既存テストの結果に影響しないことを確認）。
既存の全テストも引き続きPassedであること。

---

## 完了後の報告事項（Codex → Claude Code）

- 変更/作成したファイル一覧
- `ShelterZone` の実際のシーンへの配置は本プランの範囲外（手動作業）である旨の確認
- 作業中に見つかった、本プラン記載以外の問題（あれば報告のみ）
