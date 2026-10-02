using UnityEngine;
using Unity.Netcode;
using System;

public class NetworkPressurePlate : NetworkBehaviour {

    public event Action<GameObject, bool> OnPressed;
    public       PressurePlate            PressurePlate;

    private void Start()
    {
        PressurePlate = GetComponent<PressurePlate>();

        PressurePlate.OnPressed += OnPressurePlatePressed;
    }

    public void Unbind()
    {
        PressurePlate.OnPressed -= OnPressurePlatePressed;
    }

    private void OnPressurePlatePressed(GameObject obj, bool isPressed)
    {
        // The plate can fire while the lobby is being torn down (character leaving it on unload).
        if (!IsSpawned)
            return;

        if (IsServer)
            PressPlateClientRpc(isPressed);
        else
            PressPlateServerRpc(isPressed);

        OnPressed?.Invoke(obj, isPressed);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PressPlateClientRpc(bool isPressed)
    {
        PressurePlate.UpdatePlateState(isPressed);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PressPlateServerRpc(bool isPressed)
    {
        PressPlateClientRpc(isPressed);
    }
}
