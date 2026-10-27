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

    // The cursor is hidden through Hideable, never deactivated: a deactivated NetworkObject was
    // skipped by NGO on shutdown, and a hidden client cursor could no longer hear the server.
    [UnityTest]
    public IEnumerator IsActiveFalse_HidesTheCursorButKeepsItActive()
    {
        CursorController cursor = SpawnCursor();
        yield return null;

        cursor.IsActive.Value = false;
        yield return null;

        Assert.IsFalse(cursor.IsVisible, "The cursor should be hidden.");
        Assert.IsTrue(cursor.gameObject.activeSelf, "The cursor must stay active for NGO.");
    }

    // Regression: after a right click (character released), the server sets IsActive back to true
    // and the cursor must show again.
    [UnityTest]
    public IEnumerator IsActiveBackToTrue_ShowsTheCursorAgain()
    {
        CursorController cursor = SpawnCursor();
        yield return null;

        cursor.IsActive.Value = false;
        yield return null;

        cursor.IsActive.Value = true;
        yield return null;

        Assert.IsTrue(cursor.IsVisible, "The cursor should be shown again.");
    }

    private CursorController SpawnCursor()
    {
        var prefab = Resources.Load<GameObject>("TestCursor");
        Assert.IsNotNull(prefab, "TestCursor prefab not found in Assets/Tests/PlayMode/Resources/");

        _cursorInstance = Object.Instantiate(prefab);
        _cursorInstance.GetComponent<NetworkObject>().Spawn();

        return _cursorInstance.GetComponent<CursorController>();
    }
}