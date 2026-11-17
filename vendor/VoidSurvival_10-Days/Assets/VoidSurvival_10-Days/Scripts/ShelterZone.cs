using UnityEngine;

/// <summary>
/// プレイヤーがこのトリガーの中にいる間、PlayerShelterState.IsSheltered を true にする。
/// ZONE-Nの遮蔽物オブジェクトにCollider2D(IsTrigger)と一緒に付ける。
/// </summary>
public class ShelterZone : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
        {
            PlayerShelterState.Enter();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
        {
            PlayerShelterState.Exit();
        }
    }
}
