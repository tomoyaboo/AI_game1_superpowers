using TMPro;
using UnityEngine;

/// <summary>
/// カット割りS3/S4で使う観測値3行(RAD/MAG/ΔT)と、S2末尾で初めて出るETA表示を
/// まとめた共通HUD。UIManager.prefab配下に配置され、全シーンで使い回す想定。
/// 表示/非表示はストーリー進行側(各シーンの実装)から呼び出して制御する。
/// </summary>
public class ObservedValuesHud : MonoBehaviour
{
    public static ObservedValuesHud Instance { get; private set; }

    [SerializeField] private GameObject observedValuesPanel;
    [SerializeField] private TMP_Text observedValuesText;
    [SerializeField] private GameObject etaPanel;
    [SerializeField] private TMP_Text etaText;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // 初期状態は両方非表示(ストーリー進行で個別に見せる)
        if (observedValuesPanel != null) observedValuesPanel.SetActive(false);
        if (etaPanel != null) etaPanel.SetActive(false);
    }

    private void Update()
    {
        if (DataManager.Instance == null || HKDataDisplay.Instance == null || TimeManager.Instance == null) return;

        if (observedValuesText != null)
        {
            observedValuesText.text = FormatObservedValues(
                DataManager.Instance.CurrentRadiationSignalCount,
                DataManager.Instance.CurrentMagMagnitude,
                HKDataDisplay.Instance.CurrentThermalDelta);
        }

        if (etaText != null)
        {
            var (days, hours) = TimeManager.Instance.GetReentryEta();
            etaText.text = FormatEta(days, hours);
        }
    }

    public void SetObservedValuesVisible(bool visible)
    {
        if (observedValuesPanel != null) observedValuesPanel.SetActive(visible);
    }

    public void SetEtaVisible(bool visible)
    {
        if (etaPanel != null) etaPanel.SetActive(visible);
    }

    internal static string FormatObservedValues(int rad, float mag, float deltaT)
    {
        string magText = float.IsNaN(mag) ? "--" : Mathf.RoundToInt(mag).ToString();
        // "Δ"は日本語フォールバックフォントの文字リストに含まれておらず□表示に
        // なるため、確実に表示できるASCII文字(dT)を使う。
        return $"RAD {rad} / MAG {magText} / dT {Mathf.RoundToInt(deltaT)}";
    }

    internal static string FormatEta(int days, int hours)
    {
        return $"REENTRY ETA .... {days}D {hours:00}H";
    }
}
