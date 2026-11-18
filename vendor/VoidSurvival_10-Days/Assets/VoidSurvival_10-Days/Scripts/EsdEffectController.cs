using UnityEngine;

/// <summary>
/// ESD(衛星帯電・放電)の発生を管理するシングルトン。
/// 面間の温度差(ΔT)が大きく、かつ磁場が弱いときに発生中とみなし、
/// 電源施設に継続ダメージを与える。
///
/// 既知の簡略化: 「触れるとダメージ」等の接触判定・見た目の変更は本タスクでは実装しない。
/// </summary>
public class EsdEffectController : MonoBehaviour
{
    public static EsdEffectController Instance { get; private set; }

    public bool IsEsdActive { get; private set; }

    [Header("ESD調整パラメータ(仮値。バランス調整は別途)")]
    [SerializeField] private float checkInterval = 1f;
    [SerializeField] private float thermalDeltaThreshold = 20f;
    [SerializeField] private float facilityDamagePerCheck = 2f;

    private float _timer;
    private FacilityBase _powerSupply;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);
    }

    private void Start()
    {
        if (FacilityManager.Instance != null)
        {
            _powerSupply = FacilityManager.Instance.GetFacility(FacilityManager.FacilityType.PowerSupply);
        }
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < checkInterval) return;
        _timer = 0f;
        Evaluate();
    }

    private void Evaluate()
    {
        if (HKDataDisplay.Instance == null || DataManager.Instance == null)
        {
            IsEsdActive = false;
            return;
        }

        IsEsdActive = IsEsdCondition(
            HKDataDisplay.Instance.CurrentThermalDelta, thermalDeltaThreshold,
            DataManager.Instance.Mag1State, DataManager.Instance.Mag2State, DataManager.Instance.Mag3State);

        if (IsEsdActive && _powerSupply != null && !_powerSupply.IsBroken)
        {
            _powerSupply.DamageFacility(facilityDamagePerCheck);
        }
    }

    /// <summary>ΔTが閾値を超え、かつ磁場のいずれか1つでも弱いならESD発生条件とみなす。</summary>
    internal static bool IsEsdCondition(float thermalDelta, float threshold, string mag1State, string mag2State, string mag3State)
    {
        bool magWeak = mag1State == "LOW" || mag2State == "LOW" || mag3State == "LOW";
        return thermalDelta > threshold && magWeak;
    }
}
