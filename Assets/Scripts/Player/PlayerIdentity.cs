using Unity.Netcode;
using UnityEngine;

public class PlayerIdentity : NetworkBehaviour
{

    #region Data Attributes

    [SerializeField]
    public InputReader Input;

    [Header("Player Data")]
    public PlayerData Data;

    public NetworkVariable<bool> IsVoting = new(false);

    #endregion

    #region GameObject intance

    [Header("GameObjects to control")]
    public GameObject Cursor;

    // The character this player controls, written by the server and replicated: every peer knows
    // it, without searching the children (the character is hidden during phase 1).
    private static readonly NetworkObjectReference NoCharacter = new((NetworkObject)null);
    private readonly NetworkVariable<NetworkObjectReference> _character = new(NoCharacter);
    private PlayerCharacter _resolvedCharacter;

    // Known as soon as the value arrives, even before the character itself is resolved.
    public bool HasCharacter => _character.Value.NetworkObjectId != NoCharacter.NetworkObjectId;

    // Null when the player has none, or when this peer has not spawned it yet (retried next call).
    public PlayerCharacter Character
    {
        get
        {
            if (!HasCharacter)
                return null;

            ulong characterId = _character.Value.NetworkObjectId;
            if (_resolvedCharacter == null || _resolvedCharacter.NetworkObjectId != characterId)
                _resolvedCharacter = ResolveCharacter();

            return _resolvedCharacter;
        }
    }

    #endregion

    #region Methods

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Every peer keeps every identity, with its cursor and data, across scene loads.
        // Its character is no longer a child: it persists on its own (PlayerCharacter.ApplyPersistence).
        DontDestroyOnLoad(gameObject);

        // The identity must disappear with its owner. If "Don't Destroy With Owner" were set on
        // the prefab, NGO would hand it (and its behaviour) to the host when the client leaves.
        if (IsServer)
            NetworkObject.DontDestroyWithOwner = false;

        Players.Register(this);

        // Right click (release the character): only acts in the lobby, with a character.
        if (IsOwner && Input != null)
            Input.MouseAction2Event += OnMouseAction2;
    }

    public override void OnNetworkDespawn()
    {
        // Server side, when the owner leaves: its character is kept by NGO (DontDestroyWithOwner),
        // decide its fate here.
        if (IsServer && !NetworkManager.ShutdownInProgress)
        {
            PlayerCharacter character = Character;
            if (character != null)
                FreeCharacterOfLeavingOwner(character);
        }

        // The InputReader is a shared asset that outlives this identity: a forgotten
        // subscription would call into it in the next session (RPC on an unspawned object).
        if (Input != null)
            Input.MouseAction2Event -= OnMouseAction2;

        Players.Unregister(this);

        base.OnNetworkDespawn();
    }

    private PlayerCharacter ResolveCharacter()
    {
        NetworkManager networkManager = NetworkManager != null ? NetworkManager : NetworkManager.Singleton;
        if (networkManager == null || networkManager.SpawnManager == null)
            return null;

        return _character.Value.TryGet(out NetworkObject characterObject, networkManager)
            ? characterObject.GetComponent<PlayerCharacter>()
            : null;
    }

    // Server only.
    private void SetCharacter(PlayerCharacter character)
    {
        _character.Value = character != null ? new NetworkObjectReference(character.NetworkObject) : NoCharacter;
        _resolvedCharacter = character;
    }

    // The identity is being despawned: its character reference goes with it, nothing to clear.
    private void FreeCharacterOfLeavingOwner(PlayerCharacter character)
    {
        // In a game scene, the character leaves with its player. Handing it to the server would
        // make it owned by client 0 (the host) and corrupt the bomb and score logic. The lobby
        // recreates every character when it reloads, so destroying it here is safe.
        if (LobbyManager.Instance == null)
        {
            if (character.IsSpawned)
                character.NetworkObject.Despawn(true);
            return;
        }

        // In the lobby, give the character back so another player can pick it.
        // Runs immediately on the server: IsPlaying = false (goes with the lobby again) + RemoveOwnership().
        if (character.IsSpawned)
            character.ReleaseFromPlayer();

        if (character.TryGetComponent(out LobbyCharacter lobbyCharacter))
            lobbyCharacter.ResetToOriginalPosition();
    }

    public void Init()
    {
        Data = GetComponentInChildren<PlayerData>();
        Cursor = GetComponentInChildren<CursorController>().gameObject;

        Data.PlayerID.OnValueChanged += (oldValue, newValue) => Data.SetColor(newValue);
        Data.PlayerColor.OnValueChanged += (oldValue, newValue) => SetPlayerColor(newValue);

        Cursor.GetComponent<CursorController>().Init();
        Cursor.GetComponent<TilemapHover>().Init();
        Cursor.GetComponent<CursorController>().OnClick += SelectCharacter;
    }

    public void UpdatePlayerData()
    {
        UpdatePlayerDataServerRpc(Data.PlayerColor.Value);
    }

    private void RefreshInitInCaseOfBadSync()
    {
        if (!Data)
            Data = GetComponentInChildren<PlayerData>();
        if (!Cursor)
        {
            Cursor = GetComponentInChildren<CursorController>().gameObject;

            Cursor.GetComponent<CursorController>().Init();
            Cursor.GetComponent<TilemapHover>().Init();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void UpdatePlayerDataServerRpc(Color color)
    {
        RefreshInitInCaseOfBadSync();

        var AU_Cursor = Cursor.GetComponent<CursorController>();

        AU_Cursor.ChangeColor(color);

        UpdatePlayerDataClientRpc(color);
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void UpdatePlayerDataClientRpc(Color color)
    {
        RefreshInitInCaseOfBadSync();

        var AU_Cursor = Cursor.GetComponent<CursorController>();

        AU_Cursor.ChangeColor(color);
    }

    public void SendDataToNewClient(ulong newClientId)
    {
        if (Data)
        {
            SendDataToNewClientRpc(Data.PlayerColor.Value, RpcTarget.Single(newClientId, RpcTargetUse.Temp));
        }
    }

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    private void SendDataToNewClientRpc(Color playerColor, RpcParams rpcParams = default)
    {
        RefreshInitInCaseOfBadSync();

        var AU_Cursor = Cursor.GetComponent<CursorController>();

        AU_Cursor.ChangeColor(playerColor);
        AU_Cursor.SetVisible(!HasCharacter);
    }

    public void SetPlayerID(ulong playerID)
    {
        if (IsOwner)
        {
            Data.PlayerID.Value = playerID;
        }
    }

    public void SetPlayerColor(Color playerColor)
    {
        if (!IsOwner)
            return;
        var AU_Cursor = Cursor.GetComponent<CursorController>();

        AU_Cursor.SetColor(playerColor);
    }

    public void SelectCharacter(CursorController cursor)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return;

        RequestOwnerShipServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestOwnerShipServerRpc()
    {
        if (HasCharacter || Cursor == null)
        {
            return;
        }

        CursorController cursor = Cursor.GetComponent<CursorController>();
        GameObject obj = cursor.HoveredEntity();


        if (obj == null || !obj.CompareTag("Player"))
        {
            return;
        }

        PlayerCharacter character = obj.GetComponent<PlayerCharacter>();

        if (character == null || character.IsPlaying.Value)
        {
            return;
        }

        // IsPlaying = true: from now on the character follows its player into the level.
        SetCharacter(character);
        character.AssignToPlayer(OwnerClientId);

        // Hides the cursor on every peer, the server included (CursorController.HandleIsActiveChanged).
        cursor.OnClick -= SelectCharacter;
        cursor.IsActive.Value = false;
    }

    public void RemoveCharacter()
    {
        // -- Reset position of the character
        ResetPositionServerRpc();

        // -- Remove ownership in Network
        RemoveOwnerShipServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void ResetPositionServerRpc()
    {
        ResetCharacterToLobbySlot();
        ResetPositionClientRpc();
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    public void ResetPositionClientRpc()
    {
        ResetCharacterToLobbySlot();
    }

    private void ResetCharacterToLobbySlot()
    {
        PlayerCharacter character = Character;
        if (character != null && character.TryGetComponent(out LobbyCharacter lobbyCharacter))
            lobbyCharacter.ResetToOriginalPosition();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RemoveOwnerShipServerRpc(RpcParams rpcParams = default)
    {
        if (!RpcValidation.SenderMatches(rpcParams, OwnerClientId))
            return;

        ReleaseCharacter();
    }

    // Server only.
    public void ReleaseCharacter()
    {
        PlayerCharacter character = Character;
        if (character == null)
            return;

        // IsPlaying = false: the character goes with the lobby again.
        character.ReleaseFromPlayer();

        // The server owns it again: only now can it move it back to its slot (the owner's own
        // reset, sent just before, may arrive after the ownership change and do nothing).
        if (character.TryGetComponent(out LobbyCharacter lobbyCharacter))
            lobbyCharacter.ResetToOriginalPosition();

        SetCharacter(null);

        CursorController cursor = Cursor.GetComponent<CursorController>();

        cursor.OnClick += SelectCharacter;
        cursor.IsActive.Value = true;
    }

    // Phase 1 controls the cursor (trap placement), phase 2 the character.
    // Explicit state instead of a toggle: a missed or repeated call cannot invert it.
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    public void SetPlayingClientRpc(bool playing)
    {
        RefreshInitInCaseOfBadSync();

        PlayerCharacter character = Character;
        if (character == null)
        {
            Debug.LogWarning($"[PlayerIdentity] Player {OwnerClientId} has no character, cannot switch control mode.");
            return;
        }

        Cursor.GetComponent<CursorController>().SetVisible(!playing);
        character.SetVisible(playing);

        if (playing)
            character.ActivateCamera();
    }

    // Every peer, when the lobby opens: characters always shown, the cursor only for a player
    // without one. A game cut short in phase 1 (not enough players) left them the other way round.
    // Runs before SetupPlayer has created the cursor in a new session: nothing to hide then.
    public void ApplyLobbyVisibility()
    {
        PlayerCharacter character = Character;
        if (character != null)
            character.SetVisible(true);

        CursorController cursor = Cursor != null
            ? Cursor.GetComponent<CursorController>()
            : GetComponentInChildren<CursorController>(true);

        if (cursor != null)
            cursor.SetVisible(!HasCharacter);
    }

    #endregion

    #region Input Actions

    public void OnMouseAction2()
    {
        // -- Detect if we are in the lobby (Because if we are not, the LobbyManager is not initialized)
        if (LobbyManager.Instance == null)
            return;

        if (IsOwner && !IsVoting.Value && HasCharacter)
            RemoveCharacter();
    }

    #endregion
}