# SEU(シングルイベントアップセット)実装 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **この計画はCodex CLI（`codex exec`）が実行する。** Claude Code（オーケストレーター）が
> 本ファイルを渡し、Codexが実装、Claude Codeが差分レビューとコンパイル/テスト実行結果の
> 確認（Unityバッチモードは直接実行）を行う。

**Goal:** SAA通過中（磁場が弱い かつ 放射線が強い）に確率でSEUを発生させ、
`PlayerStatus.IsInversion`（移動側に既にある未接続のフック、`docs/ai-handoff.md` C-3）
を実際に駆動する。発生確率はシールド装備で下がる。

## スコープ

**含む**: SAA判定、発生確率判定、Normal/Active/Cooldownの状態機械、
`IsInversion` の実際の切り替え、シールド装備による確率低下、デモ運用上の保険
（強制発火・強制終了の口を用意する）。

**含まない（別タスク）**:
- ESD（帯電・放電）— 別計画 `2026-11-17-esd-effect.md`（今後作成）
- UIの数字化け・AIセリフの文字化けなどの見た目の演出 — 別タスク（UI/演出）
- 「屋外にいる」条件 — ZONE-N のエリア分割（マスター設計書タスク#8）で
  屋内/遮蔽物の概念ができてから接続する。**本タスクでは屋外判定を省略し、
  常に判定対象とする**（既知の簡略化としてコードにコメントを残す）
- リブートボタン・発表者用ボタンのUI配線 — `ForceEndActive()`/`ForceTrigger()`
  という口（public メソッド）だけ用意し、ボタンへの接続は別タスク

**Architecture:** 新規コンポーネント `SeuEffectController`（シングルトン）が
`DataManager.Instance` の磁場・放射線状態を1秒ごとに確認し、SAA条件を満たせば
確率判定（シールド装備で倍率を掛ける）を行う。当たったら `PlayerStatus.IsInversion`
を `true` にし、一定時間後に `false` に戻してクールダウンへ移る。
判定条件・確率計算は `internal static` な純粋関数に切り出してテストする。

**Tech Stack:** Unity 2023.1.22f1, C#, Unity Test Framework

**Spec:** `docs/superpowers/specs/2026-11-17-voidsurvival-ai-collab-design.md`
（§5 タスク#6に対応）、`docs/ai-handoff.md` §2.7/§3(C-3)。
背景: `docs/VoidSurvival_DemoSpec_v0.3.md` §6.1, §6.3, §6.5, §6.6（git管理外）

## Global Constraints

- 作業ディレクトリは `vendor/VoidSurvival_10-Days/`
- upstream への push・merge は行わない
- 発生確率・持続時間・クールダウンの具体的な数値は、DemoSpec §6.3の調整パラメータ表
  （判定間隔1秒、発生確率3〜5%、シールド倍率0.2〜0.3、持続10秒、クールダウン20秒）の
  **中央値を仮値として使う**（判定間隔1秒、発生確率4%、シールド倍率0.25、
  持続12秒、クールダウン20秒）。すべて `[SerializeField]` でInspectorから
  調整可能にすること
- `PlayerStatus.IsInversion` 以外の既存コードの挙動は変更しない
- Unityバッチモードのコンパイル確認・テスト実行は **Claude Codeが直接実行する**。
  Codexは実装のみ行い、コミットもしない

## Review Focus

- **`DataManager.Instance` が null、またはデータ未ロードの場合に例外を出さないこと**
- **`PlayerStatus` が見つからない場合（シーンに存在しない等）に例外を出さないこと**
- **Active状態が終わったら必ず `IsInversion` が `false` に戻ること**
  （Cooldown中・Normal復帰後にtrueのまま残らないこと）
- **シールド装備の判定が、装備していないアイテムをシールドと誤認しないこと**
  （`isShield` フィールドが存在しない既存アイテム（デフォルト`false`）は
  シールド扱いされないこと）
- **既存のテスト(Task1〜5で追加した22件)が引き続きパスすること**

---

### Task 1: `Item` にシールド判定用フィールドを追加

**Files:**
- Modify: `Assets/VoidSurvival_10-Days/Scripts/Data/Item.cs`

- [ ] **Step 1: フィールドを追加**

`Item` 抽象クラスに追加（既存フィールドの直後、既存フィールドは変更しない）:

```csharp
[Tooltip("装備中にSEUの発生確率を下げる(シールド)アイテムかどうか")]
public bool isShield = false;
```

- [ ] **Step 2: Commitは行わない**（全タスク完了後にClaude Codeがまとめて行う）

---

### Task 2: `SeuEffectController` の実装

**Files:**
- Create: `Assets/VoidSurvival_10-Days/Scripts/SeuEffectController.cs`
- Test: `Assets/VoidSurvival_10-Days/Tests/EditMode/SeuEffectControllerTests.cs`

**Interfaces:**
- Produces:
  - `SeuEffectController.IsSaaCondition(string mag1State, string mag2State, string mag3State, string radiationState) -> bool`
  - `SeuEffectController.CalculateTriggerProbability(float baseProbability, bool shieldEquipped, float shieldMultiplier) -> float`
  （いずれも `internal static`）
- Consumes: `DataManager.Instance.{Mag1State,Mag2State,Mag3State,RadiationState}`,
  `ItemManager.Instance.equipmentSlot`, `PlayerStatus.IsInversion`

- [ ] **Step 1: 失敗するテストを書く**

`Assets/VoidSurvival_10-Days/Tests/EditMode/SeuEffectControllerTests.cs`:

```csharp
using NUnit.Framework;

public class SeuEffectControllerTests
{
    [Test]
    public void IsSaaCondition_MagWeakAndRadiationHigh_ReturnsTrue()
    {
        Assert.IsTrue(SeuEffectController.IsSaaCondition("LOW", "NORMAL", "NORMAL", "HIGH"));
    }

    [Test]
    public void IsSaaCondition_AnyMagWeak_ReturnsTrue()
    {
        Assert.IsTrue(SeuEffectController.IsSaaCondition("NORMAL", "NORMAL", "LOW", "HIGH"));
    }

    [Test]
    public void IsSaaCondition_NoMagWeak_ReturnsFalse()
    {
        Assert.IsFalse(SeuEffectController.IsSaaCondition("NORMAL", "NORMAL", "NORMAL", "HIGH"));
    }

    [Test]
    public void IsSaaCondition_RadiationNotHigh_ReturnsFalse()
    {
        Assert.IsFalse(SeuEffectController.IsSaaCondition("LOW", "NORMAL", "NORMAL", "NORMAL"));
    }

    [Test]
    public void CalculateTriggerProbability_NoShield_ReturnsBaseProbability()
    {
        Assert.AreEqual(0.04f,
            SeuEffectController.CalculateTriggerProbability(0.04f, shieldEquipped: false, shieldMultiplier: 0.25f),
            0.0001f);
    }

    [Test]
    public void CalculateTriggerProbability_WithShield_ReturnsReducedProbability()
    {
        Assert.AreEqual(0.01f,
            SeuEffectController.CalculateTriggerProbability(0.04f, shieldEquipped: true, shieldMultiplier: 0.25f),
            0.0001f);
    }
}
```

- [ ] **Step 2: Unityバッチモードでテストを実行し、失敗を確認**

（`SeuEffectController` クラス自体が存在しないためコンパイルエラーになることを確認）

- [ ] **Step 3: `SeuEffectController.cs` を実装**

```csharp
using UnityEngine;

/// <summary>
/// SEU(シングルイベントアップセット)の発生を管理するシングルトン。
/// SAA通過中(磁場が弱い かつ 放射線が強い)に確率で発生し、一定時間
/// PlayerStatus.IsInversion を true にする(移動の左右反転)。
///
/// 既知の簡略化: 「屋外にいる」条件は未実装(ZONE-Nのエリア分割タスクで対応予定)。
/// 現時点では場所を問わず判定する。
/// </summary>
public class SeuEffectController : MonoBehaviour
{
    public static SeuEffectController Instance { get; private set; }

    private enum SeuPhase { Normal, Active, Cooldown }

    [Header("SEU調整パラメータ(仮値。バランス調整は別途)")]
    [SerializeField] private float checkInterval = 1f;
    [SerializeField] private float baseTriggerProbability = 0.04f;
    [SerializeField] private float shieldProbabilityMultiplier = 0.25f;
    [SerializeField] private float activeDuration = 12f;
    [SerializeField] private float cooldownDuration = 20f;

    private SeuPhase _phase = SeuPhase.Normal;
    private float _timer;
    private PlayerStatus _playerStatus;

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
        _playerStatus = FindAnyObjectByType<PlayerStatus>();
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        switch (_phase)
        {
            case SeuPhase.Normal:
                if (_timer >= checkInterval)
                {
                    _timer = 0f;
                    TryTrigger();
                }
                break;
            case SeuPhase.Active:
                if (_timer >= activeDuration) EndActive();
                break;
            case SeuPhase.Cooldown:
                if (_timer >= cooldownDuration)
                {
                    _phase = SeuPhase.Normal;
                    _timer = 0f;
                }
                break;
        }
    }

    private void TryTrigger()
    {
        if (DataManager.Instance == null || _playerStatus == null) return;

        bool saa = IsSaaCondition(
            DataManager.Instance.Mag1State, DataManager.Instance.Mag2State,
            DataManager.Instance.Mag3State, DataManager.Instance.RadiationState);
        if (!saa) return;

        float probability = CalculateTriggerProbability(
            baseTriggerProbability, IsShieldEquipped(), shieldProbabilityMultiplier);
        if (Random.value < probability)
        {
            BeginActive();
        }
    }

    /// <summary>磁場のいずれか1つでも弱く(LOW)、かつ放射線が強い(HIGH)ならSAA通過中とみなす。</summary>
    internal static bool IsSaaCondition(string mag1State, string mag2State, string mag3State, string radiationState)
    {
        bool magWeak = mag1State == "LOW" || mag2State == "LOW" || mag3State == "LOW";
        bool radiationHigh = radiationState == "HIGH";
        return magWeak && radiationHigh;
    }

    /// <summary>シールド装備時は確率に倍率を掛けて下げる。</summary>
    internal static float CalculateTriggerProbability(float baseProbability, bool shieldEquipped, float shieldMultiplier)
    {
        return shieldEquipped ? baseProbability * shieldMultiplier : baseProbability;
    }

    private bool IsShieldEquipped()
    {
        if (ItemManager.Instance?.equipmentSlot == null) return false;
        foreach (var kv in ItemManager.Instance.equipmentSlot)
        {
            if (kv.Value != null && kv.Value.isShield) return true;
        }
        return false;
    }

    private void BeginActive()
    {
        _phase = SeuPhase.Active;
        _timer = 0f;
        if (_playerStatus != null) _playerStatus.IsInversion = true;
    }

    private void EndActive()
    {
        _phase = SeuPhase.Cooldown;
        _timer = 0f;
        if (_playerStatus != null) _playerStatus.IsInversion = false;
    }

    /// <summary>リブート操作等からSEUを早期終了させるための口(UI配線は別タスク)。</summary>
    public void ForceEndActive()
    {
        if (_phase == SeuPhase.Active) EndActive();
    }

    /// <summary>
    /// チュートリアルでの確定発火・発表者用の強制発火ボタンのための口
    /// (DemoSpec §6.6。UI配線は別タスク)。Normal状態のときのみ発火する。
    /// </summary>
    public void ForceTrigger()
    {
        if (_phase == SeuPhase.Normal) BeginActive();
    }
}
```

- [ ] **Step 4: Unityバッチモードでコンパイル確認・テスト実行**

Expected: コンパイル成功、`SeuEffectControllerTests` の6件がPassed。
既存の22件(Task1〜5)も引き続きPassedであること。

---

## 完了後の報告事項（Codex → Claude Code）

- 変更/作成したファイル一覧
- 作業中に見つかった、本プラン記載以外の問題（あれば報告のみ）
- `SeuEffectController` をシーンに配置する作業（GameObject作成・Inspector設定）は
  本プランのコード実装には含まれない旨の確認（Unity Editorでの手動配置が別途必要）
