using Unity.Netcode;
using UnityEngine.SceneManagement;
using UnityEngine;

public class SceneManagerNetwork : NetworkBehaviour
{
    [SerializeField] private string m_LobbySceneName;

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Server)]
    public void LoadSceneRpc(string sceneName)
    {
        if (!IsServer) return;

        var status = NetworkManager.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        if (status != SceneEventProgressStatus.Started)
        {
            Debug.LogWarning($"Failed to load {sceneName} with status: {status}");
        }
    }

    public void LoadLobbyScene()
    {
        if (IsServer)
        {
            LoadSceneRpc(m_LobbySceneName);
        }
        else
        {
            Debug.LogWarning("Only the host can initiate scene loading.");
        }
    }
}

