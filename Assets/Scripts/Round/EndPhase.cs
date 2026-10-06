using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class EndPhase : APhase
{
    [SerializeField] private float _displayDuration = 5f;

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Server)]
    public override void StartPhaseServerRpc()
    {
        foreach (var player in FindObjectsByType<TransferManager>(FindObjectsSortMode.None))
            player.Revive();

        TrapPlacementManager.Instance.DespawnAllTraps();

        // Built on the server: character names are the same on every peer.
        string message = BuildWinnerMessage(GameRoundManager.Instance.GetWinnerIds());
        GameRoundManager.Instance.ShowGameMessageClientRpc(message);

        StartCoroutine(GameRoundManager.Instance.ReturnToLobby(_displayDuration));
    }

    private static string BuildWinnerMessage(ulong[] winnerIds)
    {
        if (winnerIds.Length == 0)
            return "No winner this time!";

        string names = string.Join(" & ", winnerIds.Select(GetCharacterName));

        return winnerIds.Length == 1
            ? $"{names} is the real King of Pirates!"
            : $"{names} share the crown of the Pirates!";
    }

    private static string GetCharacterName(ulong clientId)
    {
        PlayerIdentity identity = FindObjectsByType<PlayerIdentity>(FindObjectsSortMode.None)
            .FirstOrDefault(p => p.OwnerClientId == clientId);

        return identity != null && identity.Characters != null
            ? identity.Characters.name
            : $"Player {clientId}";
    }
}