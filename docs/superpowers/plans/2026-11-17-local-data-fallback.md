# PlayFab未接続時のローカルデータフォールバック Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **この計画はCodex CLI（`codex exec`）が実行する。** Claude Code（オーケストレーター）が
> 本ファイルを渡し、Codexが実装、Claude Codeが差分レビューとコンパイル/テスト実行結果の
> 確認（Unityバッチモードは直接実行）を行う。

**Goal:** PlayFabへのログイン・データ取得が失敗する、またはタイムアウトする場合に、
同梱のローカルJSONデータへ自動的に切り替える。DemoSpec §12.3「デモ当日のための保険」
（会場のネットワーク障害時の対策として元から計画されていた機能）に対応する。
これにより、ネットワーク環境を問わず（Xcodeでの実機テスト時含む）デモが動作する。

## 前提（既に用意済み）

- `Assets/Resources/LocalFallbackData/GameMasterData.json` — 軌道1周分(270サンプル)の
  Gyro/Mag1/Mag2/Mag3/Radiationのダミーデータ。SAA通過を模した区間を含む
  （`GameMasterDataRoot` と同じスキーマ）
- `Assets/Resources/LocalFallbackData/HKData.json` — 20行分の温度(5面)/電圧/電流の
  ダミーデータ。食→日照の遷移を模している
  （`HKDataDisplay.ParseHKData` が期待する形式: 配列、各行に`Date`/`Split No`/
  `"XXX (°C)"`/`"XXX (mV)"`または`"(V)"`/`"XXX (mA)"`列）

**これらのファイルは既に配置済み。本タスクではデータファイル自体は作成・変更しない。**

## 現状のバグ（今回合わせて直す）

`DataManager.LoadDataFromPlayFab()` は現在、`LoginWithCustomID`/`GetTitleData` の
**エラーコールバックが両方 `null`**。失敗しても何も起きず、`_isDataLoaded` が
永遠に`false`のまま沈黙する（§12.3の保険が実質存在しない状態）。

## Global Constraints

- 作業ディレクトリは `vendor/VoidSurvival_10-Days/`
- upstream への push・merge は行わない
- **既存のPlayFab成功時の挙動(データの解析・状態更新)は変更しない。** 失敗時の
  処理を追加するだけ
- フォールバック使用時は `DataSourceLabel`（"LIVE"/"SIM"）を公開し、将来のUI
  （DemoSpec §5.4の `[TEST DATA]`/`[SIM]` 表示）が参照できるようにする
- タイムアウト値は仮値(10秒)で良い。`[SerializeField]` で調整可能にすること
- Unityバッチモードのコンパイル確認・テスト実行は **Claude Codeが直接実行する**。
  Codexは実装のみ行い、コミットもしない

## Review Focus

- **PlayFabが正常に応答する既存のケースの挙動が一切変わっていないこと**
- **ログイン失敗・GetTitleData失敗・タイムアウトの3パターンすべてでフォールバックが
  効くこと**
- **フォールバックデータの解析が、PlayFabから来たデータの解析と同じコードパスを
  通ること**（JSON文字列を渡す先は共通化し、取得元だけが違う形にする）
- **既存のテスト(現時点で56件)が引き続きパスすること**

---

### Task 1: `DataManager` にローカルフォールバックを追加

**Files:**
- Modify: `Assets/VoidSurvival_10-Days/Scripts/PlayfabTest/DataManager.cs`
- Test: `Assets/VoidSurvival_10-Days/Tests/EditMode/DataManagerFallbackTests.cs`

**Interfaces:**
- Produces: `DataManager.DataSourceLabel { get; }` (string, "LIVE" or "SIM"),
  `DataManager.ParseGameMasterData(string json) -> GameMasterDataRoot`
  （既存のPlayFab成功時の解析処理をこの `internal static` 関数に切り出す）

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System.IO;
using NUnit.Framework;
using UnityEngine;

public class DataManagerFallbackTests
{
    [Test]
    public void ParseGameMasterData_LocalFallbackJson_ParsesSuccessfully()
    {
        var textAsset = Resources.Load<TextAsset>("LocalFallbackData/GameMasterData");
        Assert.IsNotNull(textAsset, "LocalFallbackData/GameMasterData.json が Resources に見つかりません");

        var root = DataManager.ParseGameMasterData(textAsset.text);

        Assert.IsNotNull(root);
        Assert.IsNotEmpty(root.Gyro);
        Assert.IsNotEmpty(root.Mag1);
        Assert.IsNotEmpty(root.Radiation);
    }

    [Test]
    public void ParseGameMasterData_InvalidJson_ReturnsNull()
    {
        var result = DataManager.ParseGameMasterData("not valid json");
        Assert.IsNull(result);
    }
}
```

- [ ] **Step 2: Unityバッチモードでテストを実行し、失敗を確認**

- [ ] **Step 3: `DataManager.cs` に実装**

```csharp
public string DataSourceLabel { get; private set; } = "LIVE";

[Tooltip("PlayFab応答待ちの最大秒数。超えたらローカルフォールバックに切り替える(仮値)")]
[SerializeField] private float playFabTimeoutSeconds = 10f;
```

既存の `JsonConvert.DeserializeObject<GameMasterDataRoot>(jsonStr)` を使っている箇所を
切り出し:

```csharp
internal static GameMasterDataRoot ParseGameMasterData(string json)
{
    try
    {
        return JsonConvert.DeserializeObject<GameMasterDataRoot>(json);
    }
    catch (System.Exception)
    {
        return null;
    }
}
```

既存の `LoadDataFromPlayFab()` を次のように変更する(タイムアウト用コルーチンを追加し、
エラーコールバックを設定する):

```csharp
private bool _fallbackTriggered = false;

private void LoadDataFromPlayFab()
{
    StartCoroutine(TimeoutFallbackCoroutine());

    var loginRequest = new LoginWithCustomIDRequest { CustomId = "VoidSurvival_User", CreateAccount = false };
    PlayFabClientAPI.LoginWithCustomID(loginRequest, loginRes => {
        PlayFabClientAPI.GetTitleData(new GetTitleDataRequest(), result => {
            if (_fallbackTriggered) return; // タイムアウトで既に切り替わっていたら無視
            if (result.Data.ContainsKey("GameMasterData"))
            {
                string jsonStr = result.Data["GameMasterData"];
                var parsed = ParseGameMasterData(jsonStr);
                if (parsed != null)
                {
                    _rootData = parsed;
                    OnGameMasterDataLoaded("LIVE");
                }
                else
                {
                    LoadLocalFallback();
                }
            }
            else
            {
                LoadLocalFallback();
            }
        }, error => { if (!_fallbackTriggered) LoadLocalFallback(); });
    }, error => { if (!_fallbackTriggered) LoadLocalFallback(); });
}

private IEnumerator TimeoutFallbackCoroutine()
{
    yield return new WaitForSeconds(playFabTimeoutSeconds);
    if (!_isDataLoaded)
    {
        LoadLocalFallback();
    }
}

private void LoadLocalFallback()
{
    if (_fallbackTriggered || _isDataLoaded) return;
    _fallbackTriggered = true;

    var textAsset = Resources.Load<TextAsset>("LocalFallbackData/GameMasterData");
    if (textAsset == null)
    {
        Debug.LogError("[DataManager] ローカルフォールバックデータが見つかりません。");
        return;
    }
    var parsed = ParseGameMasterData(textAsset.text);
    if (parsed == null)
    {
        Debug.LogError("[DataManager] ローカルフォールバックデータの解析に失敗しました。");
        return;
    }
    _rootData = parsed;
    OnGameMasterDataLoaded("SIM");
}

/// <summary>データロード完了後の共通処理(既存のStart()成功時の処理と同じ内容)。</summary>
private void OnGameMasterDataLoaded(string sourceLabel)
{
    if (_rootData != null && _rootData.Gyro != null && _rootData.Gyro.Count > 0)
    {
        DataSourceLabel = sourceLabel;
        _dataStartOffset = _rootData.Gyro[0].TotalSeconds;
        _dataSpanSec = _rootData.Gyro[_rootData.Gyro.Count - 1].TotalSeconds - _dataStartOffset;
        ComputeStateRanges();
        _isDataLoaded = true;
        Debug.Log($"★全センサーデータ読み込み完了！(source={sourceLabel})");
    }
}
```

（`using System.Collections;` が無ければコルーチン用に追加すること。既存の
`Start()` 内の `LoadDataFromPlayFab()` 呼び出しはそのまま。既存のPlayFab成功時の
処理(今までStart()直下にあったコード)は `OnGameMasterDataLoaded("LIVE")` に
統合されるため、元のPlayFabコールバック内の重複コードは削除してよい）

- [ ] **Step 4: Unityバッチモードでコンパイル確認・テスト実行**

Expected: コンパイル成功、`DataManagerFallbackTests` の2件がPassed。

---

### Task 2: `HKDataDisplay` にローカルフォールバックを追加

**Files:**
- Modify: `Assets/VoidSurvival_10-Days/Scripts/PlayfabTest/HKData/HKDataDisplay.cs`
- Test: `Assets/VoidSurvival_10-Days/Tests/EditMode/HKDataDisplayFallbackTests.cs`

**Interfaces:**
- Produces: `HKDataDisplay.DataSourceLabel { get; }` (string),
  `HKDataDisplay.ParseHkRows(string json) -> Newtonsoft.Json.Linq.JArray`
  （既存の `ParseHKData` 内のJSON解析部分を切り出す）

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using NUnit.Framework;
using UnityEngine;

public class HKDataDisplayFallbackTests
{
    [Test]
    public void ParseHkRows_LocalFallbackJson_ParsesSuccessfully()
    {
        var textAsset = Resources.Load<TextAsset>("LocalFallbackData/HKData");
        Assert.IsNotNull(textAsset, "LocalFallbackData/HKData.json が Resources に見つかりません");

        var rows = HKDataDisplay.ParseHkRows(textAsset.text);

        Assert.IsNotNull(rows);
        Assert.Greater(rows.Count, 0);
    }
}
```

- [ ] **Step 2: Unityバッチモードでテストを実行し、失敗を確認**

- [ ] **Step 3: `HKDataDisplay.cs` に実装**

```csharp
public string DataSourceLabel { get; private set; } = "LIVE";

[Tooltip("PlayFab応答待ちの最大秒数。超えたらローカルフォールバックに切り替える(仮値)")]
[SerializeField] private float playFabTimeoutSeconds = 10f;
private bool _fallbackTriggered = false;
```

既存の `ParseHKData(string json)` 内、JSON解析部分を切り出す:

```csharp
internal static JArray ParseHkRows(string json)
{
    JToken root = JToken.Parse(json);
    return (root.Type == JTokenType.Array) ? (JArray)root : root["rows"] as JArray;
}
```

`ParseHKData` はこれを呼ぶ形に変更(既存の列選択・range計算ロジックはそのまま)。

`Start()` でタイムアウト監視コルーチンを開始:

```csharp
private void Start()
{
    PlayFabSettings.staticSettings.TitleId = "148482";
    StartCoroutine(TimeoutFallbackCoroutine());
    LoadHKData();
}

private IEnumerator TimeoutFallbackCoroutine()
{
    yield return new WaitForSeconds(playFabTimeoutSeconds);
    if (!_isLoaded)
    {
        LoadLocalFallback();
    }
}

private void LoadLocalFallback()
{
    if (_fallbackTriggered || _isLoaded) return;
    _fallbackTriggered = true;

    var textAsset = Resources.Load<TextAsset>("LocalFallbackData/HKData");
    if (textAsset == null)
    {
        Debug.LogError("[HKDataDisplay] ローカルフォールバックデータが見つかりません。");
        return;
    }
    DataSourceLabel = "SIM";
    ParseHKData(textAsset.text);
}
```

既存の `LoadHKData` のリトライ上限到達時のエラーログの直後、またはそれぞれの
エラーコールバック内で `LoadLocalFallback()` を呼ぶように変更する
（`_fallbackTriggered` の既チェックにより、タイムアウトと重複しても安全）。

- [ ] **Step 4: Unityバッチモードでコンパイル確認・テスト実行**

Expected: コンパイル成功、`HKDataDisplayFallbackTests` の1件がPassed。
既存の全テスト(56件)も引き続きPassedであること(合計59件)。

---

## 完了後の報告事項（Codex → Claude Code）

- 変更/作成したファイル一覧
- 作業中に見つかった、本プラン記載以外の問題（あれば報告のみ）
