using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class PlayerManagerTests
{
    private GameObject _networkManagerObject;
    private GameObject _playerIdentityObject;
    private GameObject _menuControllerObject;
    private GameObject _playerManagerObject;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _networkManagerObject = new GameObject("TestNetworkManager");
        var networkManager = _networkManagerObject.AddComponent<NetworkManager>();
        networkManager.NetworkConfig = new NetworkConfig();
        var transport = _networkManagerObject.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        networkManager.NetworkConfig.NetworkTransport = transport;
        networkManager.StartHost();

        yield return null;

        // Stand-ins for the two leftover-GameObject bugs fixed this session: a stale
        // PlayerIdentity (DontDestroyOnLoad survivor) and a stale MenuController
        // (PersistentSingleton survivor), both of which used to leak into Main Menu
        // after disconnecting.
        _playerIdentityObject = new GameObject("TestPlayerIdentity");
        _playerIdentityObject.AddComponent<PlayerIdentity>();

        _menuControllerObject = new GameObject("TestMenuController");
        _menuControllerObject.AddComponent<MenuController>();

        _playerManagerObject = new GameObject("TestPlayerManager");
        _playerManagerObject.AddComponent<PlayerManager>();

        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        // Best-effort cleanup - Disconnect() itself destroys most of these, so some of
        // these calls are expected to no-op against already-destroyed objects.
        if (_playerManagerObject != null) Object.Destroy(_playerManagerObject);
        if (_menuControllerObject != null) Object.Destroy(_menuControllerObject);
        if (_playerIdentityObject != null) Object.Destroy(_playerIdentityObject);
        if (_networkManagerObject != null) Object.Destroy(_networkManagerObject);

        yield return null;
    }

    [UnityTest]
    public IEnumerator Disconnect_DestroysStalePlayerIdentityAndMenuController()
    {
        var playerManager = _playerManagerObject.GetComponent<PlayerManager>();

        playerManager.Disconnect();

        // DisconnectRoutine yields one frame before destroying anything, plus we need a
        // couple more for Destroy() calls to actually take effect.
        yield return null;
        yield return null;
        yield return null;

        Assert.IsTrue(_networkManagerObject == null, "Stale NetworkManager GameObject should have been destroyed.");
        Assert.IsTrue(_playerIdentityObject == null, "Stale PlayerIdentity GameObject should have been destroyed.");
        Assert.IsTrue(_menuControllerObject == null, "Stale MenuController GameObject should have been destroyed.");
    }
}