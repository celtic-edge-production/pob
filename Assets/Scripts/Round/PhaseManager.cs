using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PhaseManager : NetworkBehaviour
{
    // Directly use the NetworkVariable for the current phase
    [SerializeField] private NetworkVariable<GamePhase> _currentPhase = new NetworkVariable<GamePhase>(GamePhase.Phase1);

    [SerializeField] private Phase1 phase1;
    [SerializeField] private Phase2 phase2;
    [SerializeField] private EndPhase endGame;

    private Spawner _Spawner;

    // Enum Class that define phase of the game
    public enum GamePhase
    {
        Phase1,
        Phase2,
        EndPhase
    }

    private void Awake()
    {
        _currentPhase.OnValueChanged += HandlePhaseChanged;
    }

    private void Start()
    {
        if (!IsOwner) return;

        _Spawner = Object.FindFirstObjectByType<Spawner>();

        StartCoroutine(StartFirstPhaseOfTheGame());
    }

    private IEnumerator StartFirstPhaseOfTheGame()
    {
        yield return new WaitForSeconds(3f);

        if (_Spawner != null)
            _Spawner.ResetCharactersPosition();
        phase1.StartPhaseServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Server)]
    public void SwitchGamePhaseServerRpc()
    {
        if (!IsServer) return;

        if (_currentPhase.Value == GamePhase.Phase1)
        {
            _currentPhase.Value = GamePhase.Phase2;
        }
        else if (_currentPhase.Value == GamePhase.Phase2)
        {
            // Must run while the characters are still active (end of phase 2): Spawner only
            // finds active characters, and they are hidden for the whole phase 1.
            if (_Spawner != null)
                _Spawner.ResetCharactersPosition();

            _currentPhase.Value = GamePhase.Phase1;
        }
    }

    private void HandlePhaseChanged(GamePhase oldPhase, GamePhase newPhase)
    {
        if (!IsServer) return;

        switch (newPhase)
        {
            case GamePhase.Phase1:
                phase1.StartPhaseServerRpc();
                break;
            case GamePhase.Phase2:
                phase2.StartPhaseServerRpc();
                break;
            case GamePhase.EndPhase:
                if (endGame != null)
                    endGame.StartPhaseServerRpc();
                else
                    Debug.LogError("[PhaseManager] EndPhase reference is not assigned.");
                break;
        }
    }

    public override void OnDestroy()
    {
        _currentPhase.OnValueChanged -= HandlePhaseChanged;
        base.OnDestroy();
    }

    // Server only. EndPhase is final: SwitchGamePhaseServerRpc ignores it.
    public void EndGame()
    {
        if (!IsServer)
            return;

        _currentPhase.Value = GamePhase.EndPhase;
    }
}
