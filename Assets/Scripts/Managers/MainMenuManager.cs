using UnityEngine;

public class MainMenuManager : MonoBehaviour {

    [Header("Screens")]
    public GameObject TitleScreen;
    public GameObject PlayScreen;
    public GameObject SettingsScreen;

    private void Awake()
    {
        Return();
    }

    public void Play()
    {
        PlayScreen.SetActive(true);

        TitleScreen.SetActive(false);
        SettingsScreen.SetActive(false);
    }

    public void Option()
    {
        SettingsScreen.SetActive(true);

        TitleScreen.SetActive(false);
        PlayScreen.SetActive(false);
    }

    public void Return()
    {
        TitleScreen.SetActive(true);

        PlayScreen.SetActive(false);
        SettingsScreen.SetActive(false);
    }

    public static void ExitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
			UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
