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

    public FlagSaveData ToSaveData()
    {
        return new FlagSaveData
        {
            CraftTutorial = craftTutorial,
            CraftWorkbench = craftWorkbench,
            DestructionRock = destructionRock,
            FindSomeone = findSomeone,
        };
    }
    public void FromSaveData(FlagSaveData saveData)
    {
        craftTutorial = saveData.CraftTutorial;
        craftWorkbench = saveData.CraftWorkbench;
        destructionRock = saveData.DestructionRock;
        findSomeone = saveData.FindSomeone;
    }
}