using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class EnemySpawner : MonoBehaviour
{
    [Header("敵情報(デフォルト)")]
    [SerializeField] private int spawnCount;
    [SerializeField] private List<SpawnEnemy> spawnEnemies;
    [Header("敵情報(天気ごと)")]
    [SerializeField] private List<WeatherSpawnCount> weatherSpawnCounts;
    [SerializeField] private List<WeatherSpawnEnemy> weatherSpawnEnemies;

    [Header("沸き範囲")]
    [SerializeField] private Vector2 minXY;
    [SerializeField] private Vector2 maxXY;

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

    /// <summary>
    /// どのエネミーが出るか決める
    /// </summary>
    /// <param name="x"></param>
    /// <returns></returns>
    private GameObject SelectEnemy(int x, List<SpawnEnemy> enemies)
    {
        var i = 0;
        foreach (var enemy in enemies)
        {
            i += enemy.spawnRange;
            if (x <= i) return enemy.enemyPrefab;
        }
        return null;
    }
    /// <summary>
    /// 敵をNavMesh上のランダムな位置にスポーン
    /// </summary>
    /// <param name="enemy"></param>
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0f, 0.6f);
        Vector2 transform = this.transform.position;
        Gizmos.DrawLine(minXY, new Vector2(minXY.x, maxXY.y));
        Gizmos.DrawLine(minXY, new Vector2(maxXY.x, minXY.y));
        Gizmos.DrawLine(maxXY, new Vector2(minXY.x, maxXY.y));
        Gizmos.DrawLine(maxXY, new Vector2(maxXY.x, minXY.y));
    }

    [System.Serializable]
    class SpawnEnemy
    {
        public GameObject enemyPrefab;
        public int spawnRange;
    }
    [System.Serializable]
    class WeatherSpawnCount
    {
        public Weather weather;
        public int spawnCount;
    }
    [System.Serializable]
    class WeatherSpawnEnemy
    {
        public Weather weather;
        public List<SpawnEnemy> Enemies;
    }
}
