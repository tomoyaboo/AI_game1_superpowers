# 既知バグ修正 + FlagData永続化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **この計画はCodex CLI（`codex exec`）が実行する。** Claude Code（オーケストレーター）が
> タスクごとに本ファイルの該当セクションを渡し、Codexが実装、Claude Codeが差分をレビューする。

**Goal:** `VoidSurvival_10-Days` の既知の2つのコードバグ（NullReferenceException誘発、
ドロップ抽選ロジックのミス）を修正し、`FlagData`（進行フラグ）のセーブ/ロードを実装する。

**Architecture:** 既存の `SaveData`/`SaveManager` のパターン（`ItemSaveData`/`PlayerSaveData`
と同じ形）を踏襲して `FlagSaveData` を追加する。既存クラスの責務分担・継承構造は変更しない。

**Tech Stack:** Unity 2023.1.22f1, C#（Assembly-CSharpのまま。新規asmdefは作らない）

**Spec:** `docs/superpowers/specs/2026-11-17-voidsurvival-ai-collab-design.md`
（§5 タスク#1に対応）。背景資料: `docs/ai-handoff.md`、
`docs/VoidSurvival_DemoSpec_v0.3.md` §11.4（git管理外、ローカル参照のみ）

## Global Constraints

- 作業ディレクトリは `vendor/VoidSurvival_10-Days/`。この配下にのみ変更を加える
- upstream（`NakaAwjr/VoidSurvival_10-Days`）への push・merge は一切行わない
  （`vendor/VoidSurvival_10-Days` に `.git` は存在しないため、技術的に不可能）
- 既存のコードスタイル・命名規則（例: `SaveData` の `public PlayerSaveData PlayerSaveData;`
  のように型名とプロパティ名を一致させる慣習）に合わせる
- **本タスクでは新規に `.asmdef` を作らない。** Unity Test Framework による自動テスト基盤の
  整備は別タスク（spec §5 タスク#2として繰り上げ済み）で行う。本タスクの「テスト」は
  Unity Editorのバッチモードでのコンパイル確認と、手動トレースによる検証とする
- 対象外（今回は直さない。設計上の数値決定が必要なため別途確認）:
  `Hammer.asset`/`BaseballBat.asset` の `Power: 0`、`HammerRecipe.asset` の
  `RequiredItems: []`、各施設アセットの必要素材リスト空。これらはコードバグではなく
  ゲームデザイン上の数値未設定であり、本プランのスコープ外

## Review Focus

- **FlagData.FromSaveData/ToSaveData の呼び出し元が存在しない状態からの変更**:
  `SaveManager` 側の呼び出し追加漏れがないか（ロード時・セーブ時の両方）
- **RemoveItemの修正が既存の正常系（在庫にあるアイテムの削除）を壊していないか**:
  既存の `if (existingStack.Item != null)` を単純に `if (existingStack != null)` に
  変えるだけで、分岐の中身（`existingStack.Remove` 等）は変更しない
- **CharacterItemDropperの修正が複数ドロップ候補の挙動を変えすぎないか**:
  `break`→`continue` は「候補ごとに独立判定」に変える意図通りの変更であり、
  一つも当たらない場合に何もドロップしない、という既存の暗黙挙動は維持されること
- **FlagData.asset のリセットが意図しない他のフラグ(craftTutorial/craftWorkbench)まで
  変更していないか**: 変更対象は `destructionRock`/`findSomeone` の2つのみ
- **既存のビルド（コンパイル）が一切壊れていないこと**: 全タスク共通で
  Unity バッチモードのコンパイルチェックを最終ステップに置く

---

### Task 1: `ItemManager.RemoveItem` の null チェック修正

**Files:**
- Modify: `Assets/VoidSurvival_10-Days/Scripts/ItemManager.cs:171`

**Interfaces:**
- 変更しない（`RemoveItem(Item item, int amount = 1)` のシグネチャは既存のまま）

**現状のバグ:** `GetItemStack(item)` は `List<ItemStack>.FirstOrDefault` を使っており、
該当アイテムが無い場合は `null` を返す（170行目）。しかし171行目は
`if (existingStack.Item != null)` と書かれており、`existingStack` 自体が `null` のとき
`existingStack.Item` へのアクセスで `NullReferenceException` が発生する。
（`AddItem`側の同等チェックは137行目で `if (existingStack != null)` と正しく書かれている）

- [ ] **Step 1: 手動トレースで再現を確認する**

`ItemManager.Instance.RemoveItem(item, 1)` を、`item` が現在の `inventory` に
存在しない状態で呼んだ場合の実行経路を確認する:
`GetItemStack` → `FirstOrDefault` が見つからず `null` を返す → 171行目
`existingStack.Item` で `NullReferenceException`。

- [ ] **Step 2: `ItemManager.cs:171` を修正**

```csharp
var existingStack = GetItemStack(item);
if (existingStack != null)
```

に変更する（`.Item != null` を削除するだけ。以降の中身は変更しない）。

- [ ] **Step 3: 手動トレースで修正後の挙動を確認**

同じ呼び出し（在庫に無いアイテムの `RemoveItem`）が、例外を投げずに
`else` 節（203行目付近）の `Debug.LogWarning($"Item {item.name} not found in inventory.");`
に到達することを確認する。既存の「在庫にあるアイテムを削除する」経路
（`existingStack.Remove` 以降）はコード上変更していないことを確認する。

- [ ] **Step 4: Unityバッチモードでコンパイル確認**

Run:
```bash
/Applications/Unity/Hub/Editor/2023.1.22f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographic -quit \
  -projectPath "$(pwd)" \
  -logFile "$(pwd)/.ci/unity-compile.log"
```
Expected: 終了コード `0`。ログに `error CS` を含む行が無いこと
（`grep "error CS" .ci/unity-compile.log` が何も出力しない）。

- [ ] **Step 5: Commit**

```bash
git add Assets/VoidSurvival_10-Days/Scripts/ItemManager.cs
git commit -m "fix: RemoveItem の null チェック漏れを修正(NullReferenceException対策)"
```

---

### Task 2: `CharacterItemDropper` のドロップ抽選ロジック修正

**Files:**
- Modify: `Assets/VoidSurvival_10-Days/Scripts/Character/CharacterItemDropper.cs:35`

**Interfaces:**
- 変更しない（`DropIfNeeded()` は private のまま、呼び出し元 `_status.onDie` も変更しない）

**現状のバグ:** `DropIfNeeded()` の35行目 `if (Random.Range(0, 1f) >= drop.dropRate) break;`
は、`_items` 内のある1つの候補がドロップ抽選に外れた時点で**ループ全体を終了**させる。
後続の候補（当たるはずだったかもしれない他のドロップ品）が一切判定されずに
スキップされる。正しくは「その候補だけスキップして次の候補を判定する」`continue` であるべき。

- [ ] **Step 1: 手動トレースで再現を確認する**

`_items` に候補A（dropRate低）, 候補B（dropRate高）がこの順で入っている場合、
Aの抽選に外れると `break` によりB以降が全く判定されず、Bが当たるはずでもドロップされない。
リストの並び順によってドロップ率が実質的に歪む。

- [ ] **Step 2: `CharacterItemDropper.cs:35` を修正**

```csharp
if (Random.Range(0, 1f) >= drop.dropRate) continue;
```

に変更する（`break` → `continue`。他の行は変更しない）。

- [ ] **Step 3: 手動トレースで修正後の挙動を確認**

同じ候補A, Bの例で、Aが外れてもBの判定に進み、Bの`dropRate`に基づいて
独立に抽選されることを確認する。

- [ ] **Step 4: Unityバッチモードでコンパイル確認**

Task 1のStep 4と同じコマンドを再実行し、同じ合否基準で確認する。

- [ ] **Step 5: Commit**

```bash
git add Assets/VoidSurvival_10-Days/Scripts/Character/CharacterItemDropper.cs
git commit -m "fix: ドロップ抽選でbreakをcontinueに修正(候補ごとに独立判定)"
```

---

### Task 3: `FlagData` のセーブ/ロード実装

**Files:**
- Create: `Assets/VoidSurvival_10-Days/Scripts/SaveData/FlagSaveData.cs`
- Modify: `Assets/VoidSurvival_10-Days/Scripts/Data/FlagData.cs`
- Modify: `Assets/VoidSurvival_10-Days/Scripts/SaveData/SaveData.cs`
- Modify: `Assets/VoidSurvival_10-Days/Scripts/SaveManager.cs`

**Interfaces:**
- Produces: `FlagData.ToSaveData() -> FlagSaveData`,
  `FlagData.FromSaveData(FlagSaveData saveData) -> void`
- Consumes（既存): `FlagData.Instance`（既存のシングルトンアクセサ、変更しない）

**現状:** `FlagData.FromSaveData()`/`ToSaveData()` は引数も中身も空。`SaveData` クラスにも
フラグ用のフィールドが無く、`SaveManager` のセーブ/ロード処理からも一切呼ばれていない
（進行フラグがセーブ・ロードされない）。

- [ ] **Step 1: `FlagSaveData.cs` を新規作成**

`PlayerSaveData.cs` と同じ形式（`[System.Serializable] public struct`）で、
`FlagData` の4つのフィールドに対応する構造体を作る:

```csharp
[System.Serializable]
public struct FlagSaveData
{
    public bool CraftTutorial;
    public bool CraftWorkbench;
    public bool DestructionRock;
    public bool FindSomeone;
}
```

- [ ] **Step 2: `FlagData.cs` の `FromSaveData`/`ToSaveData` を実装**

```csharp
public FlagSaveData ToSaveData()
{
    return new FlagSaveData
    {
        CraftTutorial = craftTutorial,
        CraftWorkbench = craftWorkbench,
        DestructionRock = destructionRock,
        FindSomeone = findSomeone,
    };
}
public void FromSaveData(FlagSaveData saveData)
{
    craftTutorial = saveData.CraftTutorial;
    craftWorkbench = saveData.CraftWorkbench;
    destructionRock = saveData.DestructionRock;
    findSomeone = saveData.FindSomeone;
}
```

既存の既存フィールド名（`craftTutorial`等, 小文字開始）はそのまま使う。

- [ ] **Step 3: `SaveData.cs` にフィールドを追加**

既存の `public PlayerSaveData PlayerSaveData;`（型名=プロパティ名の慣習）に合わせて:

```csharp
public FlagSaveData FlagSaveData;
```

を `SaveData` クラスに追加する。

- [ ] **Step 4: `SaveManager.cs` のセーブ/ロード処理に接続**

`SaveGameAsync`内、既存の `_data.PlayerSaveData = _playerStatus.ToSaveData();` の
近くに追加:

```csharp
_data.FlagSaveData = FlagData.Instance.ToSaveData();
```

`LoadGameAsync`内、既存の `if (_data != null) { ... }` ブロック内、
`_playerStatus.FromSaveData(_data.PlayerSaveData);` の近くに追加:

```csharp
FlagData.Instance.FromSaveData(_data.FlagSaveData);
```

- [ ] **Step 5: 手動トレースで往復を確認する**

`FlagData.Instance.destructionRock = true` の状態で `ToSaveData()` を呼び、
返った `FlagSaveData.DestructionRock` が `true` であること。
その値を別の `FlagData` 相当の状態（`destructionRock = false`）に対して
`FromSaveData(savedData)` を呼び、`destructionRock` が `true` に戻ることを
コード上でトレースする。

- [ ] **Step 6: Unityバッチモードでコンパイル確認**

Task 1のStep 4と同じコマンド・同じ合否基準。

- [ ] **Step 7: Commit**

```bash
git add Assets/VoidSurvival_10-Days/Scripts/SaveData/FlagSaveData.cs \
        Assets/VoidSurvival_10-Days/Scripts/Data/FlagData.cs \
        Assets/VoidSurvival_10-Days/Scripts/SaveData/SaveData.cs \
        Assets/VoidSurvival_10-Days/Scripts/SaveManager.cs
git commit -m "feat: FlagData(進行フラグ)のセーブ/ロードを実装"
```

---

### Task 4: `FlagData.asset` をデフォルト状態にリセット

**Files:**
- Modify: `Assets/Resources/FlagData.asset`

**現状:** テスト実行後の状態 (`destructionRock: 1`, `findSomeone: 1`) がコミットされており、
新規プレイでイベントがスキップされる。

- [ ] **Step 1: `destructionRock` と `findSomeone` を `0` に戻す**

`Assets/Resources/FlagData.asset` 内:
```yaml
  craftTutorial: 0
  craftWorkbench: 0
  destructionRock: 0
  findSomeone: 0
```
（`craftTutorial`/`craftWorkbench` は既に `0` のため変更不要。
`destructionRock`/`findSomeone` の2行だけ `1` → `0` に変更する）

- [ ] **Step 2: Unityバッチモードでコンパイル確認**

Task 1のStep 4と同じコマンド・同じ合否基準（アセットのみの変更でもプロジェクトが
正常に開けることの確認を兼ねる）。

- [ ] **Step 3: Commit**

```bash
git add Assets/Resources/FlagData.asset
git commit -m "fix: FlagData.asset をデフォルト状態にリセット(テスト実行状態の混入を解消)"
```

---

## 完了後の報告事項（Codex → Claude Code）

- 各タスクのコンパイル確認ログ（Step 4/6/2 の出力）
- Task 3 Step 5 の手動トレース結果
- 作業中に見つかった、本プラン記載以外の問題（あれば報告のみ。修正は別タスクとする）
