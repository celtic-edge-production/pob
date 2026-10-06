using Unity.Netcode;
using UnityEngine;

public class ChestUi : NetworkBehaviour
{
    private Animator _Animator;

    private void Awake()
    {
        _Animator = GetComponent<Animator>();
        _Animator.SetBool("IsOpening", false);
        _Animator.SetBool("IsToggle", false);
    }

    // Animation event, end of the opening animation. Server only.
    public void OnUIOpen()
    {
        if (!IsServer)
            return;

        TrapPlacementManager.Instance.CreateTrapsForSelection();
    }

    // Animation event, end of the closing animation. Server only.
    public void OnUIClose()
    {
        if (!IsServer)
            return;

        TrapPlacementManager.Instance.ToggleSelectedTrap();
        _Animator.SetBool("IsToggle", false);
        CloseChest();
    }

    // Server only: called once every player has chosen a trap.
    public void StartClosingAnimation()
    {
        _Animator.SetBool("IsOpening", false);
        _Animator.SetBool("IsToggle", true);
    }

    private void CloseChest()
    {
        if (TrapChest.Current == null)
        {
            Debug.LogError("[ChestUi] No chest spawned.");
            return;
        }

        TrapChest.Current.GetComponent<Animator>().SetBool("Open", false);
    }
}