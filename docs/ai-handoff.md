# AI 協調開発ハンドオフ: プレイヤー移動機能（Void Survival: 10 Days）

- 作成日: 2026-11-17
- 作成: Claude Code（設計担当）
- 実装担当: Codex
- ステータス: 設計完了 / 未実装（本ドキュメントの作成のみ。コード変更・コミットは行っていない）
- 対象リポジトリ: `NakaAwjr/VoidSurvival_10-Days`（Unity 2023.1.22f1, `develop` ブランチ, 2026-11-17 時点）
- 対象ファイル:
  - `Assets/VoidSurvival_10-Days/Scripts/Character/PlayerController.cs`
  - `Assets/VoidSurvival_10-Days/Scripts/Character/PlayerStatus.cs`
  - `Assets/VoidSurvival_10-Days/Scripts/Character/CharacterStatus.cs`
  - `Assets/VoidSurvival_10-Days/Scripts/Character/CharacterRenderer.cs` / `PlayerRenderer.cs`
  - `Assets/VoidSurvival_10-Days/Scripts/Character/CharacterAttack.cs`
  - `Assets/VoidSurvival_10-Days/Scripts/DashButton.cs`

このドキュメントは「Claude が既存実装を調査・仕様化し、Codex が改修・テスト実装する」協調開発の検証用ハンドオフです。
参照した仕様書は `docs/VoidSurvival_DemoSpec_v0.3.md`（§5.6, §6.3, §11）と `docs/VoidSurvival_カット割り_2.md`（J-3）。

---

## 0. 前提の訂正（申し送り）

当初はこのリポジトリに実コードが無い前提で、汎用的な移動機能の仕様書を書いていた。
その後 `docs/` に実際の企画書が置かれ、本体コード（`NakaAwjr/VoidSurvival_10-Days`, public）が
GitHub 上に存在することが分かったため、**実装を直接読み、現状の挙動をそのまま仕様として書き起こす**方針に変更した。
以降の章は、新機能の提案ではなく **現状動作の正確な記述** と **見つかった課題への対応方針** である。

---

## 1. 目的とスコープ

### 目的
既存のプレイヤー移動実装（`PlayerController` 系）の挙動を正確に文書化し、Codex が
安全に改修・テスト追加できる状態にする。特に、デモ版仕様書 §6.3 の **SEU（操作反転）** が
フック済みだが未接続であること、ダッシュが PC キーボードから使えないことなど、
企画と実装のギャップを明確にする。

### スコープに含む
- 現状の移動・ダッシュ・スタミナ・向き決定・状態遷移ロジックの仕様化
- 見つかった不具合・未接続箇所の修正方針
- Unity Test Framework（EditMode / PlayMode）によるテスト項目の設計

### スコープに含まない（今回は実装しない）
- SEU（ビット反転）演出自体の新規実装（§4.5 に申し送りのみ記載。フックは既存）
- 衛星データ連動（電圧→加工速度 等。デモ仕様書 §5.6 の別タスク）
- 攻撃・インベントリ・施設系のロジック変更
- 実際のコード修正・コミット・push（本ハンドオフ作成のみ）

---

## 2. 現状仕様（既存実装のリバースエンジニアリング）

### 2.1 入力

- `PlayerController.Update()` が毎フレーム `CrossPlatformInputManager.GetAxis("Horizontal")` /
  `"Vertical"` を読む。
- `CrossPlatformInputManager` は実行環境によって実体が切り替わる抽象層：
  - PC（Standalone/Editor）: `StandaloneInput` → Unity 標準 Input Manager の `Horizontal` /
    `Vertical` 軸＝既定では **矢印キー と A/D, W/S**。
  - モバイル: `MovableJoystick`（画面上の仮想ジョイスティック）が同じ軸名に値を書き込む。
- **ダッシュに対応するキーボード入力は存在しない。** `DashButton`（`IPointerDownHandler` /
  `IPointerUpHandler`）はマウス/タッチでのクリックのみを受け付け、静的フラグ
  `DashButton.IsDashing` をクリックごとに**トグル**する（押している間ではない）。

### 2.2 速度計算（`PlayerController.Update()`）

1. `_moveInput = (Horizontal軸, Vertical軸)`
2. `PlayerStatus.IsInversion` が true なら `_moveInput.x`, `_moveInput.y` をそれぞれ符号反転
3. `_moveInput.Normalize()` — 斜め入力でも単位ベクトル化され、斜めが速くならない
4. `_moveSpeed = moveSpeed`（Inspector 既定値 5）。
   `_moveInput.magnitude > 0 && DashButton.IsDashing && _status.IsDashing` が全て真なら
   `_moveSpeed = moveSpeed * 2` とし、同時に `_status.ConsumeStamina(1)` を呼ぶ
5. `_status.IsMovable` が true のときだけ、`_renderer.SetDirection(_moveInput)`
   （アニメーション用）と `_rigidbody.velocity = _moveInput * _moveSpeed` を実行

**不変条件（現状の意図）**: `_status.IsMovable` が false の間は `rigidbody.velocity` は
このメソッドからは更新されない（直前のフレームの velocity が残るため、**慣性で動き続ける**。
これは意図的な停止処理ではなく「何もしない」であることに注意、§3.1 参照）。

### 2.3 状態と移動可否（`CharacterStatus` / `PlayerStatus`）

状態は `Idle` / `Active` / `Fatigue` / `Dead` の4種（`PlayerStatus` のみ `Fatigue` を使う）。

| 状態 | `IsMovable`（Player） | `IsDashing` | 遷移元/遷移先 |
|---|---|---|---|
| `Idle` | ○ | ○ | 既定状態 |
| `Active` | ×（移動不可） | × | `GoToActiveStateIfPossible()` で遷移（攻撃中など） |
| `Fatigue` | ○（移動は可、ダッシュ不可） | × | スタミナ0到達で `GoToFatigueStateIfPossible()` |
| `Dead` | × | × | HP0到達、以後復帰しない |

- `PlayerStatus.IsMovable` は基底クラスを override し `Idle || Fatigue` を返す
  （基底 `CharacterStatus.IsMovable` は `Idle` のみ）。
- つまり**攻撃アニメーション中（Active）は移動できない**。攻撃終了は
  `CharacterAttack.OnAttackFinished()` → `attackInterval` 秒後に `GoToIdleStateIfPossible()`。

### 2.4 ダッシュとスタミナ（`PlayerStatus`）

- ダッシュ条件: `DashButton.IsDashing`（UIトグル） かつ `PlayerStatus.IsDashing`（状態が Idle）
  かつ 移動入力あり。
- ダッシュ中は **1フレームごとに `ConsumeStamina(1)`** を呼ぶ。
  → **フレームレートに依存してスタミナ消費速度が変わる**（60fps と 30fps で消費速度が2倍差）。
- スタミナ回復は `TimeManager.OnMinuteChanged` イベント（ゲーム内時間が1分進むたび）に
  `RecoverStamina(StaminaHealSpeed)` を呼ぶ。**実時間ではなくゲーム内時間**に連動。
- スタミナが0になると `Fatigue` へ遷移。`Fatigue` 中は移動可能だがダッシュ不可。
  スタミナが回復して0より大きくなると `Idle` に戻る。

### 2.5 向き・アニメーション（`CharacterRenderer.SetDirection`）

- 入力ベクトルの大きさが `0.01` 未満なら Idle アニメーション（直前の向きを保持）。
- それ以外は次のルールで3方向＋ミラーに分類（斜め入力は必ずどちらかの軸に吸収される）：
  - `|y| > |x|` → 北（`y>0`）または南（`y<=0`）
  - それ以外（`|x| >= |y|`、x=y=0 を除く） → 「横」扱いで `Run E` アニメーションを再生し、
    `transform.localScale.x = Mathf.Sign(direction.x)` で東西を反転表現
- **真の斜めアニメーションは存在しない。** 常に縦優先・横劣後（同値のときは横扱い）。

### 2.6 攻撃との連動（`CharacterAttack`）

- 攻撃判定ボックスの位置・サイズは `CharacterRenderer.lastDirection`（0=北,1=南,2=横）で
  3パターンに切り替わる。横方向は `transform.localScale.x` のミラーにより東西どちらでも
  同じ `westOffset`/`westSize` がそのまま正しい側に反映される（ローカル座標がミラーされるため）。

### 2.7 入力反転フック（SEU, デモ仕様書 §6.3 対応）

- `PlayerStatus.IsInversion`（`public bool`, 既定 `false`）が唯一のインターフェース。
- **現状、このフラグを `true` にするコードは実装全体に存在しない。**
  デモ仕様書 §6.3 の「SAA通過中 + 屋外 + 放射線が強い → 操作の左右が反転」は
  **フックのみ用意されており、発生条件判定・持続時間・クールダウンの実装は未着手**。

---

## 3. 現状実装で見つかった課題

優先度は「このデモの目的（§12.1: 衛星データが効いていることの証明）に対する影響」で付けた。

| # | 課題 | 内容 | 優先度 |
|---|---|---|---|
| C-1 | スタミナ消費がフレームレート依存 | `ConsumeStamina(1)` を `Update()` で毎フレーム呼ぶため、
60fps 環境は 30fps 環境の2倍の速度でスタミナを消費する。実機のフレームレートが揺れると
体感難易度が変わる | 高 |
| C-2 | PC キーボードにダッシュの割り当てがない | `DashButton` は UI クリック/タップ専用。
マウスでクリックすれば PC でも機能はするが、**キーボード操作中にダッシュを使う手段がない**。
デモで配布する環境（展示会場・PC操作想定）次第で体験に直結する | 高 |
| C-3 | `IsInversion` が未接続 | SEU 演出のフックはあるが発生源が無く、デモ仕様書 §6.3 の
目玉現象の1つが実質未実装 | 中（別タスクとして扱うが移動側の対応は要る） |
| C-4 | `Active` 状態中は velocity が放置される | 攻撃などで `IsMovable=false` になった瞬間の
`rigidbody.velocity` がそのまま残り、移動していた慣性で**攻撃中にわずかに流れる**
（意図的な急停止処理が無い） | 低〜中 |
| C-5 | 斜め入力時にアニメーションが斜めにならない | 操作感としては許容範囲だが、
入力が8方向相当でも見た目は常に4方向止まり | 低（仕様として維持でも可） |
| C-6 | 自動テストが存在しない | `com.unity.test-framework` は依存に入っているが、
`Tests` アセンブリや EditMode/PlayMode テストが1つも無い。`Test/` 以下は
手動デバッグ用スクリプト（`TestCraft.cs` 等）で代替にならない | 高（今回の本題） |

---

## 4. 実装方針（Codex 向け）

### 4.1 設計原則

- **先にテストを増やし、挙動を固定してから直す。** 現状の `PlayerController.Update()` は
  入力取得・状態参照・Rigidbody操作が1メソッドに同居しており、Unity を起動しないと
  何も検証できない。最初のタスクは**計算部分を純粋関数として切り出す**こと。
- 切り出した関数のシグネチャ例（提案。最終的な型・名前は Codex の判断に委ねる）：

```csharp
// 既存 PlayerController 内に実装イメージ。MonoBehaviour に依存させない。
public static Vector2 CalculateVelocity(
    Vector2 rawInput,     // Horizontal/Vertical 軸の生値
    bool isInverted,      // PlayerStatus.IsInversion
    bool isDashRequested,       // DashButton.IsDashing && PlayerStatus.IsDashing
    float baseSpeed,
    float dashMultiplier = 2f)
{
    var input = isInverted ? -rawInput : rawInput;
    input = input.normalized; // 0ベクトルは normalized でも0のまま
    var speed = (input.sqrMagnitude > 0f && isDashRequested) ? baseSpeed * dashMultiplier : baseSpeed;
    return input * speed;
}
```

  `Update()` 側は、この関数の戻り値を `rigidbody.velocity` に代入するだけにする。

### 4.2 課題ごとの対応方針

| 課題 | 対応方針 |
|---|---|
| C-1（スタミナのフレーム依存） | `ConsumeStamina` の呼び出しを `Time.deltaTime` ベースの
消費レート（例: 毎秒 N 消費）に変更する。`ConsumeStamina(int amount)` の意味を
「1回の呼び出しで amount 消費」から「秒間 amount 消費」に変える場合、呼び出し側を
`ConsumeStamina(staminaDrainPerSecond * Time.deltaTime)` 形に直し、内部の `int` 蓄積を
`float` に変更する必要がある（保存データ `PlayerSaveData` への影響を確認すること） |
| C-2（PCダッシュ未割り当て） | 新しい入力ボタン名（例 `"Dash"`）を Unity の Input Manager に
追加し、`Input.GetButton("Dash")`（または `CrossPlatformInputManager` 経由）を
`DashButton.IsDashing` と同じ場所で OR 条件として扱う。`DashButton` 側の「クリックでトグル」
という UX（押しっぱなしではない）との整合を企画に確認する（§5 の要検討事項 Q-02） |
| C-3（IsInversion未接続） | 本タスクのスコープ外。別途 SEU 発生源コンポーネント
（例 `SeuEffectController`）を追加し、`PlayerStatus.IsInversion` を書き換える形にする。
移動側の変更は不要（既存フックをそのまま使える）という点だけ明記しておく |
| C-4（Active中の慣性） | `IsMovable` が false になった最初のフレームで
`rigidbody.velocity = Vector2.zero` を1度だけ設定する分岐を追加する |
| C-5（斜めアニメーション） | 今回は対応しない。企画判断が必要なため §6 の未決事項に残す |
| C-6（テスト不在） | `Assets/VoidSurvival_10-Days/Tests/` に EditMode 用 asmdef を追加し、
§5 のテスト項目を実装する |

### 4.3 実装順序（推奨）

1. `PlayerController` から移動計算を純粋関数（または `MoveInputResolver` のような
   非 MonoBehaviour クラス）に切り出す（挙動は変えない。リファクタのみ）
2. 切り出した関数に対する EditMode テスト（§5.1〜5.4）を書き、全てパスすることを確認
3. C-1（スタミナのフレーム依存）を `Time.deltaTime` ベースに修正し、該当テストを追加
4. C-4（Active中の慣性ゼロ化）を追加し、テストを追加
5. C-2（PCダッシュ割り当て）は企画確認（§6 Q-02）後に着手
6. PlayMode テスト（§5.6）でシーン上の実機挙動を確認

### 4.4 やらないこと（実装時の注意）

- `moveSpeed` や `dashMultiplier` などの既存パラメータの既定値は変更しない
  （ゲームバランスは企画判断。変える場合は提案として分けて出す）
- `PlayerSaveData` のフィールド構成を変えない。スタミナの型を変える場合は
  セーブデータの後方互換性（既存セーブの読み込み）を確認する
- `CrossPlatformInputManager` 自体（サードパーティ資産）は変更しない

---

## 5. テスト項目

Unity Test Framework（`com.unity.test-framework`、既に依存に含まれている）を使う。
`Assets/VoidSurvival_10-Days/Tests/EditMode/` と `.../PlayMode/` を新設する想定。

### 5.1 速度計算（EditMode, `CalculateVelocity` 等の純粋関数が対象）

| # | 入力 | 期待結果 |
|---|---|---|
| T-01 | 入力 `(0,0)` | 速度 `(0,0)` |
| T-02 | 入力 `(1,0)`, 反転なし, ダッシュなし | 速度 `(moveSpeed, 0)` |
| T-03 | 入力 `(1,1)`, 反転なし | 速度の大きさが `moveSpeed` に等しい（正規化されている） |
| T-04 | 入力 `(1,0)`, `isInverted=true` | 速度 `(-moveSpeed, 0)` |
| T-05 | 入力 `(0,1)`, `isInverted=true` | 速度 `(0, -moveSpeed)` |
| T-06 | 入力 `(1,0)`, `isDashRequested=true` | 速度の大きさが `moveSpeed * dashMultiplier` |
| T-07 | 入力 `(0,0)`, `isDashRequested=true` | 速度 `(0,0)`（入力が無いならダッシュでも動かない） |
| T-08 | 入力 `(1,0)`, `isInverted=true` かつ `isDashRequested=true` | 速度 `(-moveSpeed*dashMultiplier, 0)`（反転とダッシュは独立に効く） |

### 5.2 状態と移動可否（EditMode, `PlayerStatus` / `CharacterStatus`）

| # | 条件 | 期待結果 |
|---|---|---|
| T-09 | 初期状態 | `IsMovable == true`, `IsDashing == true` |
| T-10 | `GoToActiveStateIfPossible()` 実行後 | `IsMovable == false`, `IsDashing == false` |
| T-11 | スタミナを0まで消費 | 状態が `Fatigue` になる（`IsMovable == true`, `IsDashing == false`） |
| T-12 | `Fatigue` 中に `RecoverStamina` でスタミナ>0 | 状態が `Idle` に戻る |
| T-13 | HPを0まで減らした後 | `IsMovable == false` のまま、`RecoverStamina` / 遷移メソッドを呼んでも `Idle` に戻らない（Dead固定） |

### 5.3 スタミナ消費（修正後, EditMode）

| # | 条件 | 期待結果 |
|---|---|---|
| T-14 | `Time.deltaTime=1/60` を60回ダッシュ消費 | 1秒ぶんの消費量が `Time.deltaTime=1/30` を30回消費した場合と一致する（フレームレート非依存） |
| T-15 | ダッシュ中にスタミナがちょうど0になるフレーム | その場でダッシュ不可（`IsDashing==false`）に切り替わり、移動速度は通常速度に落ちる |

### 5.4 向き判定（EditMode, `CharacterRenderer.DirectionToIndex` 相当）

| # | 入力ベクトル | 期待結果 |
|---|---|---|
| T-16 | `(0, 1)` | 北 |
| T-17 | `(0, -1)` | 南 |
| T-18 | `(1, 0)` | 横（`localScale.x > 0`） |
| T-19 | `(-1, 0)` | 横（`localScale.x < 0`） |
| T-20 | `(1, 1)`（45度） | 横判定になる（`|x| >= |y|` の境界挙動を固定する回帰テスト） |
| T-21 | 大きさ `0.005` の微小入力 | Idle 扱い（直前の向きを維持） |

### 5.5 SEU フック（EditMode, 現状の接続範囲のみ）

| # | 条件 | 期待結果 |
|---|---|---|
| T-22 | `PlayerStatus.IsInversion = true` の状態で `(1,0)` を入力 | 計算結果の速度が `(-moveSpeed, 0)` になる（§2.7 のフックが呼び出し側から正しく使われることの確認。発生源の実装はスコープ外） |

### 5.6 シーン上の実機確認（PlayMode / 手動, 自動化できない範囲）

- キーボード（矢印キー/WASD）で8方向に移動できる
- 攻撃アニメーション中（Active状態）は移動入力を入れても位置が変わらない
- スタミナが尽きるとダッシュできなくなり、時間経過（ゲーム内時間）で回復するとまた使える
- モバイル実機 or シミュレータで仮想ジョイスティックからも同じ移動ロジックが動く
- （C-2 対応後）キーボードのみでダッシュが使える

---

## 6. 完了条件（Definition of Done）

- [ ] 移動計算が MonoBehaviour から独立した関数/クラスに切り出されている
- [ ] §5.1〜5.5 の EditMode テストが実装され、全てパスする
- [ ] C-1（スタミナのフレーム依存）が修正され、既存のセーブデータ互換性が確認されている
- [ ] C-4（Active中の慣性ゼロ化）が実装されている
- [ ] 既存の見た目・パラメータ（`moveSpeed` 等の既定値）が変わっていない
- [ ] §6 の未決事項のうち実装に関わるものについて、企画側の回答が得られている
- [ ] 本ドキュメントに記載のない判断をした場合、その理由が本ドキュメントに追記されている

---

## 7. 申し送り事項・未決事項

| # | 項目 | 状態 |
|---|---|---|
| Q-01 | C-2: PC版のダッシュキーは何に割り当てるか（Shift/Space 等）。また「押しっぱなし」と
「クリックでトグル」のどちらの UX にするか（現状の `DashButton` はトグル） | 企画確認待ち |
| Q-02 | C-1: スタミナ消費を秒間レートに変える際、既存の `PlayerSaveData.CurrentStaminaPoint`
（`int`）を `float` にするか、`int` のまま丸めるか | 企画/Codex判断待ち |
| Q-03 | C-5: 斜め入力時にアニメーションを8方向化するかどうか（現状は意図した仕様か未確認） | 企画確認待ち |
| Q-04 | SEU（C-3）の発生源実装は本ハンドオフの対象外としたが、別タスクとして
いつ着手するか | 未定（デモ仕様書 §12.2 の第3週予定と合わせる想定） |
