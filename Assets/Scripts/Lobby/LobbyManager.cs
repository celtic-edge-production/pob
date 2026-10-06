using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyManager : NetworkSingleton<LobbyManager>
{
    #region Attributes

    [Header("Level previews (one pressure plate each)")]
    [SerializeField]
    private ScenePreview[] _ScenePreviews;

    [Header("Countdown Timer")]
    [SerializeField]
    private float _TimeIfEveryPlayerIsReady = 3f;

    [SerializeField]
    private float _TimeIfHalfPlayerIsReady = 20f;

    [SerializeField]
    private ClockUI _ClockUI;

    [SerializeField]
    private TMP_Text _MessageText;

    // One vote per player (client id -> level name): moving to another plate replaces the vote.
    private readonly Dictionary<ulong, string> _votes = new();

    // Countdown before loading the voted level. Runs on the server, shown to everyone via _NetworkedTimer.
    private Clock _ChoosingLevelClock = new(0);

    private NetworkVariable<float> _NetworkedTimer = new();

    #endregion

    #region Lifecycle

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

        _NetworkedTimer.OnValueChanged += (oldValue, newValue) => _ClockUI.SetTime(newValue);

        if (IsServer)
            DisplayRoomCode();

        ResetLobby();
    }

    public void Update()
    {
        if (IsServer)
        {
            _ChoosingLevelClock.Update();
            _NetworkedTimer.Value = _ChoosingLevelClock.GetTimer();
        }

        if (_ClockUI != null)
            _ClockUI.SetTime(_NetworkedTimer.Value);
    }

    public override void OnNetworkDespawn()
    {
        // LaunchLevel() only runs on the server, so clients never unbound the pressure plates.
        // Unbind on every peer when the lobby goes away (unsubscribing twice is harmless).
        UnloadLobby();
        base.OnNetworkDespawn();
    }

    private void ResetLobby()
    {
        PlayerCleaner playerCleaner = FindFirstObjectByType<PlayerCleaner>();

        if (playerCleaner != null)
            playerCleaner.RemoveDuplicatedCharacters();

        foreach (LobbyCharacter character in FindObjectsByType<LobbyCharacter>(FindObjectsSortMode.None))
            character.ResetToOriginalPosition();
    }

    private void UnloadLobby()
    {
        foreach (ScenePreview scenePreview in _ScenePreviews)
        {
            scenePreview.Unbind();
            scenePreview.OnLevelSelected -= HandleLevelSelected;
            scenePreview.OnLevelDeSelected -= HandleLevelDeSelected;
        }
    }

    #endregion

    #region Votes

    private void HandleLevelSelected(GameObject character, string levelName)
    {
        ReportVote(character, levelName, true);
    }

    private void HandleLevelDeSelected(GameObject character, string levelName)
    {
        ReportVote(character, levelName, false);
    }

    // Runs on every peer (each one sees the plate collision): only the voter's own machine reports.
    private void ReportVote(GameObject character, string levelName, bool selected)
    {
        if (!IsSpawned || character.transform.parent == null)
            return;

        NetworkObject voter = character.transform.parent.GetComponentInParent<NetworkObject>();
        if (voter == null || !voter.IsOwner)
            return;

        if (selected)
            HandleLevelSelectedServerRpc(voter.NetworkObjectId, levelName);
        else
            HandleLevelDeSelectedServerRpc(voter.NetworkObjectId, levelName);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void HandleLevelSelectedServerRpc(ulong voterObjectId, string levelName, RpcParams rpcParams = default)
    {
        if (!TryGetVoter(voterObjectId, rpcParams, out PlayerIdentity voter))
            return;

        voter.IsVoting.Value = true;
        _votes[voter.OwnerClientId] = levelName;
        CheckPressurePlates();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void HandleLevelDeSelectedServerRpc(ulong voterObjectId, string levelName, RpcParams rpcParams = default)
    {
        if (!TryGetVoter(voterObjectId, rpcParams, out PlayerIdentity voter))
            return;

        voter.IsVoting.Value = false;

        // Only drop the vote for this plate: the player may already stand on another one.
        if (_votes.TryGetValue(voter.OwnerClientId, out string votedLevel) && votedLevel == levelName)
            _votes.Remove(voter.OwnerClientId);

        CheckPressurePlates();
    }

    // A player may only vote for himself.
    private bool TryGetVoter(ulong voterObjectId, RpcParams rpcParams, out PlayerIdentity voter)
    {
        voter = null;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(voterObjectId, out NetworkObject voterObject))
            return false;

        if (!RpcValidation.SenderMatches(rpcParams, voterObject.OwnerClientId))
            return false;

        return voterObject.TryGetComponent(out voter);
    }

    // Server only. Starts or stops the countdown depending on how many players voted.
    private void CheckPressurePlates()
    {
        // Votes of players who left no longer count.
        foreach (ulong clientId in _votes.Keys.ToList())
        {
            if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId))
                _votes.Remove(clientId);
        }

        int votes = _votes.Count;
        int playerCount = NetworkManager.Singleton.ConnectedClientsList.Count;

        // At least half of the players voted (and everyone has a character): start the countdown.
        if (votes > 0 && votes >= playerCount / 2 && playerCount > 1 && IsEveryonePlayed())
        {
            StartTimerClientRpc(votes == playerCount ? _TimeIfEveryPlayerIsReady : _TimeIfHalfPlayerIsReady);
            _ChoosingLevelClock.Start();
        }
        else
        {
            StopTimerClientRpc();
        }
    }

    public bool IsEveryonePlayed()
    {
        foreach (PlayerIdentity player in FindObjectsByType<PlayerIdentity>(FindObjectsSortMode.None))
        {
            if (!player.HaveCharacters.Value)
                return false;
        }
        return true;
    }

    #endregion

    #region Countdown and level loading

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void StartTimerClientRpc(float time)
    {
        _ClockUI.gameObject.SetActive(true);
        _ClockUI.enabled = true;

        _ChoosingLevelClock.SetTimer(time);

        if (IsServer)
            _NetworkedTimer.Value = time;
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void StopTimerClientRpc()
    {
        _ClockUI.gameObject.SetActive(false);
        _ClockUI.enabled = false;

        _ChoosingLevelClock.Stop();

        if (IsServer)
            _NetworkedTimer.Value = 0;
    }

    // Server only: called when the countdown ends.
    private void LaunchLevel()
    {
        if (_votes.Count == 0)
            return;

        // A random level among the voted ones (several votes for the same level weigh more).
        List<string> votedLevels = _votes.Values.ToList();
        string levelName = votedLevels[Random.Range(0, votedLevels.Count)];

        UnloadLobby();
        ChangeSceneServerRpc(levelName);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Server)]
    public void ChangeSceneServerRpc(string sceneName)
    {
        NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    private void DisplayRoomCode()
    {
        if (!string.IsNullOrEmpty(SteamLobbySession.RoomCode))
            _MessageText.text = $"Lobby Code: {SteamLobbySession.RoomCode}";
        else if (!string.IsNullOrEmpty(EOSLobbySession.RoomCode))
            _MessageText.text = $"Lobby Code: {EOSLobbySession.RoomCode}";
        else if (NetworkBackendSelector.Current == BackendType.Local)
            _MessageText.text = "Lobby local (127.0.0.1)";
        else
            Debug.LogWarning("[LobbyManager] No lobby code to display.");
    }

    #endregion
}