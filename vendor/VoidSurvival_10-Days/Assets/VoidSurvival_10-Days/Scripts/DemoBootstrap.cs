using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// デモ専用の起動処理。0_TitleDemo に配置し、通常のTitle(セーブ画面)を経由せず
/// 直接次のシーン(1_Terminal_Demo)へ進む。
/// </summary>
public class DemoBootstrap : MonoBehaviour
{
    [SerializeField] private string nextSceneName = "1_Terminal_Demo";

    private void Start()
    {
        SceneManager.LoadScene(nextSceneName);
    }
}
