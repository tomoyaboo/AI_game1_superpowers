using System;
using UnityEngine;

// ============================================================
//  WeatherTester.cs
//  実データ(DataManager/HKDataDisplay)のLOW/NORMAL/HIGH状態から
//  明暗・荒れ具合・計器信頼性の3軸を独立に判定。
//  天気が変わるたびに OnWeatherChanged で通知する（WeatherDisplayが購読）。
// ============================================================

public class WeatherTester : MonoBehaviour
{
    public static WeatherTester Instance { get; private set; }

    [Header("実データ連動の天気判定")]
    [SerializeField] private DataManager dataManager;
    [SerializeField] private HKDataDisplay hkDataDisplay;

    public Weather CurrentRuleBasedWeather { get; private set; } = Weather.Sunny;

    public event Action<Weather> OnWeatherChanged;

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

    public LightPhase CurrentLightPhase { get; private set; } = LightPhase.Sun;
    public InstrumentReliability CurrentInstrumentReliability { get; private set; } = InstrumentReliability.Stable;

    // 軸2(荒れ具合): 放射線+磁場3つのうちHIGHの個数で段階的に決める。
    // LOW/NORMALの個数には依存しない(個数に関わらずHIGHが0なら必ずSunny)。
    internal static Weather EvaluateRoughWeather(string radiationState, string mag1State, string mag2State, string mag3State)
    {
        int highCount = 0;
        foreach (var s in new[] { radiationState, mag1State, mag2State, mag3State })
        {
            if (s == "HIGH") highCount++;
        }
        var levels = new[] { Weather.Sunny, Weather.Cloudy, Weather.Rainy, Weather.Thunderstorm };
        return levels[Math.Min(highCount, levels.Length - 1)];
    }

    // 軸1(明暗): 電流が低いときは食(Eclipse)とみなす。
    internal static LightPhase EvaluateLightPhase(string currentState)
    {
        return currentState == "LOW" ? LightPhase.Eclipse : LightPhase.Sun;
    }

    // 軸3(計器信頼性): ジャイロが高いときは姿勢が乱れているとみなす。
    internal static InstrumentReliability EvaluateInstrumentReliability(string gyroState)
    {
        return gyroState == "HIGH" ? InstrumentReliability.Disturbed : InstrumentReliability.Stable;
    }

    private void LateUpdate()
    {
        if (dataManager == null || hkDataDisplay == null) return;

        Weather weather = EvaluateRoughWeather(
            dataManager.RadiationState, dataManager.Mag1State, dataManager.Mag2State, dataManager.Mag3State);
        CurrentLightPhase = EvaluateLightPhase(hkDataDisplay.CurrentState);
        CurrentInstrumentReliability = EvaluateInstrumentReliability(dataManager.GyroState);

        if (weather != CurrentRuleBasedWeather)
        {
            CurrentRuleBasedWeather = weather;
            Debug.Log($"[WeatherTester] rad={dataManager.RadiationState} mag1={dataManager.Mag1State} " +
                       $"mag2={dataManager.Mag2State} mag3={dataManager.Mag3State} → Weather: {weather}");
            OnWeatherChanged?.Invoke(weather);
        }
    }
}
