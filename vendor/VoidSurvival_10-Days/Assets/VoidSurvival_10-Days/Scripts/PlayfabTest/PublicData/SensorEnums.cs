// ============================================================
//  SensorEnums.cs
//  センサーの3段階と天気の状態をまとめて定義するファイル。
//  ※このファイルは MonoBehaviour ではないので、ファイル名は何でもOK。
// ============================================================

/// <summary>各センサーを3段階に分けたときのレベル。数値（0,1,2）は天気合成の点数計算に使う。</summary>
public enum Level
{
    Low  = 0, // 低
    Mid  = 1, // 中
    High = 2  // 高
}

/// <summary>ゲーム内の天気状態。</summary>
public enum Weather
{
    Sunny,        // 晴れ
    Cloudy,       // 曇り
    Rainy,        // 雨
    Thunderstorm  // 雷雨
}

/// <summary>明暗の状態(軸1)。電流状態から判定する。</summary>
public enum LightPhase
{
    Sun,     // 日照
    Eclipse  // 食
}

/// <summary>計器信頼性の状態(軸3)。ジャイロ状態から判定する。</summary>
public enum InstrumentReliability
{
    Stable,    // 安定
    Disturbed  // 乱れ
}
