using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class BombManager : NetworkSingleton<BombManager>
{
    [SerializeField] private AudioClip _BackgroundMusic;
    [SerializeField] private AudioClip _WaitingMusic;
    private AudioSource _AudioSource;
    public TMP_Text timerText;

    public float bombTimer = 60f;
    public NetworkVariable<ulong> bombHolderId = new NetworkVariable<ulong>(ulong.MaxValue);
    private ulong oldBombHolderId = ulong.MaxValue;
    private float countdown = 0f;
    private bool timerActive = false;
    private bool firstTransfer = true;
    private bool isBackgroundMusicPlaying = false;

    public bool IsRoundRunning => timerActive;

    private void Start()
    {
        _AudioSource = GetComponent<AudioSource>();

        timerText.gameObject.SetActive(false);

        PlayMusic(_WaitingMusic, 1.0f);

        if (IsServer)
        {
            bombHolderId.Value = ulong.MaxValue;
            countdown = bombTimer;
        }
    }

    private void Update()
    {
        if (!IsServer || !timerActive)
            return;

        if (!isBackgroundMusicPlaying)
            PlayBackgroundMusic();
        else
            UpdateBackgroundMusicSpeed();

        countdown -= Time.deltaTime;

        if (timerText != null)
            timerText.text = Mathf.CeilToInt(countdown).ToString();

        if (countdown <= 10f && countdown > 0f)
            timerText.gameObject.SetActive(true);

        if (countdown <= 0)
            ExplodeBombHolder();
    }

    private void UpdateBackgroundMusicSpeed()
    {
        if (countdown >= 10f)
            return;

        float newPitch = Mathf.Lerp(1.0f, 2.0f, (10f - countdown) / 10f);
        UpdateMusicPitchClientRpc(newPitch);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void UpdateMusicPitchClientRpc(float pitch)
    {
        if (_AudioSource != null)
            _AudioSource.pitch = pitch;
    }

    private void PlayBackgroundMusic()
    {
        if (_BackgroundMusic == null)
        {
            Debug.LogWarning("No background music assigned.");
            return;
        }

        isBackgroundMusicPlaying = true;
        PlayMusicClientRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayMusicClientRpc()
    {
        PlayMusic(_BackgroundMusic, 1.0f);
    }

    private void StopBackgroundMusic()
    {
        if (_BackgroundMusic == null)
        {
            Debug.LogWarning("No background music assigned.");
            return;
        }

        StartWaitingMusicClientRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void StartWaitingMusicClientRpc()
    {
        isBackgroundMusicPlaying = false;
        PlayMusic(_WaitingMusic, 1.0f);
    }

    private void PlayMusic(AudioClip clip, float pitch)
    {
        _AudioSource.clip = clip;
        _AudioSource.loop = true;
        _AudioSource.playOnAwake = true;
        _AudioSource.volume = 0.5f;
        _AudioSource.pitch = pitch;

        _AudioSource.Play();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void TransferBombServerRpc(ulong newHolderId, RpcParams rpcParams = default)
    {
        // The check here isn't "does sender match newHolderId" - that's the player
        // RECEIVING the bomb, not the one calling. The real requirement is "does the
        // real sender currently hold the bomb" - this also fixes the same redundant-call
        // pattern seen elsewhere this session, since OnCollisionEnter2D (the caller, in
        // TransferManager.cs) has no IsOwner guard and could otherwise fire from every
        // client who locally sees the collision, not just the actual holder.
        if (!RpcValidation.SenderMatches(rpcParams, bombHolderId.Value))
            return;

        AssignBombHolder(newHolderId);
    }

    public void AssignBombHolder(ulong newHolderId)
    {
        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(newHolderId))
            return;

        NetworkObject newHolder = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(newHolderId);

        if (!CanTransferBombTo(newHolder, newHolderId))
            return;

        if (firstTransfer)
        {
            firstTransfer = false;
            ResetCountdown();
        }

        if (!TryReleaseCurrentHolder())
            return;

        GiveBombTo(newHolder, newHolderId);

        oldBombHolderId = bombHolderId.Value;
        bombHolderId.Value = newHolderId;
    }

    private bool CanTransferBombTo(NetworkObject newHolder, ulong newHolderId)
    {
        TransferManager transferManager = newHolder?.GetComponentInChildren<TransferManager>();
        bool isInvincible = transferManager != null && transferManager.IsPlayerInvincible();

        bool canTransfer = CanTransfer(bombHolderId.Value, newHolderId, isInvincible);

        if (!canTransfer && bombHolderId.Value == newHolderId)
            Debug.LogWarning("Player already has the bomb!");

        return canTransfer;
    }

    // Returns false if the current holder is stunned and the transfer must be aborted.
    private bool TryReleaseCurrentHolder()
    {
        NetworkObject currentHolder = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(bombHolderId.Value);
        TransferManager currentHolderTransferManager = currentHolder?.GetComponentInChildren<TransferManager>();
        PlayerCharacter currentHolderPlayerController = currentHolder?.GetComponentInChildren<PlayerCharacter>();

        if (currentHolderPlayerController == null)
        {
            Debug.LogWarning("Current bomb holder player controller not found: " + bombHolderId.Value);
            return true; // No holder to release (e.g. first transfer) - not an error.
        }

        if (currentHolderPlayerController.isStun.Value)
        {
            Debug.LogWarning("Current bomb holder is stunned and cannot transfer the bomb.");
            return false;
        }

        currentHolderPlayerController.hasBomb.Value = false;

        if (currentHolderTransferManager != null)
            currentHolderTransferManager.OnBombTransferred();
        else
            Debug.LogWarning("Current bomb holder not found: " + bombHolderId.Value);

        return true;
    }

    private void GiveBombTo(NetworkObject newHolder, ulong newHolderId)
    {
        PlayerCharacter newHolderPlayerController = newHolder?.GetComponentInChildren<PlayerCharacter>();

        if (newHolderPlayerController != null)
            newHolderPlayerController.hasBomb.Value = true;
        else
            Debug.LogWarning("New bomb holder not found: " + newHolderId);
    }

    private void ResetCountdown()
    {
        List<TransferManager> alivePlayers = new List<TransferManager>();
        foreach (var player in GameRoundManager.Instance.players)
        {
            if (player.IsAlive.Value)
                alivePlayers.Add(player);
        }

        int divisor = MathUtils.SafeDivisor(alivePlayers.Count);
        countdown = bombTimer / divisor + Random.Range(0f, 5f);

        timerActive = true;
    }

    private void ExplodeBombHolder()
    {
        NetworkObject player = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(bombHolderId.Value);

        if (player != null && player.gameObject != null)
        {
            TransferManager currentHolder = player.gameObject.GetComponentInChildren<TransferManager>();

            if (currentHolder != null)
                currentHolder.MarkAsDead();
            else
                Debug.LogWarning("TransferManager not found on player.");
        }

        // StopRound() resets oldBombHolderId, read it first.
        ulong assistId = oldBombHolderId;

        StopRound();

        if (assistId != ulong.MaxValue)
            GameRoundManager.Instance.AddRoundScore(assistId, 2, "Assist");

        GameRoundManager.Instance.CheckForRoundEnd();
    }

    private void DespawnActiveBombs()
    {
        BombProjectile[] activeBombs = FindObjectsByType<BombProjectile>(FindObjectsSortMode.None);

        foreach (BombProjectile bombProjectile in activeBombs)
        {
            NetworkObject networkObject = bombProjectile.GetComponent<NetworkObject>();

            if (networkObject != null && IsServer)
                networkObject.Despawn();
        }
    }

    public static bool CanTransfer(ulong currentHolderId, ulong newHolderId, bool newHolderIsInvincible)
    {
        if (newHolderIsInvincible)
            return false;

        if (currentHolderId == newHolderId)
            return false;

        return true;
    }

    public void HandlePlayerLeft(ulong clientId)
    {
        // No assist points for a player who left.
        if (oldBombHolderId == clientId)
            oldBombHolderId = ulong.MaxValue;

        if (!timerActive || bombHolderId.Value != clientId)
            return;

        // The holder left with the bomb: give it to a random alive player.
        // The countdown keeps running (firstTransfer is false while the timer is active).
        List<TransferManager> candidates = new();
        foreach (var player in GameRoundManager.Instance.players)
        {
            if (player.IsAlive.Value && !player.IsPlayerInvincible())
                candidates.Add(player);
        }

        bombHolderId.Value = ulong.MaxValue;

        if (candidates.Count > 0)
            AssignBombHolder(candidates[Random.Range(0, candidates.Count)].OwnerClientId);
    }

    public void StopRound()
    {
        timerText.gameObject.SetActive(false);
        timerActive = false;
        firstTransfer = true;
        bombHolderId.Value = ulong.MaxValue;
        oldBombHolderId = ulong.MaxValue;

        DespawnActiveBombs();
        StopBackgroundMusic();
    }

    public bool IsBombHolder(ulong clientId)
    {
        return bombHolderId.Value == clientId;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ResetBombServerRpc()
    {
        bombHolderId.Value = ulong.MaxValue;
    }
}