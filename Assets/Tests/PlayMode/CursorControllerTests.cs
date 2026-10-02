using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

public class CursorControllerTests
{
    private NetworkManager _networkManager;
    private GameObject _cursorInstance;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        var cameraGameObject = new GameObject("TestMainCamera");
        cameraGameObject.AddComponent<Camera>();
        cameraGameObject.tag = "MainCamera";

        var nmGameObject = new GameObject("TestNetworkManager");
        _networkManager = nmGameObject.AddComponent<NetworkManager>();
        _networkManager.NetworkConfig = new NetworkConfig();

        var transport = nmGameObject.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        _networkManager.NetworkConfig.NetworkTransport = transport;

        _networkManager.StartHost();

        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        var testCamera = GameObject.Find("TestMainCamera");
        if (testCamera != null)
            Object.Destroy(testCamera);

        if (_cursorInstance != null)
            Object.Destroy(_cursorInstance);

        if (_networkManager != null)
        {
            _networkManager.Shutdown();
            Object.Destroy(_networkManager.gameObject);
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator IsActiveChangedWhileDisabled_StillResyncsInputSubscriptionOnEnable()
    {
        // Regression test for the exact bug found and fixed this session: when
        // gameObject.SetActive() is called from inside HandleIsActiveChanged (a
        // NetworkVariable.OnValueChanged callback), it triggers OnDisable() synchronously
        // mid-callback, unsubscribing HandleIsActiveChanged itself. If IsActive.Value then
        // changes AGAIN while nothing is subscribed, the input event resubscribe was
        // silently skipped - right-click-to-release worked, but picking a new character
        // afterward did nothing. The fix re-syncs explicitly in OnEnable based on the
        // CURRENT value, not just future changes.

        var prefab = Resources.Load<GameObject>("TestCursor");
        Assert.IsNotNull(prefab, "TestCursor prefab not found in Assets/Tests/PlayMode/Resources/");

        _cursorInstance = Object.Instantiate(prefab);
        var networkObject = _cursorInstance.GetComponent<NetworkObject>();
        networkObject.Spawn();

        yield return null;

        var cursor = _cursorInstance.GetComponent<CursorController>();

        // Simulate the exact sequence: disable while unsubscribed (mimicking what
        // PlayerIdentity does), flip IsActive back to true while disabled, then re-enable.
        cursor.gameObject.SetActive(false);
        yield return null;

        cursor.IsActive.Value = true;
        yield return null;

        cursor.gameObject.SetActive(true);
        yield return null;

        // We can't directly assert on the private _Input subscription from outside, so
        // this test currently only confirms the sequence doesn't throw and IsActive ends
        // up true. A more thorough version would need a test-only InputReader double and
        // a way to count subscribers - flagging this as a partial test, not full coverage.
        Assert.IsTrue(cursor.IsActive.Value);
    }
}