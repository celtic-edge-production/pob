using Steamworks;
using UnityEngine;

// Initialises the Steamworks API once, when the game starts.
// Must sit on a GameObject of the very first scene loaded (Main Menu), with DontDestroyOnLoad
// so it survives the scene changes.
public class SteamManager : MonoBehaviour
{
    private static bool _initialized;
    public static bool Initialized => _initialized;

    private void Awake()
    {
        if (_initialized)
        {
            // Already initialised by an earlier instance (scene change): this one is a duplicate.
            Destroy(gameObject);
            return;
        }

        try
        {
            if (!SteamAPI.Init())
            {
                Debug.LogError("Échec de l'initialisation de Steamworks. Le jeu doit être lancé via Steam, " +
                               "ou un fichier steam_appid.txt valide doit être présent à la racine du projet en mode Éditeur.");
                return;
            }
        }
        catch (System.DllNotFoundException e)
        {
            Debug.LogError("Bibliothèque Steamworks introuvable (steam_api.dll manquant ou Steam non lancé) : " + e.Message);
            return;
        }

        _initialized = true;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (_initialized)
            SteamAPI.RunCallbacks();
    }

    private void OnApplicationQuit()
    {
        if (_initialized)
        {
            SteamAPI.Shutdown();
            _initialized = false;
        }
    }
}
