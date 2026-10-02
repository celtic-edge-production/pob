using Unity.Netcode;

// IMPORTANT - read before using this on a new RPC:
// rpcParams.Receive.SenderClientId is only populated by NGO's real network dispatch
// (an actual remote client invoking the Rpc). If the same method can ALSO be reached
// via a direct, in-process C# call (e.g. server-side code calling another
// [Rpc(SendTo.Server)] method directly instead of through a real client request),
// SenderClientId will be its default value there, and this check will incorrectly
// reject that legitimate call path. Trace every caller of the RPC first - if any of
// them is a direct in-process call, this helper is NOT safe to use as-is for that RPC.
// (We hit exactly this with PlayerCharacter.RequestOwnerShipServerRpc this session -
// see the comment left in that file.)
public static class RpcValidation
{
    public static bool SenderMatches(RpcParams rpcParams, ulong expectedClientId)
    {
        return rpcParams.Receive.SenderClientId == expectedClientId;
    }
}