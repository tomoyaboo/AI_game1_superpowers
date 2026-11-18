# S4 帯電を待つ(HUB-A→ZONE-N 2回目) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** カット割りS4「帯電を待つ」のN節(前言撤回・方針転換)を、HUB-A 2回目訪問時にだけ再生される新規シーケンスとして実装する。O節(帯電機体との戦闘)とP節(加工)は「演出を入れない」とカット割り自身が明記しているため、新規シーケンスは作らず、既存のEnemySpawner/CharacterItemDropper/WorkbenchManager/CraftingManagerに必要なデータとロジックを足すことで、プレイヤーが自分の判断で遊べる状態にする。

**Architecture:** `HubAIntroSequence`(S2, 1回目訪問)と`HubAChargeWaitSequence`(新規, 2回目訪問)を同じ `2_HubA_Demo.unity` シーンに共存させ、`FlagData.Instance.zoneNFirstVisitComplete` で「今が1回目か2回目か」を判定して片方だけが実際に台詞を進める。帯電個体の見た目は新規コンポーネント`ChargedEnemyVisual`が`DataManager`のMAG値を見て切り替える。帯電由来の素材ドロップは`CharacterItemDropper`に二値ゲート(ESD発生中でなければ0%)を追加して「帯電していない個体からは取れない」を再現する。クラフトは既存の`WorkbenchManager`/`CraftingManager`にレシピを追加するだけで、新規UIやシーケンスは作らない。

**Tech Stack:** Unity 2023.1.22f1, C#, NUnit (EditMode), 既存 FlagData / EsdEffectController / CharacterItemDropper / WorkbenchManager / CraftingManager / MessageText。

**Spec:** `docs/VoidSurvival_カット割り_2.md` (S4節, 617〜701行)

## Global Constraints

- 元のオリジナルシーン・原本アセット(`Assets/Scenes/Indoor/HUB-A.unity`等)は一切変更しない。配置・調整は `Assets/Scenes/Demo/` 配下と既存のDataアセット追加のみで行う。
- `vendor/VoidSurvival_10-Days/.git` は存在しない(個人リポジトリ内で直接管理)。upstream `NakaAwjr/VoidSurvival_10-Days` には絶対にpush/マージしない。
- 新規C#ファイルは `VoidSurvival.Runtime.asmdef` の対象ディレクトリ(`Assets/VoidSurvival_10-Days/Scripts/`)に置く。
- 既存の`FlagData`/`CharacterItemDropper`/`WorkbenchManager`/`CraftingManager`/`EsdEffectController`のAPIは変更せず、後方互換な拡張のみ行う。
- イラスト・新規アートは作れないため、帯電個体の見た目は既存`SpriteRenderer`の色調変更(青白いtint)で代用する。

## Review Focus

- `HubAIntroSequence`(1回目訪問)が2回目以降の訪問でも毎回再生されてしまうと、S4の前提(プレイヤーは既に「避けろ」と言われている)が壊れる。フラグでの排他制御が両方向(1回目はHubAIntroSequenceのみ、2回目以降はHubAChargeWaitSequenceのみ)で効くこと。
- `CharacterItemDropper`のESDゲートが、ESD関連の既存テスト(`EffectiveDropRate`/`ApplyEsdBonus`)の挙動を壊さないこと(非エネルギー素材のドロップ率計算は従来通り)。
- `ChargedEnemyVisual`が`DataManager.Instance`未初期化時にnull参照で落ちないこと。
- `FlagData.Instance`が(Resources未配置等で)nullの場合に`HubAChargeWaitSequence`/`ZoneNIntroSequence`が例外で止まらないこと。
- 新規レシピ(`BombRecipe`)の必要アイテムが揃っていない状態で`CraftingManager.CraftItem`を呼んでも例外にならないこと(既存の`NumberCanCrafting`ガードで担保されているため、レシピ投入時に壊さないことだけ確認する)。

---

### Task 1: ZONE-N初回訪問フラグの追加と配線

**Files:**
- Modify: `Assets/VoidSurvival_10-Days/Scripts/Data/FlagData.cs`
- Modify: `Assets/VoidSurvival_10-Days/Scripts/SaveData/FlagSaveData.cs`
- Modify: `Assets/VoidSurvival_10-Days/Scripts/Events/ZoneNIntroSequence.cs:112-119`(M節の末尾)
- Modify: `Assets/VoidSurvival_10-Days/Scripts/Events/HubAIntroSequence.cs:87-100`(`Start()`冒頭)

**Interfaces:**
- Consumes: なし(既存`FlagData.Instance`のみ)
- Produces: `FlagData.zoneNFirstVisitComplete`(public bool, デフォルトfalse)。Task 4がこれを読む。

- [ ] **Step 1: `FlagData.cs`に`public bool zoneNFirstVisitComplete = false;`を追加し、`ToSaveData()`/`FromSaveData()`にも対応するフィールドを追加**

`FlagSaveData`に`public bool ZoneNFirstVisitComplete;`を追加し、`ToSaveData`/`FromSaveData`の対応行も追加する。

- [ ] **Step 2: `ZoneNIntroSequence.RunSequence()`のM節末尾(`SceneManager.LoadScene(nextSceneName)`の直前)に`if (FlagData.Instance != null) FlagData.Instance.zoneNFirstVisitComplete = true;`を追加**

- [ ] **Step 3: `HubAIntroSequence.Start()`の冒頭に、`FlagData.Instance != null && FlagData.Instance.zoneNFirstVisitComplete`が真なら`yield break`する分岐を追加**(`Start()`は`StartCoroutine(RunSequence())`を呼ぶだけなので、`RunSequence()`の先頭に`if (FlagData.Instance != null && FlagData.Instance.zoneNFirstVisitComplete) yield break;`を追加する)

- [ ] **Step 4: Unityバッチモードでコンパイルを確認**

Run: `Unity -batchmode -nographic -quit -projectPath vendor/VoidSurvival_10-Days -logFile -`
Expected: exit code 0、コンパイルエラーなし。

- [ ] **Step 5: Commit**

```bash
git add vendor/VoidSurvival_10-Days/Assets/VoidSurvival_10-Days/Scripts/Data/FlagData.cs \
  vendor/VoidSurvival_10-Days/Assets/VoidSurvival_10-Days/Scripts/SaveData/FlagSaveData.cs \
  vendor/VoidSurvival_10-Days/Assets/VoidSurvival_10-Days/Scripts/Events/ZoneNIntroSequence.cs \
  vendor/VoidSurvival_10-Days/Assets/VoidSurvival_10-Days/Scripts/Events/HubAIntroSequence.cs
git commit -m "feat: ZONE-N初回訪問完了フラグを追加しHubAIntroSequenceを1回限りにする"
```

---

### Task 2: `CharacterItemDropper`のESDエネルギー素材ドロップを二値ゲート化

**Files:**
- Modify: `Assets/VoidSurvival_10-Days/Scripts/Character/CharacterItemDropper.cs`
- Test: `Assets/VoidSurvival_10-Days/Tests/EditMode/CharacterItemDropperTests.cs`(既存ファイルに追記)

**Interfaces:**
- Consumes: 既存`EffectiveDropRate`/`ApplyEsdBonus`(内部で呼び出すのみ、シグネチャ変更なし)
- Produces: `CharacterItemDropper.ShouldAttemptEnergyMaterialDrop(bool isEnergyMaterial, bool esdActive) -> bool`(`internal static`)。`DropIfNeeded()`がこれを使う。

- [ ] **Step 1: 失敗するテストを書く**

```csharp
[Test]
public void ShouldAttemptEnergyMaterialDrop_NonEnergyMaterial_AlwaysTrue()
{
    Assert.IsTrue(CharacterItemDropper.ShouldAttemptEnergyMaterialDrop(isEnergyMaterial: false, esdActive: false));
    Assert.IsTrue(CharacterItemDropper.ShouldAttemptEnergyMaterialDrop(isEnergyMaterial: false, esdActive: true));
}

[Test]
public void ShouldAttemptEnergyMaterialDrop_EnergyMaterialWithoutEsd_False()
{
    Assert.IsFalse(CharacterItemDropper.ShouldAttemptEnergyMaterialDrop(isEnergyMaterial: true, esdActive: false));
}

[Test]
public void ShouldAttemptEnergyMaterialDrop_EnergyMaterialWithEsd_True()
{
    Assert.IsTrue(CharacterItemDropper.ShouldAttemptEnergyMaterialDrop(isEnergyMaterial: true, esdActive: true));
}
```

- [ ] **Step 2: テストを実行し失敗を確認**(`ShouldAttemptEnergyMaterialDrop`が存在しないためコンパイルエラーになることを確認)

- [ ] **Step 3: `CharacterItemDropper.cs`に`internal static bool ShouldAttemptEnergyMaterialDrop(bool isEnergyMaterial, bool esdActive)`を実装**

`return !isEnergyMaterial || esdActive;` の1行で表現できる。

- [ ] **Step 4: `DropIfNeeded()`のループ内、`EffectiveDropRate`計算の前に次のガードを追加**

```csharp
bool esdActive = EsdEffectController.Instance != null && EsdEffectController.Instance.IsEsdActive;
if (drop.item != null && !ShouldAttemptEnergyMaterialDrop(drop.item.isEnergyMaterial, esdActive)) continue;
```

既存の`isEnergyMaterial && IsEsdActive`判定(ボーナス倍率適用部分)はそのまま残し、このガードは「そもそも抽選に参加するか」を先に絞る形にする。

- [ ] **Step 5: テストを実行し成功を確認**

- [ ] **Step 6: Commit**

```bash
git add vendor/VoidSurvival_10-Days/Assets/VoidSurvival_10-Days/Scripts/Character/CharacterItemDropper.cs \
  vendor/VoidSurvival_10-Days/Assets/VoidSurvival_10-Days/Tests/EditMode/CharacterItemDropperTests.cs
git commit -m "feat: ESD発生中でなければエネルギー素材をドロップしないよう修正"
```

---

### Task 3: `ChargedEnemyVisual`の実装(帯電個体の見た目)

**Files:**
- Create: `Assets/VoidSurvival_10-Days/Scripts/ChargedEnemyVisual.cs`
- Test: `Assets/VoidSurvival_10-Days/Tests/EditMode/ChargedEnemyVisualTests.cs`

**Interfaces:**
- Consumes: `DataManager.Instance.{Mag1State,Mag2State,Mag3State}`(既存、"LOW"/"NORMAL"/"HIGH"の文字列)
- Produces: `ChargedEnemyVisual.IsChargedCondition(string mag1State, string mag2State, string mag3State) -> bool`(`internal static`)。Task 4は使わないが、Non-Code Follow-upでEnemy.prefabに付与する。

- [ ] **Step 1: 失敗するテストを書く**

```csharp
[Test]
public void IsChargedCondition_AnyMagLow_True()
{
    Assert.IsTrue(ChargedEnemyVisual.IsChargedCondition("LOW", "NORMAL", "NORMAL"));
    Assert.IsTrue(ChargedEnemyVisual.IsChargedCondition("NORMAL", "LOW", "NORMAL"));
    Assert.IsTrue(ChargedEnemyVisual.IsChargedCondition("NORMAL", "NORMAL", "LOW"));
}

[Test]
public void IsChargedCondition_AllNormalOrHigh_False()
{
    Assert.IsFalse(ChargedEnemyVisual.IsChargedCondition("NORMAL", "NORMAL", "NORMAL"));
    Assert.IsFalse(ChargedEnemyVisual.IsChargedCondition("HIGH", "NORMAL", "NORMAL"));
}
```

- [ ] **Step 2: テストを実行し失敗を確認**

- [ ] **Step 3: `ChargedEnemyVisual.cs`を実装**

```csharp
using UnityEngine;

/// <summary>
/// カット割りS4 O-4「帯電状態で現れる」の簡易表現。
/// MAGのいずれかがLOWのとき、SpriteRendererの色を青白く変える。
/// </summary>
public class ChargedEnemyVisual : MonoBehaviour
{
    [SerializeField] private float checkInterval = 1f;
    [SerializeField] private Color chargedColor = new Color(0.6f, 0.8f, 1f, 1f);

    private SpriteRenderer _spriteRenderer;
    private Color _defaultColor;
    private float _timer;

    private void Start()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null) _defaultColor = _spriteRenderer.color;
    }

    private void Update()
    {
        if (_spriteRenderer == null || DataManager.Instance == null) return;
        _timer += Time.deltaTime;
        if (_timer < checkInterval) return;
        _timer = 0f;

        bool charged = IsChargedCondition(
            DataManager.Instance.Mag1State, DataManager.Instance.Mag2State, DataManager.Instance.Mag3State);
        _spriteRenderer.color = charged ? chargedColor : _defaultColor;
    }

    internal static bool IsChargedCondition(string mag1State, string mag2State, string mag3State)
    {
        return mag1State == "LOW" || mag2State == "LOW" || mag3State == "LOW";
    }
}
```

- [ ] **Step 4: テストを実行し成功を確認**

- [ ] **Step 5: Unityバッチモードでコンパイルを確認**

- [ ] **Step 6: Commit**

```bash
git add vendor/VoidSurvival_10-Days/Assets/VoidSurvival_10-Days/Scripts/ChargedEnemyVisual.cs \
  vendor/VoidSurvival_10-Days/Assets/VoidSurvival_10-Days/Scripts/ChargedEnemyVisual.cs.meta \
  vendor/VoidSurvival_10-Days/Assets/VoidSurvival_10-Days/Tests/EditMode/ChargedEnemyVisualTests.cs
git commit -m "feat: ChargedEnemyVisual(帯電個体の見た目)を実装"
```

---

### Task 4: `HubAChargeWaitSequence`の実装(N節、2回目訪問限定)

**Files:**
- Create: `Assets/VoidSurvival_10-Days/Scripts/Events/HubAChargeWaitSequence.cs`

**Interfaces:**
- Consumes: `MessageText.Instance.TextMessage(List<string>)`, `FlagData.Instance.zoneNFirstVisitComplete`(Task 1で追加済み)
- Produces: なし(本シーケンスの終了後はプレイヤーが自由に行動する。シーン遷移は行わない)

- [ ] **Step 1: `HubAChargeWaitSequence.cs`を実装**

`TerminalIntroSequence`/`HubAIntroSequence`/`ZoneNIntroSequence`と同じ構造(台詞リストを`private static readonly List<string>`で保持し、`Start()`から`RunSequence()`コルーチンを起動)で以下を実装する:

1. 冒頭で`FlagData.Instance == null || !FlagData.Instance.zoneNFirstVisitComplete`なら`yield break`(1回目訪問、または2回目の判定ができない場合は何もしない)。
2. N-1: 「爆薬がいる。爆薬には、電気の塊がいる」「……電気の塊が、いる」を表示。
3. N-2: 帯電の説明3行を表示。
4. N-3: 「……さっき、相手にするなと言ったな」「撤回する」「電気の塊は、あいつらからしか取れない。壊せ」を表示。
5. N-4: 観測値の見方の説明3行を表示。
6. N-5: 「ただし、同じところで線量も上がる。……さっき君が食らったやつだ」「同じ条件だ。分けられん」を表示。
7. N-6: 「行くなら、覚悟していけ」「戻る場所は覚えておけよ。岩陰でいい」を表示。
8. N-7: 2秒待機後、「……戻る場所は、覚えて……おけ」を表示して終了(シーン遷移はしない。`ShowLines`相当のヘルパーで`MessageText.Instance`/`MainUI.Instance`/`TimeManager.Instance`のnullガードを行う、`ZoneNIntroSequence.ShowLines`と同じ実装パターンを使う)。

台詞文面はカット割り記載のまま(617〜660行)を使う。各節の待機秒数はカット割りに明記の秒数を使う。

- [ ] **Step 2: Unityバッチモードでコンパイルを確認**

- [ ] **Step 3: Commit**

```bash
git add vendor/VoidSurvival_10-Days/Assets/VoidSurvival_10-Days/Scripts/Events/HubAChargeWaitSequence.cs \
  vendor/VoidSurvival_10-Days/Assets/VoidSurvival_10-Days/Scripts/Events/HubAChargeWaitSequence.cs.meta
git commit -m "feat: HubAChargeWaitSequence(S4 前言撤回 N節)を実装"
```

---

## Non-Code Follow-up (このプランのスコープ外、Claude側で後続実施)

- `2_HubA_Demo.unity`/`3_ZoneN_Demo.unity`/`4_A00_Demo.unity`内の`SceneMove`プレファブインスタンスの`m_SceneName`オーバーライドが、いまだにオリジナルシーン名(`ZONE-N`/`HUB-A`/`A0-0`)を指している不具合を、対応する`_Demo`シーン名に直す(扉を実際に通ったときにオリジナルシーンへ漏れてしまうバグの修正。既存のバッチEditorスクリプトパターンで対応)。
- `Energy.asset`の`isEnergyMaterial`を`true`に設定する(既存の「エネルギー」アイテムをS4の「静電エネルギー」として再利用する)。
- `ProcessedEnergy`(加工済みエネルギー)MaterialItemアセットを新規作成し、`ProcessedEnergyRecipe`(WorkbenchRecipe: Energy→ProcessedEnergy)、`BombRecipe`(CraftingRecipe: ProcessedEnergy+ScrapIron→Bomb、既存の`Bomb.asset`を結果アイテムにする)を作成して、それぞれ`WorkbenchRecipeDataBase.asset`/`CraftingRecipeDatabase.asset`の`Values`に登録する。
- `ChargedEnemyVisual`を`Assets/VoidSurvival_10-Days/Prefabs/Calacters/Enemy.prefab`に追加する(共有プレファブなので1回の変更で両シーンの敵に反映される)。
- `HubAChargeWaitSequence`を`2_HubA_Demo.unity`に配置する(既存の`HubAIntroSequence`オブジェクトとは別のGameObjectとして共存させる)。
