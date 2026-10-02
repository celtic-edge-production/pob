using Unity.Netcode;
using UnityEngine;

public class EndPhase : APhase
{
    void Start()
    {

    }

    protected override void Update()
    {

    }


    [Rpc(SendTo.Server)]
    public override void StartPhaseServerRpc()
    {
        Debug.Log("End Phase Started");
    }
}
