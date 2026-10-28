using Steamworks;
using UnityEngine;

// Initialise l'API Steamworks une seule fois au lancement du jeu.
// Doit être présent sur un GameObject dans la toute première scène chargée
// (Main Menu), avec DontDestroyOnLoad pour survivre aux changements de scène.
public class SteamManager : MonoBehaviour
{
    private static bool _initialized;
    public static bool Initialized => _initialized;

    private void Awake()
    {
        if (_initialized)
        {
            // Déjà initialisé par une instance précédente (changement de scène) : doublon, on se détruit.
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
