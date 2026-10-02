using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerData : NetworkBehaviour {

    public NetworkVariable<ulong> PlayerID                 = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<FixedString32Bytes> PlayerName  = new("Player", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<Color> PlayerColor              = new(Color.white, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public void SetColor(ulong playerID)
    {
        PlayerColor.Value = Utils.ColorTools.GetColorByID((int)playerID);
    }
}
