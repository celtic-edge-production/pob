using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyManager : NetworkSingleton<LobbyManager>
{

    #region Attributes

    [Header("List of Scene Preview (to select)")]
    [SerializeField]
    private ScenePreview[] _ScenePreviews;

    [Header("List of selected levels")]
    [SerializeField]
    private List<string> _SelectedLevels = new();

    [Header("Countdown Timer")]
    /*
     * @brief Clock used to countdown the time when players are ready to play, and choose a level.
     */
    private Clock _ChoosingLevelClock = new(0);

    private NetworkVariable<float> _NetworkedTimer = new();

    [SerializeField]
    private float _TimeIfEveryPlayerIsReady = 3f;  // In seconds ( 3 seconds)

    [SerializeField]
    private float _TimeIfHalfPlayerIsReady = 20f; // In seconds (20 seconds)

    [SerializeField]
    private ClockUI _ClockUI;

    [SerializeField]
    private TMP_Text _MessageText;

    #endregion

    #region Methods

    protected override void OnSingletonAwake() { }

    public void Start()
    {
        _ScenePreviews = FindObjectsByType<ScenePreview>(FindObjectsSortMode.None);

        foreach (ScenePreview scenePreview in _ScenePreviews)
        {
            scenePreview.OnLevelSelected += HandleLevelSelected;
            scenePreview.OnLevelDeSelected += HandleLevelDeSelected;
        }

        _ChoosingLevelClock.OnTimerOver += LaunchLevel;

        _NetworkedTimer.OnValueChanged += (float oldValue, float newValue) => {
            _ClockUI.SetTime(newValue);
        };

        if (IsHost || IsServer)
        {
            DisplayRoomCode();
        }
        ResetLobby();
    }

    public void Update()
    {
        if (IsServer) {
            _ChoosingLevelClock.Update();

            _NetworkedTimer.Value = _ChoosingLevelClock.GetTimer();
        }

        if (_ClockUI != null)
            _ClockUI.SetTime(_NetworkedTimer.Value);
    }

    private void ResetLobby()
    {
        PlayerCleaner playerCleaner = Object.FindFirstObjectByType<PlayerCleaner>();

        if (playerCleaner != null)
            playerCleaner.RemoveDuplicatedCharacters();

        LobbyCharacter[] characters = FindObjectsByType<LobbyCharacter>(FindObjectsSortMode.None);

        foreach (LobbyCharacter character in characters)
            character.ResetToOriginalPosition();
    }

    /*
     * @brief Callback function, called when the timer is over.
     * It launch one of the different level chosen by the players.
     */
    private void LaunchLevel()
    {
        string levelName = string.Empty;

        if (_SelectedLevels.Count > 1) {
            int randomIndex = Random.Range(0, _SelectedLevels.Count); // -- Choose a random level between the selected levels

            levelName = _SelectedLevels[randomIndex]; // -- Get the selected level (string name)
        } else {
            levelName = _SelectedLevels[0]; // -- Get the only selected level (index = 0)
        }

        // -- Unload the lobby
        UnloadLobby();
        
        // -- Load the level chosen by the players
        //SceneTransactionManager.Instance.ChangeSceneServerRpc(levelName);
        ChangeSceneServerRpc(levelName);
    }

    [Rpc(SendTo.Server)]
    public void ChangeSceneServerRpc(string sceneName)
    {
        if (!IsServer)
            return;
        NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    /*
     * @brief Callback function, called when the lobby is over.
     * It release the resources used by the lobby.
     */
    private void UnloadLobby()
    {
        // -- Remove the listeners of the ScenePreviews
        for (int i = 0; i < _ScenePreviews.Length; i++) {
            _ScenePreviews[i].Unbind();

            _ScenePreviews[i].OnLevelSelected   -= HandleLevelSelected;
            _ScenePreviews[i].OnLevelDeSelected -= HandleLevelDeSelected;
        }
    }

    /*
     * @brief Callback function, called when a pressure plate is pressed.
     * It add the level to the list of selected levels.
     * And launch the game if all the pressure plates are pressed.
     */
    private void HandleLevelSelected(GameObject obj, string levelName)
    {
        if (!IsSpawned)
            return;
        Transform parentT = obj.transform.parent;

        if (parentT == null)
            return;
        GameObject parent = parentT.gameObject;

        if (parent == null)
            return;
        NetworkObject playerNetworkObject = parent.GetComponentInParent<NetworkObject>();

        if (playerNetworkObject != null)
            HandleLevelSelectedServerRpc(playerNetworkObject.NetworkObjectId, levelName);
    }

    [Rpc(SendTo.Server)]
    private void HandleLevelSelectedServerRpc(ulong objID, string levelName)
    {
        NetworkObject playerNetworkObject = NetworkManager.Singleton.SpawnManager.SpawnedObjects[objID];

        if (playerNetworkObject != null)
        {
            PlayerIdentity playerController = playerNetworkObject.GetComponent<PlayerIdentity>();

            if (playerController != null)
                playerController.IsVoting.Value = true;
        }

        _SelectedLevels.Add(levelName);

        CheckPressurePlates();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void HandleLevelDeSelectedServerRpc(ulong objID, string levelName)
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.SpawnManager == null)
            return;
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objID, out NetworkObject playerNetworkObject) || playerNetworkObject == null)
            return;
        if (playerNetworkObject.TryGetComponent(out PlayerIdentity playerController))
        {
            if (playerController != null)
                playerController.IsVoting.Value = false;
            if (_SelectedLevels == null)
                return;

            if (_SelectedLevels.Contains(levelName))
                _SelectedLevels.Remove(levelName);

            try
            {
                CheckPressurePlates();
            }
            catch (System.Exception e)
            {
                Debug.LogError(e.Message);
            }
        }
    }

    /*
     * @brief Check if all the pressure plates are pressed.
     * If it's the case, launch the game.
     */
    private void HandleLevelDeSelected(GameObject obj, string levelName)
    {
        if (!IsSpawned)
            return;
        Transform parentT = obj.transform.parent;

        if (parentT == null)
            return;
        GameObject parent = parentT.gameObject;

        if (parent == null)
            return;
        NetworkObject playerNetworkObject = parent.GetComponentInParent<NetworkObject>();

        if (playerNetworkObject != null)
            HandleLevelDeSelectedServerRpc(playerNetworkObject.NetworkObjectId, levelName);
    }

    /*
     * @brief Function that start the timer of the countdown, for loading the game.
     */
    private void CheckPressurePlates()
    {
        int activatedPlates = _SelectedLevels.Count; // -- Number of player that choose a level

        // Get the number of player with the networkManager
        int playerCount = NetworkManager.Singleton.ConnectedClientsList.Count;

        // If half of the players are ready, start the countdown
        if (activatedPlates >= playerCount / 2 && _SelectedLevels.Count != 0 && playerCount > 1 && IsEveryonePlayed()) {
            if (activatedPlates == playerCount)
                StartTimerClientRpc(_TimeIfEveryPlayerIsReady); // -- Everyone is ready to play
            else
                StartTimerClientRpc(_TimeIfHalfPlayerIsReady); // -- Half of the players are ready to play

            _ChoosingLevelClock.Start(); // -- Start the countdown
        } else {
            // -- Stop the countdown, no enough players are ready
            StopTimerClientRpc();
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void StartTimerClientRpc(float time)
    {
        _ClockUI.gameObject.SetActive(true);
        _ClockUI.enabled = true;

        _ChoosingLevelClock.SetTimer(time);

        if (IsOwner)
            _NetworkedTimer.Value = time;
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void StopTimerClientRpc()
    {
        _ClockUI.gameObject.SetActive(false);
        _ClockUI.enabled = false;

        _ChoosingLevelClock.Stop();

        if (IsOwner)
            _NetworkedTimer.Value = 0;
    }

    public bool IsEveryonePlayed()
    {
        PlayerIdentity[] players = FindObjectsByType<PlayerIdentity>(FindObjectsSortMode.None);

        foreach (PlayerIdentity player in players) {
            if (player.HaveCharacters.Value == false)
                return false;
        }
        return true;
    }

    private void DisplayRoomCode()
    {
        if (!string.IsNullOrEmpty(SteamLobbySession.RoomCode))
        {
            _MessageText.text = $"Lobby Code: {SteamLobbySession.RoomCode}";
        }
        else if (!string.IsNullOrEmpty(EOSLobbySession.RoomCode))
        {
            _MessageText.text = $"Lobby Code: {EOSLobbySession.RoomCode}";
        }
        else if (NetworkBackendSelector.Current == BackendType.Local)
        {
            _MessageText.text = "Lobby local (127.0.0.1)";
        }
        else
        {
            Debug.LogWarning("Aucun code de lobby disponible à afficher.");
        }
    }

    public override void OnNetworkDespawn()
    {
        // LaunchLevel() only runs on the server, so clients never unbound the pressure plates.
        // Unbind on every peer when the lobby goes away (unsubscribing twice is harmless).
        UnloadLobby();
        base.OnNetworkDespawn();
    }

    #endregion
}
