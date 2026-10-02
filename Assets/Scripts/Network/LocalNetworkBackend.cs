using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

// Dev-only backend: plain NGO over UnityTransport, no Steam / EOS. Lets you test host + client
// on one machine (Multiplayer Play Mode, two builds) or on a LAN by typing the host's IP.
// NetworkBackendSelector can only pick it in the Editor or in Development Builds.
public class LocalNetworkBackend : MonoBehaviour, INetworkBackend
{
    [SerializeField] private UnityTransport _transport;
    [SerializeField] private ushort _port = 7777;

    public bool IsReady => true;

    public void Host(System.Action<string> onCodeReady, System.Action<string> onError)
    {
        // Listen on all interfaces so a second PC on the LAN can join too.
        _transport.SetConnectionData("127.0.0.1", _port, "0.0.0.0");

        if (NetworkManager.Singleton.StartHost())
            onCodeReady?.Invoke("LOCAL");
        else
            onError?.Invoke("Échec du démarrage de l'hébergement local.");
    }

    // Empty code or "LOCAL" = same machine. Anything else is treated as the host's IP address.
    public void Join(string code, System.Action<string> onError)
    {
        string trimmed = string.IsNullOrWhiteSpace(code) ? "" : code.Trim();
        string address = (trimmed == "" || trimmed.ToUpperInvariant() == "LOCAL") ? "127.0.0.1" : trimmed;

        _transport.SetConnectionData(address, _port);

        if (!NetworkManager.Singleton.StartClient())
            onError?.Invoke("Échec de la connexion locale.");
    }

    public void Disconnect()
    {
        if (NetworkManager.Singleton != null &&
            (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient))
        {
            NetworkManager.Singleton.Shutdown();
        }
    }
}