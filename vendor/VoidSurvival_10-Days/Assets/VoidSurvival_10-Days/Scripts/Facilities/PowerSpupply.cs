using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 作業台の作業時間の短縮　一段階ごとに-15％
/// 主人公のスタミナの最大値の上昇　一段階ごとに＋33％
/// 主人公のスタミナの回復速度の上昇　一段階ごとに+33%
/// 衛星施設のダメージ蓄積量の増加　一段階ごとに＋25％
/// 見た目の変更、地上の基地上部にある太陽光パネルの面積が一段階ごとに広くなる
/// 故障：
/// 作業台が使えなくなる（詰み防止のため、修理には材料の加工を必要としない物を用いるようにする）
/// 通信が出来なくなる
/// 電源、OBC、ミッション、通信から得られる効果がなくなる（天候操作、通信も無くなる）
/// </summary>
class PowerSupply : FacilityBase
{
    // 電圧状態による作業台効率への補正(仮値。バランス調整は別途)
    // LOW: 電圧不足で作業台が遅くなる / HIGH: 電圧十分で若干速くなる
    internal static float VoltageEfficiencyDelta(string voltageState)
    {
        switch (voltageState)
        {
            case "LOW": return 0.20f;
            case "HIGH": return -0.10f;
            default: return 0f; // NORMAL or unknown
        }
    }

    public override FacilityManager.FacilityType FacilityType => FacilityManager.FacilityType.PowerSupply;
    public override String FacilityName { get; protected set; } = "電源";
    public override int MaxHealthPoint { get; protected set; } = 200;
    public override List<BuffBase> Buffs => new List<BuffBase>()
    {
        new DynamicBuff(() =>
        {
            if (IsBroken) return 0f;
            float levelEffect = -0.15f * Level;
            float voltageEffect = HKDataDisplay.Instance != null
                ? VoltageEfficiencyDelta(HKDataDisplay.Instance.VoltageState)
                : 0f;
            return levelEffect + voltageEffect;
        })
        {
            BuffType = OthersBuffType.WorkbenchEfficiency,
            OperationType = BuffOperationType.Multiply,
            Description = "WorkbenchEfficiency affected by Level and Voltage"
        },
        new DynamicBuff(() =>
        {
            if (IsBroken) return 0f;
            return 0.33f * Level;
        })
        {
            BuffType = CharacterBuffType.MaxStaminaPoint,
            OperationType = BuffOperationType.Multiply,
            Description = "MaxStaminaPoint increased by " + (33 * Level) + "%"
        },
        new DynamicBuff(() =>
        {
            if (IsBroken) return 0f;
            return 0.33f * Level;
        })
        {
            BuffType = CharacterBuffType.StaminaHealSpeed,
            OperationType = BuffOperationType.Multiply,
            Description = "StaminaRecoverySpeed increased by " + (33 * Level) + "%"
        },
        new DynamicBuff(() =>
        {
            if (IsBroken) return 0f;
            return 0.25f * Level;
        })
        {
            BuffType = OthersBuffType.FacilityDamageAccumulation,
            OperationType = BuffOperationType.Multiply,
            Description = "FacilityDamageAccumulation increased by " + (25 * Level) + "%"
        }
    };
}
