using UnityEngine;

/// <summary>
/// カット割りS4 O-4「帯電状態で現れる」の簡易表現。
/// MAGのいずれかがLOWのとき、SpriteRendererの色を青白く変える。
/// </summary>
public class ChargedEnemyVisual : MonoBehaviour
{
    [SerializeField] private float checkInterval = 1f;
    [SerializeField] private Color chargedColor = new Color(0.6f, 0.8f, 1f, 1f);

    private SpriteRenderer _spriteRenderer;
    private Color _defaultColor;
    private float _timer;

    private void Start()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null) _defaultColor = _spriteRenderer.color;
    }

    private void Update()
    {
        if (_spriteRenderer == null || DataManager.Instance == null) return;
        _timer += Time.deltaTime;
        if (_timer < checkInterval) return;
        _timer = 0f;

        bool charged = IsChargedCondition(
            DataManager.Instance.Mag1State, DataManager.Instance.Mag2State, DataManager.Instance.Mag3State);
        _spriteRenderer.color = charged ? chargedColor : _defaultColor;
    }

    internal static bool IsChargedCondition(string mag1State, string mag2State, string mag3State)
    {
        return mag1State == "LOW" || mag2State == "LOW" || mag3State == "LOW";
    }
}
