using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "FlagData", menuName = "FlagData")]
public class FlagData : ScriptableObject
{
    private static FlagData _instance;
    public static FlagData Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<FlagData>("FlagData");
            return _instance;
        }
    }

    public bool craftTutorial = false;
    public bool craftWorkbench = false;
    [Header("ZONE-N")]
    public bool destructionRock = false;
    public bool findSomeone = false;

    [Header("中継局(予報の解除状態)")]
    public bool relayStation1Connected = false; // 磁場→帯電の予報
    public bool relayStation2Connected = false; // 放射線→SEUの予報
    public bool relayStation3Connected = false; // 電流・温度→日照/食の予報

    public FlagSaveData ToSaveData()
    {
        return new FlagSaveData
        {
            CraftTutorial = craftTutorial,
            CraftWorkbench = craftWorkbench,
            DestructionRock = destructionRock,
            FindSomeone = findSomeone,
            RelayStation1Connected = relayStation1Connected,
            RelayStation2Connected = relayStation2Connected,
            RelayStation3Connected = relayStation3Connected,
        };
    }
    public void FromSaveData(FlagSaveData saveData)
    {
        craftTutorial = saveData.CraftTutorial;
        craftWorkbench = saveData.CraftWorkbench;
        destructionRock = saveData.DestructionRock;
        findSomeone = saveData.FindSomeone;
        relayStation1Connected = saveData.RelayStation1Connected;
        relayStation2Connected = saveData.RelayStation2Connected;
        relayStation3Connected = saveData.RelayStation3Connected;
    }
}