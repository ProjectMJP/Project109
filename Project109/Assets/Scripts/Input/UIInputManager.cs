using UnityEngine;

public class UIInputManager : MonoBehaviour
{
    private static UIInputManager _instance;
    public static UIInputManager instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<UIInputManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("UIInputManager");
                    _instance = go.AddComponent<UIInputManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    private int _uiLockCount = 0;

    public bool IsWorldTouchAllowed => _uiLockCount == 0;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    public void AcquireUILock()
    {
        _uiLockCount++;
        Debug.Log($"[UIInputManager] AcquireUILock. LockCount: {_uiLockCount}");
        EvaluateInputState();
    }

    public void ReleaseUILock()
    {
        _uiLockCount = Mathf.Max(0, _uiLockCount - 1);
        Debug.Log($"[UIInputManager] ReleaseUILock. LockCount: {_uiLockCount}");
        EvaluateInputState();
    }

    private void EvaluateInputState()
    {
        if (PlayerInputController.instance != null)
        {
            if (IsWorldTouchAllowed)
            {
                Debug.Log("[UIInputManager] Enabling world interaction input.");
                PlayerInputController.instance.EnableObjectInteractionInput();
            }
            else
            {
                Debug.Log("[UIInputManager] Disabling world interaction input.");
                PlayerInputController.instance.DisableObjectInteractionInput();
            }
        }
        else
        {
            Debug.LogWarning("[UIInputManager] PlayerInputController instance is null during EvaluateInputState.");
        }
    }
}
