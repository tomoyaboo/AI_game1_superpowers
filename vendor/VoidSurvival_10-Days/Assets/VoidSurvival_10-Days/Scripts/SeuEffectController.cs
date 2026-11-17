using UnityEngine;

/// <summary>
/// SEU(シングルイベントアップセット)の発生を管理するシングルトン。
/// SAA通過中(磁場が弱い かつ 放射線が強い)に確率で発生し、一定時間
/// PlayerStatus.IsInversion を true にする(移動の左右反転)。
///
/// 遮蔽物(ShelterZone)の中にいる間はSEUの判定をしない。
/// </summary>
public class SeuEffectController : MonoBehaviour
{
    public static SeuEffectController Instance { get; private set; }

    private enum SeuPhase { Normal, Active, Cooldown }

    [Header("SEU調整パラメータ(仮値。バランス調整は別途)")]
    [SerializeField] private float checkInterval = 1f;
    [SerializeField] private float baseTriggerProbability = 0.04f;
    [SerializeField] private float shieldProbabilityMultiplier = 0.25f;
    [SerializeField] private float activeDuration = 12f;
    [SerializeField] private float cooldownDuration = 20f;

    private SeuPhase _phase = SeuPhase.Normal;
    private float _timer;
    private PlayerStatus _playerStatus;

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

    private void Start()
    {
        _playerStatus = FindAnyObjectByType<PlayerStatus>();
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        switch (_phase)
        {
            case SeuPhase.Normal:
                if (_timer >= checkInterval)
                {
                    _timer = 0f;
                    TryTrigger();
                }
                break;
            case SeuPhase.Active:
                if (_timer >= activeDuration) EndActive();
                break;
            case SeuPhase.Cooldown:
                if (_timer >= cooldownDuration)
                {
                    _phase = SeuPhase.Normal;
                    _timer = 0f;
                }
                break;
        }
    }

    private void TryTrigger()
    {
        if (DataManager.Instance == null || _playerStatus == null) return;
        if (PlayerShelterState.IsSheltered) return; // 遮蔽物の中にいる間は判定しない

        bool saa = IsSaaCondition(
            DataManager.Instance.Mag1State, DataManager.Instance.Mag2State,
            DataManager.Instance.Mag3State, DataManager.Instance.RadiationState);
        if (!saa) return;

        float probability = CalculateTriggerProbability(
            baseTriggerProbability, IsShieldEquipped(), shieldProbabilityMultiplier);
        if (Random.value < probability)
        {
            BeginActive();
        }
    }

    /// <summary>磁場のいずれか1つでも弱く(LOW)、かつ放射線が強い(HIGH)ならSAA通過中とみなす。</summary>
    internal static bool IsSaaCondition(string mag1State, string mag2State, string mag3State, string radiationState)
    {
        bool magWeak = mag1State == "LOW" || mag2State == "LOW" || mag3State == "LOW";
        bool radiationHigh = radiationState == "HIGH";
        return magWeak && radiationHigh;
    }

    /// <summary>シールド装備時は確率に倍率を掛けて下げる。</summary>
    internal static float CalculateTriggerProbability(float baseProbability, bool shieldEquipped, float shieldMultiplier)
    {
        return shieldEquipped ? baseProbability * shieldMultiplier : baseProbability;
    }

    private bool IsShieldEquipped()
    {
        if (ItemManager.Instance?.equipmentSlot == null) return false;
        foreach (var kv in ItemManager.Instance.equipmentSlot)
        {
            if (kv.Value != null && kv.Value.isShield) return true;
        }
        return false;
    }

    private void BeginActive()
    {
        _phase = SeuPhase.Active;
        _timer = 0f;
        if (_playerStatus != null) _playerStatus.IsInversion = true;
    }

    private void EndActive()
    {
        _phase = SeuPhase.Cooldown;
        _timer = 0f;
        if (_playerStatus != null) _playerStatus.IsInversion = false;
    }

    /// <summary>リブート操作等からSEUを早期終了させるための口(UI配線は別タスク)。</summary>
    public void ForceEndActive()
    {
        if (_phase == SeuPhase.Active) EndActive();
    }

    /// <summary>
    /// チュートリアルでの確定発火・発表者用の強制発火ボタンのための口
    /// (DemoSpec §6.6。UI配線は別タスク)。Normal状態のときのみ発火する。
    /// </summary>
    public void ForceTrigger()
    {
        if (_phase == SeuPhase.Normal) BeginActive();
    }
}
