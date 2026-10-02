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

    public void OnUIOpen()
    {
        if (!IsOwner) return;

        Debug.Log("UiOpen is opened.");

        // Create Random Trap, # : of numbers of client (or more)
        TrapPlacementManager.Instance.CreateMultipleRandomTrapServerRpc();
    }

    public void OnUIClose()
    {
        if (!IsOwner) return;

        Debug.Log("OnUIClose");

        // Destroy non selected trap if there is any    
        TrapPlacementManager.Instance.ToggleSelectedTrap();
        TrapPlacementManager.Instance.DestroyRandomCreatedTrapServerRpc();

        _Animator.SetBool("IsToggle", false);

        CloseChestAnimationTriggerServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void CloseChestAnimationTriggerServerRpc()
    {
        // Find "TrapChest" gameObject
        GameObject trapChest = GameObject.Find("TrapChest(Clone)");
        
        if (trapChest == null)
        {
            Debug.LogError("TrapChest not found.");
            return;
        }

        trapChest.GetComponent<Animator>().SetBool("Open", false);

        // Get parent GameObject
        //GameObject parent = transform.parent.gameObject;
        //parent.GetComponent<Animator>().SetBool("Open", false);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void StartClosingAnimationServerRpc()
    {
        Debug.Log("Start Closing Animation");
        _Animator.SetBool("IsOpening", false);
        _Animator.SetBool("IsToggle", true);
    }
}
