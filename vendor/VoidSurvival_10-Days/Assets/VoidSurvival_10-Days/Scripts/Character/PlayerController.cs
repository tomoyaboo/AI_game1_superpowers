using UnityEngine;
using UnityStandardAssets.CrossPlatformInput;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    [Tooltip("ダッシュ中のスタミナ消費速度(秒間)。仮値。バランス調整は別途")]
    [SerializeField] private float dashStaminaPerSecond = 10f;
    private float _staminaDrainAccumulator = 0f;

    [Tooltip("PCキーボードでダッシュする際のキー")]
    [SerializeField] private KeyCode dashKey = KeyCode.LeftShift;

    private Rigidbody2D _rigidbody;
    private PlayerRenderer _renderer;
    private PlayerStatus _status;

    /// <summary>
    /// インタラクトアクションを実行するためのインターフェース
    /// </summary>
    private InteractAction _interactAction;

    /// <summary>
    /// ジョイスティックからの入力を格納する変数
    /// </summary>
    private Vector2 _moveInput;

    private void Awake()
    {
        DontDestroyOnLoad(this);
    }
    // Start is called before the first frame update
    void Start()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _renderer = GetComponent<PlayerRenderer>();
        _status = GetComponent<PlayerStatus>();
    }

    // Update is called once per frame
    void Update()
    {
        // ジョイスティックからの入力を取得
        _moveInput.x = CrossPlatformInputManager.GetAxis("Horizontal");
        _moveInput.y = CrossPlatformInputManager.GetAxis("Vertical");

        // 入力反転が有効な場合、入力を反転させる
        if (_status.IsInversion)
        {
            _moveInput.x = -_moveInput.x;
            _moveInput.y = -_moveInput.y;
        }

        // 正規化し、速度を一定にする
        _moveInput.Normalize();

        var _moveSpeed = moveSpeed;
        bool dashRequested = IsDashRequested(DashButton.IsDashing, Input.GetKey(dashKey));
        if (_moveInput.magnitude > 0 && dashRequested && _status.IsDashing)
        {
            _moveSpeed = moveSpeed * 2;
            var (amountToConsume, remaining) = CalculateStaminaDrain(_staminaDrainAccumulator, dashStaminaPerSecond, Time.deltaTime);
            _staminaDrainAccumulator = remaining;
            if (amountToConsume > 0)
            {
                _status.ConsumeStamina(amountToConsume);
            }
        }

        if (_status.IsMovable)
        {
            // キャラクターのアニメーションを更新
            _renderer.SetDirection(_moveInput);

            // プレイヤーを動かす
            _rigidbody.velocity = _moveInput * _moveSpeed;
        }
        else
        {
            // 移動不可状態(攻撃中等)になった際、直前の慣性で動き続けないようにする
            _rigidbody.velocity = Vector2.zero;
        }
    }

    /// <summary>
    /// 蓄積方式でスタミナ消費量を計算する純粋関数。小数点分は次フレームに持ち越す。
    /// </summary>
    internal static (int amountToConsume, float remainingAccumulator) CalculateStaminaDrain(
        float accumulator, float drainPerSecond, float deltaTime)
    {
        float updated = accumulator + drainPerSecond * deltaTime;
        int whole = Mathf.FloorToInt(updated);
        return (whole, updated - whole);
    }

    internal static bool IsDashRequested(bool dashButtonToggled, bool keyboardHeld)
    {
        return dashButtonToggled || keyboardHeld;
    }

    /// <summary>
    /// プレイヤーがインタラクトボタンをクリックしたときの処理
    /// インタラクトアクション(話しかけるなど)が設定されていればそれを実行し、なければインタラクトアイテムを使用する
    /// </summary>
    public void ActInteract()
    {
        if (_interactAction != null)
        {
            _interactAction.Action();
        }
        else
        {
            ItemManager.Instance.quickItems[ItemManager.Instance.selectedQuickItemIndex]?.Item.Use(gameObject);
        }
    }

    // トリガーイベント
    #region Collider Events
    public void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<InteractAction>() == null) return;
        _interactAction = other.GetComponent<InteractAction>();
        _interactAction?.ShowInteractTarget();
    }

    public void OnTriggerExit2D(Collider2D other)
    {
        var interactAction = other.GetComponent<InteractAction>();
        if (interactAction == null) return;
        if (interactAction.Equals(_interactAction))
        {
            _interactAction?.HideInteractTarget();
            _interactAction = null;
        }
    }
    #endregion
}
