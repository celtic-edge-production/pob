using UnityEngine;
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
using PlayEveryWare.EpicOnlineServices;

// Anonymous "Device ID" login - no Epic account required, matches the itch.io demo
// goal of not forcing players through any launcher/account. Based on a confirmed but
// PARTIAL doc fragment (GetEOSConnectInterface + CreateDeviceIdOptions are confirmed
// real API from the plugin's own FAQ) - the exact login call signature below is my
// best reconstruction, not verified against a complete official example like Steam was.
public class EOSBootstrap : MonoBehaviour
{
    public static bool Initialized { get; private set; }

    private void Start()
    {
        if (EOSManager.Instance == null)
        {
            Debug.LogError("EOSBootstrap: EOSManager not found in scene.");
            return;
        }

        var connectInterface = EOSManager.Instance.GetEOSConnectInterface();

        var createDeviceIdOptions = new CreateDeviceIdOptions()
        {
            DeviceModel = SystemInfo.deviceModel
        };

        connectInterface.CreateDeviceId(ref createDeviceIdOptions, null, OnDeviceIdCreated);
    }

    private void OnDeviceIdCreated(ref CreateDeviceIdCallbackInfo data)
    {
        if (data.ResultCode != Result.Success && data.ResultCode != Result.DuplicateNotAllowed)
        {
            Debug.LogError($"EOSBootstrap: CreateDeviceId failed: {data.ResultCode}");
            return;
        }

        // Fix: passing SystemInfo.deviceUniqueIdentifier (a long hash string) here was
        // rejected with "DisplayName must be 32 characters or less" - this parameter is
        // apparently treated as a display name, not a raw device token as its name
        // suggested. A short, valid placeholder name works instead.
        EOSManager.Instance.StartConnectLoginWithDeviceToken("Player", OnConnectLogin);
    }

    private void OnConnectLogin(LoginCallbackInfo data)
    {
        if (data.ResultCode == Result.Success)
        {
            Debug.Log("EOSBootstrap: Device ID login succeeded.");
            Initialized = true;
        }
        else if (data.ResultCode == Result.InvalidUser)
        {
            // No EOS user exists yet for this device - create one, then retry login once.
            EOSManager.Instance.CreateConnectUserWithContinuanceToken(data.ContinuanceToken, OnUserCreated);
        }
        else
        {
            Debug.LogError($"EOSBootstrap: Connect login failed: {data.ResultCode}");
        }
    }

    private void OnUserCreated(CreateUserCallbackInfo data)
    {
        EOSManager.Instance.StartConnectLoginWithDeviceToken("Player", (LoginCallbackInfo retryData) =>
        {
            if (retryData.ResultCode == Result.Success)
            {
                Debug.Log("EOSBootstrap: Device ID login succeeded after user creation.");
                Initialized = true;
            }
            else
            {
                Debug.LogError($"EOSBootstrap: Retry connect login failed: {retryData.ResultCode}");
            }
        });
    }
}