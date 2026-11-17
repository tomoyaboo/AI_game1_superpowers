# リスポーン実装 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **この計画はCodex CLI（`codex exec`）が実行する。** Claude Code（オーケストレーター）が
> 本ファイルを渡し、Codexが実装、Claude Codeが差分レビューとコンパイル/テスト実行結果の
> 確認（Unityバッチモードは直接実行）を行う。

**Goal:** `EnemySpawner` が `Start()` で1回しか敵を湧かせない問題を解消し、ZONE-N 滞在中に
倒した分だけ時間差で再出現するようにする（DemoSpec §11.3「未着手・不足」、
§8.4「リプレイ性: リスポーン」、§9.3「ドローンはリスポーンする(繰り返し狩る対象)」に対応）。

## 前提の確認

- シーン自体の再読込（`SceneMover` が `SceneManager.LoadSceneAsync` で非加算ロードする）
  により「ゾーンを出て再入場すると敵が湧き直す」は**既に実現できている**
  （`EnemySpawner` に `DontDestroyOnLoad` が無いため、シーン再読込で `Start()` が
  再実行される）。本タスクが対象とするのは、**同じ入場中に倒した敵が時間差で
  再出現しない**という別の問題
- `EnemySpawner` が湧かせるのは名前の無い「ドローン」のみ（§9.1）。名前付きの
  保守ユニット(UNIT 01〜06、一度きり)はシーンに直接配置された別オブジェクトであり、
  `EnemySpawner` の管理対象ではない。したがって本タスクの変更が保守ユニットの
  リスポーンに影響することはない

**Architecture:** 湧かせた敵の `CharacterStatus.onDie`（既存のUnityEvent）を購読し、
死亡を検知したら一定時間後に「現在の天候に基づく目標数」を再計算し、生存数が
目標数未満なら1体補充する。天候に応じたスポーン数/候補リストの決定ロジック
（既存の `Start()` 内のコード）を再利用できるよう `DetermineSpawnPlan()` に切り出す。

**Tech Stack:** Unity 2023.1.22f1, C#, Unity Test Framework

**Spec:** `docs/superpowers/specs/2026-11-17-voidsurvival-ai-collab-design.md`
（§5 タスク#5に対応）。背景: `docs/VoidSurvival_DemoSpec_v0.3.md` §8.4, §9.1-9.3, §11.3
（git管理外）

## Global Constraints

- 作業ディレクトリは `vendor/VoidSurvival_10-Days/`
- upstream への push・merge は行わない
- **保守ユニット(UNIT 01〜06)やその他の固定配置オブジェクトには触れない。**
  `EnemySpawner` が湧かせるドローンのリスポーンのみを扱う
- 既存の `SelectEnemy`/天候別スポーン数・候補リストの選択ロジックの**計算結果は
  変更しない**（リファクタ(メソッド切り出し)のみで、同じ入力に対して同じ出力を
  返すこと）
- Unityバッチモードのコンパイル確認・テスト実行は **Claude Codeが直接実行する**。
  Codexは実装のみ行い、コミットもしない

## Review Focus

- **既存の初回スポーン(`Start()`時の個体数・種類選択)の挙動が変わっていないこと**
- **リスポーンが無限に増え続けないこと**: 目標数(天候に応じて変動)を超えて
  補充しないこと
- **`NavMesh.SamplePosition` が失敗した場合（戻り値がfalseの場合）に
  nullやdefaultを安全に扱うこと**（既存コードは失敗時に何もしないだけだったが、
  戻り値を返す形に変えるため、失敗時の戻り値を明確にすること）
- **既存のテスト(Task1〜4で追加した19件)が引き続きパスすること**

---

### Task 1: リスポーン機能の実装

**Files:**
- Modify: `Assets/VoidSurvival_10-Days/Scripts/EnemySpawner.cs`
- Test: `Assets/VoidSurvival_10-Days/Tests/EditMode/EnemySpawnerRespawnTests.cs`

**Interfaces:**
- Produces: `EnemySpawner.ShouldRespawn(int aliveCount, int targetCount) -> bool`
  （`internal static`。純粋なしきい値判定のみ切り出してテストする）

**現状:** `Start()` 内で天候に応じた `count`/`enemies` をローカル変数として決定し、
その場でループしてスポーンするだけ。以後、敵が死んでも何も起きない。

- [ ] **Step 1: 失敗するテストを書く**

`Assets/VoidSurvival_10-Days/Tests/EditMode/EnemySpawnerRespawnTests.cs`:

```csharp
using NUnit.Framework;

public class EnemySpawnerRespawnTests
{
    [Test]
    public void ShouldRespawn_AliveCountBelowTarget_ReturnsTrue()
    {
        Assert.IsTrue(EnemySpawner.ShouldRespawn(aliveCount: 2, targetCount: 3));
    }

    [Test]
    public void ShouldRespawn_AliveCountEqualsTarget_ReturnsFalse()
    {
        Assert.IsFalse(EnemySpawner.ShouldRespawn(aliveCount: 3, targetCount: 3));
    }

    [Test]
    public void ShouldRespawn_AliveCountAboveTarget_ReturnsFalse()
    {
        // 天候が変わって目標数が減った場合など。既存の生存体を消す必要はないが、
        // 新たに補充もしない。
        Assert.IsFalse(EnemySpawner.ShouldRespawn(aliveCount: 5, targetCount: 3));
    }
}
```

- [ ] **Step 2: Unityバッチモードでテストを実行し、失敗を確認**

（`ShouldRespawn` が存在しないためコンパイルエラーになることを確認。
`EnemySpawner` は `public class` なので `internal static` メンバーのテストには
既存の `InternalsVisibleTo("VoidSurvival.Tests.EditMode")`（`AssemblyInfo.cs`）が
そのまま使える）

- [ ] **Step 3: `EnemySpawner.cs` を実装**

既存の `Start()` を次のように分割・変更する:

```csharp
[Header("リスポーン設定")]
[Tooltip("敵が死んでから再出現を試みるまでの秒数(仮値。バランス調整は別途)")]
[SerializeField] private float respawnDelay = 10f;

private int _aliveCount;

void Start()
{
    SpawnInitialEnemies();
}

private void SpawnInitialEnemies()
{
    var (targetCount, enemyPool) = DetermineSpawnPlan();
    for (int i = 0; i < targetCount; i++)
    {
        SpawnOne(enemyPool);
    }
}

/// <summary>
/// 現在の天候に基づいて、目標スポーン数と候補リストを決定する。
/// 既存のStart()内にあった選択ロジックをそのまま移したもの(計算結果は変更しない)。
/// </summary>
private (int count, List<SpawnEnemy> enemies) DetermineSpawnPlan()
{
    int count = spawnCount;
    List<SpawnEnemy> enemies = spawnEnemies;
    foreach (var value in weatherSpawnCounts)
    {
        if (EqualityComparer<Weather>.Default.Equals(WeatherTester.Instance.CurrentRuleBasedWeather, value.weather))
        {
            count = value.spawnCount;
        }
    }
    foreach (var value in weatherSpawnEnemies)
    {
        if (EqualityComparer<Weather>.Default.Equals(WeatherTester.Instance.CurrentRuleBasedWeather, value.weather))
        {
            enemies = value.Enemies;
        }
    }
    return (count, enemies);
}

private void SpawnOne(List<SpawnEnemy> enemyPool)
{
    if (enemyPool == null || enemyPool.Count == 0) return;
    var maxRange = enemyPool.Sum(x => x.spawnRange);
    if (maxRange <= 0) return;

    var prefab = SelectEnemy(Random.Range(1, maxRange + 1), enemyPool);
    var instance = EnemySpawn(prefab);
    if (instance == null) return;

    _aliveCount++;
    var status = instance.GetComponent<CharacterStatus>();
    if (status != null)
    {
        status.onDie.AddListener(OnSpawnedEnemyDied);
    }
}

private void OnSpawnedEnemyDied()
{
    _aliveCount--;
    StartCoroutine(TryRespawnAfterDelay());
}

private IEnumerator TryRespawnAfterDelay()
{
    yield return new WaitForSeconds(respawnDelay);
    var (targetCount, enemyPool) = DetermineSpawnPlan();
    if (ShouldRespawn(_aliveCount, targetCount))
    {
        SpawnOne(enemyPool);
    }
}

/// <summary>現在の生存数が目標数未満かどうかだけを判定する純粋関数。</summary>
internal static bool ShouldRespawn(int aliveCount, int targetCount)
{
    return aliveCount < targetCount;
}
```

既存の `EnemySpawn(GameObject enemy)` メソッドの戻り値を `void` から `GameObject` に
変更し、スポーンに成功したインスタンスを返すようにする（失敗時は `null`）:

```csharp
private GameObject EnemySpawn(GameObject enemy)
{
    NavMeshHit navMeshHit;
    var randomPosition = new Vector3(Random.Range(minXY.x, maxXY.x), Random.Range(minXY.y, maxXY.y), 0);
    if (NavMesh.SamplePosition(randomPosition, out navMeshHit, 10, NavMesh.AllAreas))
    {
        return Instantiate(enemy, navMeshHit.position, Quaternion.identity);
    }
    return null;
}
```

`using System.Collections;`（コルーチン用）が無ければ追加すること。

- [ ] **Step 4: Unityバッチモードでコンパイル確認・テスト実行**

Expected: コンパイル成功、`EnemySpawnerRespawnTests` の3件がPassed。
既存の19件(Task1〜4)も引き続きPassedであること。

---

## 完了後の報告事項（Codex → Claude Code）

- 変更/作成したファイル一覧
- `EnemySpawn` の戻り値変更に伴い、他に呼び出し箇所が無かったか確認した結果
- 作業中に見つかった、本プラン記載以外の問題（あれば報告のみ）
