public enum BackendType { Steam, EOS, Local }

// Compile-time default backend. Steam unless the ITCH_BUILD scripting define is set.
public static class BuildBackend
{
    public static BackendType Default
    {
        get
        {
#if ITCH_BUILD
            return BackendType.EOS;
#else
            return BackendType.Steam;
#endif
        }
    }
}