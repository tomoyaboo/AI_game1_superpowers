# Unity Test Framework 導入 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **この計画はCodex CLI（`codex exec`）が実行する。** Claude Code（オーケストレーター）が
> タスクごとに本ファイルを渡し、Codexが実装、Claude Codeが差分レビューとコンパイル/テスト
> 実行結果の確認を行う。

**Goal:** `Assets/VoidSurvival_10-Days/Scripts/` を独自の asmdef に切り出し、Unity Test
Framework（EditMode）で実際にテストを実行できる状態にする。既存の動作は一切変更しない
（純粋な構造変更 + テスト追加のみ）。

**Architecture:** 現在すべてのゲームコードは無印の `Assembly-CSharp` に属している。
`Assembly-CSharp` は他の asmdef から参照できない（Unityのビルド順序の制約）ため、
テストから参照可能にするには Scripts フォルダ自体を asmdef に切り出す必要がある。
調査の結果、以下の構成にする:

- `CrossPlatformInput.asmdef` — `Assets/Standard Assets/CrossPlatformInput/Scripts/`
  （既存asmdefが無く、`PlayerController`/`SceneMover` から参照されているため先に切り出す）
- `VoidSurvival.Editor.asmdef` — `Assets/VoidSurvival_10-Days/Scripts/EditorExtension/`
  （`UnityEditor` 名前空間を使うのはこの4ファイルのみ。Editor専用として分離する）
- `VoidSurvival.Runtime.asmdef` — `Assets/VoidSurvival_10-Days/Scripts/` の残り全部
  （ゲーム本体のコード。既存の挙動・公開APIは一切変更しない）
- `VoidSurvival.Tests.EditMode.asmdef` — `Assets/VoidSurvival_10-Days/Tests/EditMode/`
  （`VoidSurvival.Runtime` を参照するテスト専用アセンブリ）

**Tech Stack:** Unity 2023.1.22f1, C#, Unity Test Framework（`com.unity.test-framework`,
既にpackageとして導入済み）, NUnit

**Spec:** `docs/superpowers/specs/2026-11-17-voidsurvival-ai-collab-design.md`
（§5 タスク#2に対応）。前提資料: `docs/superpowers/plans/2026-11-17-known-bugs-and-flagdata-persistence.md`
（Task 1で修正した `ItemManager`/`FlagData` に対する最初のテストをここで書く）

## Global Constraints

- 作業ディレクトリは `vendor/VoidSurvival_10-Days/`
- upstream（`NakaAwjr/VoidSurvival_10-Days`）への push・merge は行わない
  （`.git` が存在しないため技術的に不可）
- **既存の挙動・公開API・既存ファイルのコード内容は変更しない。** 本タスクは
  「asmdefによる構造の整理」と「テストの追加」のみ。バグ修正や仕様変更は含まない
- Unityバッチモードの実行コマンド（コンパイル確認用）:
  ```bash
  /Applications/Unity/Hub/Editor/2023.1.22f1/Unity.app/Contents/MacOS/Unity \
    -batchmode -nographic -quit \
    -projectPath "$(pwd)" \
    -logFile "$(pwd)/../../.codex-logs/task2-compile.log"
  ```
  Expected: 終了コード `0`、ログに `error CS` を含む行が無いこと
- Unityバッチモードの実行コマンド（EditModeテスト実行用）:
  ```bash
  /Applications/Unity/Hub/Editor/2023.1.22f1/Unity.app/Contents/MacOS/Unity \
    -batchmode -nographic \
    -projectPath "$(pwd)" \
    -runTests -testPlatform EditMode \
    -testResults "$(pwd)/../../.codex-logs/task2-test-results.xml" \
    -logFile "$(pwd)/../../.codex-logs/task2-test-run.log"
  ```
  Expected: `task2-test-results.xml` に `result="Passed"` の `test-run` 要素があり、
  `total="2" passed="2" failed="0"`（本プランで追加する2件のテストのみの場合）
- asmdefのJSON中の `references` は、ここに書いた名前で**まず試す**。コンパイルエラーで
  型が見つからない場合は、エラーメッセージが示す型のアセンブリ名を
  `Library/PackageCache/` 以下の `.asmdef` から特定して追加してよい
  （これは計画からの逸脱ではなく、想定された調整。何を追加したかは報告すること）

## Review Focus

- **`VoidSurvival.Runtime` が `CrossPlatformInput`/`VoidSurvival.Editor` を
  正しい方向で参照しているか**: Runtime→CrossPlatformInput は参照するが、
  Runtime→Editor は参照しない（Editor側がRuntimeを参照する方向が正しい）
- **既存スクリプトのファイル内容が変更されていないこと**: このタスクで変更が許されるのは
  asmdefファイルの新規作成・配置と、Testsフォルダの新規ファイルのみ
- **EditorExtension配下の4ファイルが実際にPlayerビルドから除外される設定になっているか**:
  `VoidSurvival.Editor.asmdef` の `includePlatforms` が `["Editor"]` になっていること
- **テストが既存のシングルトン状態(`ItemManager.Instance`等)を汚染していないか**:
  テスト対象のアイテムは毎回 `ScriptableObject.CreateInstance` で作った、
  既存インベントリに絶対に存在しないインスタンスを使うこと
- **コンパイル確認とテスト実行の両方を実際に行い、ログを確認していること**
  （「たぶん通る」ではなく、実行結果を見て判断する）

---

### Task 1: `CrossPlatformInput.asmdef` の作成

**Files:**
- Create: `Assets/Standard Assets/CrossPlatformInput/Scripts/CrossPlatformInput.asmdef`

**Interfaces:**
- Produces: アセンブリ名 `CrossPlatformInput`（Task 3で参照する）

- [ ] **Step 1: asmdefファイルを作成**

`Assets/Standard Assets/CrossPlatformInput/Scripts/CrossPlatformInput.asmdef`:

```json
{
    "name": "CrossPlatformInput",
    "rootNamespace": "",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

このフォルダ以下（`PlatformSpecific/` サブフォルダ含む）の既存 `.cs` ファイルは
一切変更しない。このasmdefファイルを追加するだけで、フォルダ内の全スクリプトが
このアセンブリに属するようになる。

- [ ] **Step 2: Unityバッチモードでコンパイル確認**

Global Constraints記載のコンパイル確認コマンドを実行。
Expected: 終了コード `0`、`error CS` が0件。

- [ ] **Step 3: Commit**

```bash
git add "Assets/Standard Assets/CrossPlatformInput/Scripts/CrossPlatformInput.asmdef"
git commit -m "chore: CrossPlatformInput スクリプトをasmdefに切り出し"
```

---

### Task 2: `VoidSurvival.Editor.asmdef` の作成

**Files:**
- Create: `Assets/VoidSurvival_10-Days/Scripts/EditorExtension/VoidSurvival.Editor.asmdef`

**Interfaces:**
- Consumes: `VoidSurvival.Runtime`（Task 3で作られる。このタスクの時点ではまだ存在しないため、
  Task 3完了後に本taskのasmdefへ参照を追加する必要がある。下記Step 1はその前提で書く）

**現状:** `IntKeyDatabaseEditor.cs`, `WorkbenchRecipeDataBaseEditor.cs`,
`CraftingRecipeDatabaseEditor.cs`, `ItemDatabaseEditor .cs`（ファイル名にスペースあり、
既存のまま変更しない）の4ファイルが `UnityEditor` 名前空間を使っている。
現状は無印の `Assembly-CSharp` に混在しており、Player ビルドでは本来コンパイルできない
（`UnityEditor` はEditorのみで利用可能）。これを独立したEditor専用asmdefに分離する。

- [ ] **Step 1: asmdefファイルを作成**

`Assets/VoidSurvival_10-Days/Scripts/EditorExtension/VoidSurvival.Editor.asmdef`:

```json
{
    "name": "VoidSurvival.Editor",
    "rootNamespace": "",
    "references": [
        "VoidSurvival.Runtime"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

（`VoidSurvival.Runtime` への参照はTask 3で作成される。**Task 3完了後に、このタスクの
コンパイル確認を再実行して問題ないことを確認すること**。Task 2単独の時点では
`VoidSurvival.Runtime` が無いためコンパイルエラーになるのは想定内。）

- [ ] **Step 2: Commit（コンパイル確認はTask 3完了後に一括で行う）**

```bash
git add "Assets/VoidSurvival_10-Days/Scripts/EditorExtension/VoidSurvival.Editor.asmdef"
git commit -m "chore: EditorExtension配下をEditor専用asmdefに分離"
```

---

### Task 3: `VoidSurvival.Runtime.asmdef` の作成

**Files:**
- Create: `Assets/VoidSurvival_10-Days/Scripts/VoidSurvival.Runtime.asmdef`

**Interfaces:**
- Produces: アセンブリ名 `VoidSurvival.Runtime`（Task 2, Task 4が参照する）
- Consumes: `CrossPlatformInput`（Task 1で作成済み）

**現状:** `Assets/VoidSurvival_10-Days/Scripts/` 配下（`EditorExtension/` を除く）が
使用している外部依存は以下の通り（調査済み）:

| 外部依存 | 使用ファイル(代表) | 参照asmdef名 |
|---|---|---|
| `Cinemachine` | `InitialCamera.cs` | `Cinemachine` |
| `Cysharp.Threading.Tasks`(UniTask) | `SaveManager.cs`, `SceneMover.cs` | `UniTask` |
| `TMPro` | `UI/` 配下多数 | `Unity.TextMeshPro` |
| `Unity.VisualScripting` | `WorkbenchManager.cs` 等 | `Unity.VisualScripting.Core` |
| `PlayFab`, `PlayFab.ClientModels` | `PlayfabTest/` 配下 | `PlayFab` |
| `UnityStandardAssets.CrossPlatformInput` | `PlayerController.cs`, `SceneMover.cs` | `CrossPlatformInput`(Task 1) |
| `NUnit.Framework` | `UI/Communication/CommunicationDialog.cs` | 下記Step 2で対応 |
| `DG.Tweening`(DOTween) | `FieldItem.cs`, `UI/SceneLording.cs` | `DOTween.dll` は precompiled plugin のため通常は参照不要(自動的に見える想定) |
| `Newtonsoft.Json` | `PlayfabTest/` 配下 | 通常は自動参照される想定。エラーが出た場合のみ追加 |

- [ ] **Step 1: asmdefファイルを作成**

`Assets/VoidSurvival_10-Days/Scripts/VoidSurvival.Runtime.asmdef`:

```json
{
    "name": "VoidSurvival.Runtime",
    "rootNamespace": "",
    "references": [
        "PlayFab",
        "UniTask",
        "Cinemachine",
        "Unity.TextMeshPro",
        "Unity.VisualScripting.Core",
        "CrossPlatformInput"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

`includePlatforms`/`excludePlatforms` は両方空配列にすること（ゲーム本体はEditor上でも
実機上でも動く必要があるため、プラットフォームを限定しない）。

このasmdefファイルは `EditorExtension/` フォルダより上の階層（`Scripts/` 直下）に置く。
Unityのasmdef適用範囲はフォルダ階層で決まり、サブフォルダに別のasmdef
（`EditorExtension/VoidSurvival.Editor.asmdef`）があれば、そのサブフォルダだけは
`VoidSurvival.Editor` に属し `VoidSurvival.Runtime` には含まれない（Unityの標準挙動）。

- [ ] **Step 2: コンパイル確認し、エラーを解消する**

Global Constraints記載のコンパイル確認コマンドを実行。

**`NUnit.Framework` が見つからないエラーが出た場合**: `VoidSurvival.Runtime.asmdef` の
`precompiledReferences` に `"nunit.framework.dll"` を追加し、`overrideReferences` を
`true` にする。

**他の型が見つからないエラーが出た場合**: エラーメッセージの型名を
`Library/PackageCache/**/*.asmdef` の中から `grep` 等で検索し、該当する
アセンブリ名を `references` に追加する。追加したら再度コンパイル確認を実行する
（このステップは収束するまで繰り返す）。

Expected: 最終的に終了コード `0`、`error CS` が0件になること。

- [ ] **Step 3: Task 2のEditor asmdefも含めて再コンパイル確認**

Task 2で作った `VoidSurvival.Editor.asmdef` が `VoidSurvival.Runtime` を正しく
参照できるようになっているはずなので、同じコンパイル確認コマンドを再実行し、
プロジェクト全体（Editor含む）でエラーが0件であることを確認する。

- [ ] **Step 4: Commit**

```bash
git add "Assets/VoidSurvival_10-Days/Scripts/VoidSurvival.Runtime.asmdef"
git commit -m "chore: VoidSurvival_10-Days/Scripts を VoidSurvival.Runtime asmdef に切り出し"
```

Task 2で追加した `precompiledReferences` 等の調整があれば、それも合わせてコミットする
（Task 2のコミットをamendするか、別コミットにするかはCodexの判断でよい）。

---

### Task 4: EditModeテストアセンブリと最初のテストの作成

**Files:**
- Create: `Assets/VoidSurvival_10-Days/Tests/EditMode/VoidSurvival.Tests.EditMode.asmdef`
- Create: `Assets/VoidSurvival_10-Days/Tests/EditMode/ItemManagerTests.cs`
- Create: `Assets/VoidSurvival_10-Days/Tests/EditMode/FlagDataTests.cs`

**Interfaces:**
- Consumes: `VoidSurvival.Runtime`（`ItemManager`, `Item`, `MaterialItem`, `FlagData`,
  `FlagSaveData` 型を使う）

- [ ] **Step 1: テストasmdefを作成**

`Assets/VoidSurvival_10-Days/Tests/EditMode/VoidSurvival.Tests.EditMode.asmdef`:

```json
{
    "name": "VoidSurvival.Tests.EditMode",
    "rootNamespace": "",
    "references": [
        "VoidSurvival.Runtime",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": true,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: `ItemManagerTests.cs` を作成（Task 1の回帰テスト）**

```csharp
using NUnit.Framework;
using UnityEngine;

public class ItemManagerTests
{
    [Test]
    public void RemoveItem_ItemNotInInventory_DoesNotThrow()
    {
        var item = ScriptableObject.CreateInstance<MaterialItem>();
        item.ItemID = int.MinValue; // 既存インベントリと衝突しないID

        Assert.DoesNotThrow(() => ItemManager.Instance.RemoveItem(item, 1));

        Object.DestroyImmediate(item);
    }
}
```

この回帰テストは、`docs/superpowers/plans/2026-11-17-known-bugs-and-flagdata-persistence.md`
Task 1で修正した `ItemManager.RemoveItem` の null チェック漏れ
（`existingStack.Item != null` → `existingStack != null`）を固定するためのもの。

- [ ] **Step 3: `FlagDataTests.cs` を作成（Task 1の回帰テスト）**

```csharp
using NUnit.Framework;
using UnityEngine;

public class FlagDataTests
{
    [Test]
    public void ToSaveData_FromSaveData_RoundTrips()
    {
        var flagData = ScriptableObject.CreateInstance<FlagData>();
        flagData.destructionRock = true;
        flagData.findSomeone = false;
        flagData.craftTutorial = true;
        flagData.craftWorkbench = false;

        var saved = flagData.ToSaveData();

        var restored = ScriptableObject.CreateInstance<FlagData>();
        restored.FromSaveData(saved);

        Assert.IsTrue(restored.destructionRock);
        Assert.IsFalse(restored.findSomeone);
        Assert.IsTrue(restored.craftTutorial);
        Assert.IsFalse(restored.craftWorkbench);

        Object.DestroyImmediate(flagData);
        Object.DestroyImmediate(restored);
    }
}
```

この回帰テストは、同計画書Task 3で実装した `FlagData.ToSaveData`/`FromSaveData`
の往復を固定するためのもの。

- [ ] **Step 4: Unityバッチモードでテストを実行**

Global Constraints記載のテスト実行コマンドを実行。

Expected: `task2-test-results.xml` に `total="2" passed="2" failed="0"`。
1件でも失敗した場合は、`superpowers:systematic-debugging` に従って原因を特定し、
テストコードまたは参照設定(asmdef)を直す。既存の本体コード
（`ItemManager.cs`, `FlagData.cs` 等）は変更しないこと
（失敗の原因が本体コードのバグに見える場合は、修正せず報告のみ）。

- [ ] **Step 5: Commit**

```bash
git add Assets/VoidSurvival_10-Days/Tests/
git commit -m "test: EditModeテストアセンブリを追加し、ItemManager/FlagDataの回帰テストを実装"
```

---

## 完了後の報告事項（Codex → Claude Code）

- 各タスクのコンパイル確認ログ・テスト実行結果（`testResults.xml` の中身または要約）
- Global Constraintsで許可された「参照の追加」を行った場合、どの型のために何を追加したか
- `nunit.framework.dll` が最終的に `VoidSurvival.Runtime.asmdef` に必要だったか不要だったか
  （`CommunicationDialog.cs` のコンパイル結果で判断できる）
- 作業中に見つかった、本プラン記載以外の問題（あれば報告のみ）
