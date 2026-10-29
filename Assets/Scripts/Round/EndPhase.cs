using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class EndPhase : APhase
{
    [Tooltip("How long the winner is shown before going back to the lobby (waited by PhaseManager).")]
    [SerializeField] private float _displayDuration = 5f;

    public float DisplayDuration => _displayDuration;

    public override void Begin()
    {
        foreach (PlayerCharacter character in Players.Characters)
            character.GetComponent<PlayerBomb>().Revive();

        TrapPlacementManager.Instance.DespawnAllTraps();

        // Built on the server: character names are the same on every peer.
        string message = BuildWinnerMessage(GameRoundManager.Instance.Scores.GetWinnerIds());
        GameRoundManager.Instance.Display.ShowMessageClientRpc(message);
    }

    private static string BuildWinnerMessage(ulong[] winnerIds)
    {
        if (winnerIds.Length == 0)
            return "No winner this time!";

        string names = string.Join(" & ", winnerIds.Select(Players.NameOf));

        return winnerIds.Length == 1
            ? $"{names} is the real King of Pirates!"
            : $"{names} share the crown of the Pirates!";
    }
}
