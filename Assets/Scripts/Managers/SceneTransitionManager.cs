using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class SceneTransitionManager : NetworkSingleton<SceneTransitionManager>
{
    protected override void OnSingletonAwake()
    {
        DontDestroyOnLoad(gameObject);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Server)]
    public void ChangeSceneRpc(string sceneName)
    {
        if (!IsServer)
            return;
        NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}
