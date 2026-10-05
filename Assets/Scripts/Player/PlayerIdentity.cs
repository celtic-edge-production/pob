using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerIdentity : NetworkBehaviour
{

    #region Data Attributes

    [HideInInspector]
    public static PlayerIdentity LocalPlayer;

    [SerializeField]
    public InputReader Input;

    [Header("Player Data")]
    public PlayerData Data;

    public NetworkVariable<bool> IsVoting = new(false);

    #endregion

    #region GameObject intance

    [Header("GameObjects to control")]
    public GameObject Cursor;
    public GameObject Characters;

    public NetworkVariable<bool> HaveCharacters = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    #endregion

    #region Methods

    public void Awake()
    {
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Every peer (not only the owner) must keep every identity across scene loads.
        // Otherwise, when the lobby unloads, NGO despawns the in-scene character parented to a
        // non-persistent identity (DestroyWithScene is true for in-scene placed objects).
        DontDestroyOnLoad(gameObject);

        // The identity must disappear with its owner. If "Don't Destroy With Owner" were set on
        // the prefab, NGO would hand it (and its behaviour) to the host when the client leaves.
        if (IsServer)
            NetworkObject.DontDestroyWithOwner = false;
    }

    public override void OnNetworkDespawn()
    {
        // Server side, when the owner leaves: NGO has already moved the character to the root,
        // but it is still owned by the leaving client. Decide its fate before NGO does.
        if (IsServer && !NetworkManager.ShutdownInProgress && Characters != null)
            FreeCharacterOfLeavingOwner();

        base.OnNetworkDespawn();
    }

    private void FreeCharacterOfLeavingOwner()
    {
        PlayerCharacter character = Characters.GetComponent<PlayerCharacter>();

        // In a game scene, the character leaves with its player. Handing it to the server would
        // make it owned by client 0 (the host) and corrupt the bomb and score logic. The lobby
        // recreates every character when it reloads, so destroying it here is safe.
        if (LobbyManager.Instance == null)
        {
            if (character != null && character.IsSpawned)
                character.NetworkObject.Despawn(true);

            Characters = null;
            return;
        }

        // In the lobby, give the character back so another player can pick it.
        // Runs immediately on the server: IsPlaying = false + RemoveOwnership().
        if (character != null && character.IsSpawned)
            character.RemoveOwnerShipServerRpc();

        if (Characters.transform.parent != null)
            Characters.transform.SetParent(null);

        // Leave DontDestroyOnLoad so the character is cleaned up with the current scene.
        SceneManager.MoveGameObjectToScene(Characters, SceneManager.GetActiveScene());

        if (Characters.TryGetComponent(out LobbyCharacter lobbyCharacter))
            lobbyCharacter.ResetToOriginalPosition();

        Characters = null;
    }

    public void Init()
    {
        if (IsOwner)
        {
            if (LocalPlayer != null && LocalPlayer != this)
                Destroy(LocalPlayer.gameObject);

            LocalPlayer = this;
        }

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
        if (!Cursor) {
            Cursor = GetComponentInChildren<CursorController>().gameObject;

            Cursor.GetComponent<CursorController>().Init();
            Cursor.GetComponent<TilemapHover>().Init();
        }
    }

    [Rpc(SendTo.Server)]
    private void UpdatePlayerDataServerRpc(Color color)
    {
        RefreshInitInCaseOfBadSync();

        var AU_Cursor = Cursor.GetComponent<CursorController>();

        AU_Cursor.ChangeColor(color);

        UpdatePlayerDataClientRpc(color);
    }

    [Rpc(SendTo.ClientsAndHost)]
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

    [Rpc(SendTo.SpecifiedInParams)]
    private void SendDataToNewClientRpc(Color playerColor, RpcParams rpcParams = default)
    {
        RefreshInitInCaseOfBadSync();

        var AU_Cursor = Cursor.GetComponent<CursorController>();

        AU_Cursor.ChangeColor(playerColor);
        AU_Cursor.gameObject.SetActive(!HaveCharacters.Value);
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

    [Rpc(SendTo.Server)]
    public void RequestOwnerShipServerRpc()
    {
        if (Characters != null || Cursor == null)
        {
            return;
        }

        CursorController cursor = Cursor.GetComponent<CursorController>();
        GameObject obj = cursor.HoveredEntity();


        if (obj == null || !obj.CompareTag("Player"))
        {
            return;
        }

        PlayerCharacter player = obj.GetComponent<PlayerCharacter>();

        if (player == null || player.IsPlaying.Value)
        {
            return;
        }

        Characters = obj;
        Characters.transform.SetParent(gameObject.transform);

        player.RequestOwnerShipServerRpc(Data.PlayerID.Value - 1);

        cursor.OnClick -= SelectCharacter;
        cursor.IsActive.Value = false;
        cursor.gameObject.SetActive(false);

        SetCharacterOnClientRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SetCharacterOnClientRpc()
    {
        if (IsOwner) {
            HaveCharacters.Value = true;

            Input.MouseAction2Event += OnMouseAction2;
        }
    }

    public void RemoveCharacter()
    {
        // -- Reset position of the character
        ResetPositionServerRpc();

        // -- Remove ownership in Network
        RemoveOwnerShipServerRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void UnbindClientRpc()
    {
        if (IsOwner && Characters == null) {
            HaveCharacters.Value = false;

            Input.MouseAction2Event -= OnMouseAction2;
        }
    }

    [Rpc(SendTo.Server)]
    public void ResetPositionServerRpc()
    {
        if (Characters && Characters.TryGetComponent(out LobbyCharacter lobbyCharacter))
            lobbyCharacter.ResetToOriginalPosition();
        ResetPositionClientRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void ResetPositionClientRpc()
    {
        if (Characters && Characters.TryGetComponent(out LobbyCharacter lobbyCharacter))
            lobbyCharacter.ResetToOriginalPosition();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RemoveOwnerShipServerRpc(RpcParams rpcParams = default)
    {
        if (!RpcValidation.SenderMatches(rpcParams, OwnerClientId))
            return;

        ReleaseCharacter();
    }

    public void ReleaseCharacter()
    {
        if (Characters == null)
            return;
        Characters.transform.SetParent(null);

        PlayerCharacter player = Characters.GetComponent<PlayerCharacter>();

        player.RemoveOwnerShipServerRpc();

        SceneManager.MoveGameObjectToScene(Characters, SceneManager.GetActiveScene());

        Characters = null;

        CursorController cursor = Cursor.GetComponent<CursorController>();

        cursor.OnClick += SelectCharacter;
        cursor.IsActive.Value = true;
        cursor.gameObject.SetActive(true);

        RemoveOwnerShipClientRpc();
        UnbindClientRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void RemoveOwnerShipClientRpc()
    {
        if (IsOwner)
            HaveCharacters.Value = false;
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void SwitchStatePlayerClientRpc()
    {
        RefreshInitInCaseOfBadSync();

        // "Characters == null" is also true for a destroyed object. Re-resolve from the
        // children, including inactive ones: the character is deactivated during phase 1, and
        // GetComponentInChildren() skips inactive objects by default.
        if (Characters == null)
        {
            PlayerCharacter child = GetComponentInChildren<PlayerCharacter>(true);
            if (child != null)
                Characters = child.gameObject;
        }

        // TEMPORARY diagnostic (ConnectedClientsList is server-only, hence the -1 on clients).
        Debug.Log($"[PlayerIdentity] SwitchState owner={OwnerClientId} isOwner={IsOwner} isServer={IsServer} " +
                  $"hasCharacters={Characters != null} " +
                  $"characterChildren={GetComponentsInChildren<PlayerCharacter>(true).Length} " +
                  $"connected={(IsServer ? NetworkManager.Singleton.ConnectedClientsList.Count : -1)}");

        // This only stops the exception - it does NOT fix the cause. If this warning appears,
        // the real bug is whatever destroyed the character.
        if (Characters == null)
        {
            Debug.LogWarning($"[PlayerIdentity] Player {OwnerClientId} has no live character, skipping the state switch.");
            return;
        }

        //Activate and Deactivate Cursor and Characters
        Cursor.gameObject.SetActive(!Cursor.activeSelf);
        Characters.SetActive(!Characters.activeSelf);
        if (Characters.activeSelf)
            Characters.GetComponent<PlayerCharacter>().ActivateCamera();
    }

    #endregion

    #region Input Actions

    public void OnMouseAction2()
    {
        // -- Detect if we are in the lobby (Because if we are not, the LobbyManager is not initialized)
        if (LobbyManager.Instance) {
            if (IsOwner && !IsVoting.Value) {
                if (HaveCharacters.Value == true)
                    RemoveCharacter();
            }
        }
    }

    #endregion
}
