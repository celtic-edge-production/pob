using Unity.Netcode;

// Generic base class for NetworkBehaviour singletons.
// Consolidates the duplicate-instance guard and the OnDestroy cleanup that was
// rewritten by hand 6 times this session (and initially done wrong once, missing
// "override" + base.OnDestroy(), which silently broke NGO's own internal cleanup).
public abstract class NetworkSingleton<T> : NetworkBehaviour where T : NetworkSingleton<T>
{
    public static T Instance { get; private set; }

    protected virtual void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = (T)this;
        OnSingletonAwake();
    }

    // Override this instead of Awake() in derived classes for singleton-specific setup
    // (camera lookups, extra NetworkVariable init, DontDestroyOnLoad, etc).
    // Guaranteed to run only once the duplicate-instance check above has passed.
    protected virtual void OnSingletonAwake() { }

    public override void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        base.OnDestroy();
    }
}