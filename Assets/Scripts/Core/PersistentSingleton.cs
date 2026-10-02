using UnityEngine;

// Plain-MonoBehaviour equivalent of NetworkSingleton<T>, for singletons that persist
// across scenes via DontDestroyOnLoad but aren't NetworkBehaviour (MenuController,
// and potentially PlayerIdentity - see note below).
public abstract class PersistentSingleton<T> : MonoBehaviour where T : PersistentSingleton<T>
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
        DontDestroyOnLoad(gameObject);
        OnSingletonAwake();
    }

    protected virtual void OnSingletonAwake() { }

    protected virtual void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}