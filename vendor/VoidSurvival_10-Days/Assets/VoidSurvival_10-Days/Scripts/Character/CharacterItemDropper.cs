using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterStatus))]
public class CharacterItemDropper : MonoBehaviour
{
    // 電流状態によるドロップ率への倍率補正(仮値。バランス調整は別途)
    internal static float CurrentDropRateMultiplier(string currentState)
    {
        switch (currentState)
        {
            case "HIGH": return 1.2f;
            case "LOW": return 0.8f;
            default: return 1.0f; // NORMAL or unknown
        }
    }

    internal static float EffectiveDropRate(float baseDropRate, string currentState)
    {
        return Mathf.Clamp01(baseDropRate * CurrentDropRateMultiplier(currentState));
    }

    internal static float ApplyEsdBonus(float baseRate, float multiplier)
    {
        return Mathf.Clamp01(baseRate * multiplier);
    }

    internal static bool ShouldAttemptEnergyMaterialDrop(bool isEnergyMaterial, bool esdActive)
    {
        return !isEnergyMaterial || esdActive;
    }

    [Tooltip("ESD発生中、エネルギー素材のドロップ率に掛ける倍率(仮値)")]
    [SerializeField] private float esdDropRateMultiplier = 1.5f;

    [Header("ドロップアイテム(デフォルト)")]
    [SerializeField] private List<DropItemInfo> dropItems;
    [Header("ドロップアイテム(天気ごと)")]
    [SerializeField] private List<WeatherDropItem> weatherDropItems;
    [SerializeField] private FieldItem fieldItemPrefab;
    private CharacterStatus _status;
    private bool _isDropInvoked;
    private List<DropItemInfo> _items;
    private void Start()
    {
        _status = GetComponent<CharacterStatus>();
        _status.onDie.AddListener(DropIfNeeded);
        _items = dropItems;
        foreach (var value in weatherDropItems)
        {
            if (EqualityComparer<Weather>.Default.Equals(WeatherTester.Instance.CurrentRuleBasedWeather, value.weather))
            {
                _items = value.dropItems;
            }
        }
    }

    private void DropIfNeeded()
    {
        if (_isDropInvoked) return;
        _isDropInvoked = true;
        string currentState = HKDataDisplay.Instance != null ? HKDataDisplay.Instance.CurrentState : "NORMAL";
        foreach (var drop in _items)
        {
            bool esdActive = EsdEffectController.Instance != null && EsdEffectController.Instance.IsEsdActive;
            if (drop.item != null && !ShouldAttemptEnergyMaterialDrop(drop.item.isEnergyMaterial, esdActive)) continue;

            float rate = EffectiveDropRate(drop.dropRate, currentState);
            if (drop.item != null && drop.item.isEnergyMaterial
                && EsdEffectController.Instance != null && EsdEffectController.Instance.IsEsdActive)
            {
                rate = ApplyEsdBonus(rate, esdDropRateMultiplier);
            }
            if (Random.Range(0, 1f) >= rate) continue;
            for (int i = 0; i < drop.number; i++)
            {
                var item = Instantiate(fieldItemPrefab, transform.position, Quaternion.identity);
                item.Item = drop.item;
                item.Initialize();
            }
        }
    }
}

[System.Serializable]
class DropItemInfo
{
    [Range(0, 1)] public float dropRate;
    public Item item;
    public int number = 1;
}
[System.Serializable]
class WeatherDropItem
{
    public Weather weather;
    public List<DropItemInfo> dropItems;
}
