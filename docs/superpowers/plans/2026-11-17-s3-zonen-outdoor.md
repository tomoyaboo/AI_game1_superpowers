# S3 外1回目(ZONE-N) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** カット割りS3「外1回目(ZONE-N)」のI〜M節(初めての屋外〜残骸/最初の武器)を、
既存の観測値HUD・SeuEffectController・FieldItemピックアップ機構に乗せて進行させる
新規シーケンスクラス `ZoneNIntroSequence` を実装し、`3_ZoneN_Demo.unity` に配置する。

**Architecture:** `TerminalIntroSequence`/`HubAIntroSequence` と同じ「単一MonoBehaviourが
コルーチンで台詞・UI表示・SEU強制発火・武器ピックアップ待ちを順に進める」パターンを踏襲する。
空間的な遭遇(ヒューマノイド機を実際に避ける)や瓦礫の衝突判定は既存のEnemySpawner/岩オブジェクトの
環境配置にまかせ、本シーケンスは台詞と時間経過で進行を表現する(既存2タスクと同じ簡略化方針)。
L節のSEUだけは「体験の核」のため実際に `SeuEffectController.ForceTrigger()` を呼び、
効果終了を実際に待ってから台詞を続ける(ここだけは本物のゲームプレイに同期させる)。

**Tech Stack:** Unity 2023.1.22f1, C#, NUnit (EditMode), 既存 MessageText / ObservedValuesHud / SeuEffectController / FieldItem。

**Spec:** `docs/VoidSurvival_カット割り_2.md` (S3節, 515〜597行)

## Global Constraints

- 元のオリジナルシーン(`Assets/Scenes/Outdoor/ZONE-N.unity`等)は一切変更しない。配置は `Assets/Scenes/Demo/3_ZoneN_Demo.unity` にのみ行う。
- `vendor/VoidSurvival_10-Days/.git` は存在しない(個人リポジトリ内で直接管理)。upstream `NakaAwjr/VoidSurvival_10-Days` には絶対にpush/マージしない。
- 新規C#ファイルは `VoidSurvival.Runtime.asmdef` の対象ディレクトリ(`Assets/VoidSurvival_10-Days/Scripts/`)に置く。
- 既存の `SeuEffectController`/`ObservedValuesHud`/`FieldItem`/`MessageText` のAPIは変更せず、イベント追加などの後方互換な拡張のみ行う。
- イラスト・新規アートは作れないため、武器ピックアップは既存 `Hammer.asset`(WeaponItem)をそのまま「鉄棒」の代替として使う。残骸・瓦礫は既存の `rock` オブジェクトの複製+色調変更(tint)で代用する。
- シーン編集はYAML手編集禁止。`-executeMethod` のEditor batchスクリプト(使用後削除)で行う。

## Review Focus

- `SeuEffectController.Instance` が既にActive状態のときに `ForceTrigger()` を呼んでも無視される(Normal状態のみ発火)仕様を踏まえ、シーケンス側がActive終了を正しく待てるか。
- `MessageText.Instance` や `SeuEffectController.Instance` がシーン読み込み直後に未初期化な場合のnullガード。
- 武器ピックアップ用 `FieldItem` が何らかの理由で既に破棄されている(null)状態でシーケンスが `WaitUntil` に入った場合に無限待機しないこと。
- シーン遷移(`SceneManager.LoadScene`)が二重に呼ばれない(コルーチンが一度しか最後まで到達しない)こと。
- SEU発火中にプレイヤーが既に `ShelterZone` 内にいた場合、`PlayerShelterState.IsSheltered` が真でも`ForceTrigger()`は仕様上無視しない(強制発火は遮蔽判定をバイパスする)ことを明示し、実装者が誤って遮蔽チェックを追加しないようにする。

---

### Task 1: `SeuEffectController` にActive状態変化イベントを追加

**Files:**
- Modify: `Assets/VoidSurvival_10-Days/Scripts/SeuEffectController.cs`

**Interfaces:**
- Consumes: 既存の `_phase` 状態遷移(`BeginActive`/`EndActive`)
- Produces: `public event System.Action<bool> OnActiveStateChanged`(true=Active開始、false=Active終了)、`public bool IsActive => _phase == SeuPhase.Active`。Task 2が両方を使う。

- [ ] **Step 1: `IsActive` プロパティと `OnActiveStateChanged` イベントを追加し、`BeginActive()`/`EndActive()` の末尾でそれぞれ `OnActiveStateChanged?.Invoke(true)` / `OnActiveStateChanged?.Invoke(false)` を呼ぶ**

既存の `BeginActive()`/`EndActive()` の処理は変更せず、末尾に1行追加するだけ。

- [ ] **Step 2: Unityバッチモードでコンパイルを確認**

Run: `Unity -batchmode -nographic -quit -projectPath vendor/VoidSurvival_10-Days -logFile -`
Expected: exit code 0、コンパイルエラーなし。

- [ ] **Step 3: Commit**

```bash
git add vendor/VoidSurvival_10-Days/Assets/VoidSurvival_10-Days/Scripts/SeuEffectController.cs
git commit -m "feat: SeuEffectControllerにActive状態変化イベントを追加"
```

---

### Task 2: `ZoneNIntroSequence` の実装(I〜M節の進行)

**Files:**
- Create: `Assets/VoidSurvival_10-Days/Scripts/Events/ZoneNIntroSequence.cs`

**Interfaces:**
- Consumes: `MessageText.Instance.TextMessage(List<string>)`, `ObservedValuesHud.Instance.SetObservedValuesVisible(bool)`(既存シグネチャ), `SeuEffectController.Instance.ForceTrigger()`/`.IsActive`/`.OnActiveStateChanged`, `SceneManager.LoadScene(string)`。
- Produces: `public class ZoneNIntroSequence : MonoBehaviour` with `[SerializeField] GameObject passageBlockedPanel`, `[SerializeField] TMP_Text passageBlockedText`, `[SerializeField] GameObject weaponFieldItem`(ピックアップ対象の `FieldItem` インスタンス), `[SerializeField] string nextSceneName = "2_HubA_Demo"`。Task 3がInspector側でこれらをワイヤリングする。

- [ ] **Step 1: `ZoneNIntroSequence.cs` を実装**

`TerminalIntroSequence`/`HubAIntroSequence` と同じ構造(台詞リストを `private static readonly List<string>` で保持し、`Start()` から `RunSequence()` コルーチンを起動)で以下を実装する:

1. I節: `MessageText` で I-3 の台詞を表示 → `ObservedValuesHud.Instance?.SetObservedValuesVisible(true)` → 4〜6秒待機 → 同じ台詞(I-5の劣化描写、文言はI-3と同一)をもう一度表示。
2. J節: J-2、J-3の台詞を表示(実際の遭遇判定は既存EnemySpawnerに委ね、本シーケンスは台詞のみ)。
3. K節: K-2の `> PASSAGE BLOCKED` 表示用に `passageBlockedPanel`/`passageBlockedText` をアクティブ化し、1.5秒待機後に非表示、続けてK-2台詞を表示。
4. L節: L-1(数値が先に動く、2秒待機のみ)→L-2台詞→`SeuEffectController.Instance?.ForceTrigger()` を呼ぶ→`SeuEffectController.Instance`がnullでなければ `yield return new WaitUntil(() => !SeuEffectController.Instance.IsActive)` でActive終了を待つ(nullなら代わりに `activeDuration` 相当の固定12秒待機にフォールバック)→L-4の `WATCHDOG RESET / CONTROL RESTORED` をpassageBlockedPanel/Text経由で短く表示→L-5、L-6台詞。
5. M節: M-2台詞 → `weaponFieldItem` がnullでなければ `yield return new WaitUntil(() => weaponFieldItem == null)` でピックアップ(FieldItemは既存仕様で接触時に自己破棄)を待つ。nullなら待機をスキップ → M-4台詞 → M-5のログ文面(`UNIT 05 / LOG 1104`/`1106`、本文はカット割り記載のまま)を `MessageText` で表示 → M-6相当として `SceneManager.LoadScene(nextSceneName)`。

各節の待機秒数はカット割りに明記の秒数(未記載箇所は2〜3秒の適当な固定値)を使う。`MessageText.Instance`が見つからない場合は `Debug.LogError` を出して該当台詞をスキップする(既存2クラスと同じフォールバック方針)。

- [ ] **Step 2: Unityバッチモードでコンパイルを確認**

Run: `Unity -batchmode -nographic -quit -projectPath vendor/VoidSurvival_10-Days -logFile -`
Expected: exit code 0。

- [ ] **Step 3: Commit**

```bash
git add vendor/VoidSurvival_10-Days/Assets/VoidSurvival_10-Days/Scripts/Events/ZoneNIntroSequence.cs
git commit -m "feat: ZoneNIntroSequence(S3 外1回目 I〜M)を実装"
```

---

## Non-Code Follow-up (このプランのスコープ外、Claude側で後続実施)

- `3_ZoneN_Demo.unity` へ `ZoneNIntroSequence` を配置し、`passageBlockedPanel`/`Text`(新規UI)、`weaponFieldItem`(既存 `FieldItem.prefab` + `Hammer.asset` を瓦礫/残骸付近に配置し色調変更)をInspectorでワイヤリングするEditor batchスクリプトは、本プランの実装後にClaude側で作成・実行し、使用後削除する(確立済みパターン)。
