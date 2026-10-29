using System;
using TMPro;
using Unity.Netcode;
using UnityEngine;

// What every peer sees of a round: the message in the middle of the screen, the scoreboard and
// the switch back to the shared camera. Server-called RPCs only; no game rules here.
// Lives next to GameRoundManager and ScoreKeeper.
public class RoundDisplay : NetworkBehaviour
{
    [SerializeField] private TMP_Text _messageText;
    [SerializeField] private Scoreboard _scoreboard;

    // Server only: the host's scoreboard has finished its closing animation (PhaseManager waits for it).
    public event Action ScoreboardClosed;

    private ScoreKeeper _scores;
    private Camera _mainCamera;

    private void Awake()
    {
        _scores = GetComponent<ScoreKeeper>();
        _mainCamera = Camera.main;

        if (_messageText == null || _scoreboard == null)
            Debug.LogError("[RoundDisplay] Message Text and Scoreboard must be assigned in the Inspector.", this);
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    public void ShowMessageClientRpc(string message)
    {
        _messageText.gameObject.SetActive(true);
        _messageText.text = message;
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    public void HideMessageClientRpc()
    {
        _messageText.gameObject.SetActive(false);
    }

    // Every peer: back to the shared main camera, and off with this peer's own character camera.
    // Only the owner can switch it off (PlayerCharacter.DeactivateCamera checks IsOwner): done on
    // the server, it only reached the host's character, and every client kept two AudioListeners.
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    public void ResetCameraClientRpc()
    {
        if (_mainCamera != null)
        {
            _mainCamera.gameObject.SetActive(true);
            MainCameraController mainCameraController = _mainCamera.GetComponent<MainCameraController>();
            if (mainCameraController != null)
                mainCameraController.ResetCamera();
        }

        // After the main camera is back: DeactivateCamera never leaves a peer without a camera.
        PlayerCharacter localCharacter = Players.Local != null ? Players.Local.Character : null;
        if (localCharacter != null)
            localCharacter.DeactivateCamera();
    }

    // Called by PhaseManager after a delay: the scores travel through NetworkVariables (sent on the
    // next network tick), not through this RPC, and must reach every client first.
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    public void ShowScoreboardClientRpc(ulong[] survivorIds)
    {
        for (int i = 0; i < _scores.PlayerIds.Count; i++)
        {
            ulong playerId = _scores.PlayerIds[i];
            PlayerCharacter character = Players.TryGet(playerId, out PlayerIdentity player) ? player.Character : null;

            if (character == null)
            {
                Debug.LogWarning($"[RoundDisplay] No character found for player {playerId}.");
                continue;
            }

            Sprite sprite = character.GetComponent<SpriteRenderer>().sprite;

            // Same index as PlayerIds, so the image stays aligned with the score and status columns.
            if (sprite != null)
                _scoreboard.PlayerImage[i].sprite = sprite;
        }

        _scoreboard.SetUpPlayerStateImages(survivorIds);
        _scoreboard.UpdateDisplayScore();
        _messageText.gameObject.SetActive(false);
        _scoreboard.gameObject.SetActive(true);
        _scoreboard.SwitchScoreboardState();
    }

    // Every peer, when its scoreboard has closed: only the host's counts.
    public void OnScoreboardClosed()
    {
        if (IsServer)
            ScoreboardClosed?.Invoke();
    }
}
