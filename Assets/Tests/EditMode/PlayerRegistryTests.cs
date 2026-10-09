using NUnit.Framework;

public class PlayerRegistryTests
{
    private class FakePlayer
    {
        public readonly string Name;
        public FakePlayer(string name) { Name = name; }
    }

    private PlayerRegistry<FakePlayer> _registry;

    [SetUp]
    public void SetUp()
    {
        _registry = new PlayerRegistry<FakePlayer>();
    }

    [Test]
    public void Register_ThenTryGet_FindsThePlayerByClientId()
    {
        FakePlayer host = new FakePlayer("host");
        FakePlayer client = new FakePlayer("client");

        _registry.Register(0, host, isLocal: true);
        _registry.Register(1, client, isLocal: false);

        Assert.IsTrue(_registry.TryGet(1, out FakePlayer found));
        Assert.AreSame(client, found);
        Assert.AreEqual(2, _registry.Count);
    }

    [Test]
    public void TryGet_UnknownClientId_ReturnsFalse()
    {
        _registry.Register(0, new FakePlayer("host"), isLocal: true);

        Assert.IsFalse(_registry.TryGet(7, out FakePlayer found));
        Assert.IsNull(found);
    }

    [Test]
    public void Register_Local_IsExposedAsLocal()
    {
        FakePlayer me = new FakePlayer("me");

        _registry.Register(0, new FakePlayer("host"), isLocal: false);
        _registry.Register(3, me, isLocal: true);

        Assert.AreSame(me, _registry.Local);
    }

    [Test]
    public void Unregister_RemovesThePlayerAndClearsLocal()
    {
        FakePlayer me = new FakePlayer("me");
        _registry.Register(2, me, isLocal: true);

        _registry.Unregister(2, me);

        Assert.IsFalse(_registry.Contains(2));
        Assert.AreEqual(0, _registry.All.Count);
        Assert.IsNull(_registry.Local);
    }

    [Test]
    public void Unregister_AnotherInstance_KeepsTheRegisteredOne()
    {
        FakePlayer stale = new FakePlayer("stale");
        FakePlayer live = new FakePlayer("live");

        _registry.Register(1, stale, isLocal: true);
        _registry.Register(1, live, isLocal: true);

        // The stale object despawns after its replacement has registered.
        _registry.Unregister(1, stale);

        Assert.IsTrue(_registry.TryGet(1, out FakePlayer found));
        Assert.AreSame(live, found);
        Assert.AreSame(live, _registry.Local);
    }

    [Test]
    public void Register_SameClientIdTwice_ReplacesWithoutDuplicate()
    {
        FakePlayer first = new FakePlayer("first");
        FakePlayer second = new FakePlayer("second");

        _registry.Register(4, first, isLocal: false);
        _registry.Register(4, second, isLocal: false);

        Assert.AreEqual(1, _registry.Count);
        Assert.AreSame(second, _registry.All[0]);
    }

    [Test]
    public void Register_ReplacingTheLocalPlayerByARemoteOne_ClearsLocal()
    {
        FakePlayer local = new FakePlayer("local");
        _registry.Register(0, local, isLocal: true);

        _registry.Register(0, new FakePlayer("remote"), isLocal: false);

        Assert.IsNull(_registry.Local);
    }

    [Test]
    public void All_KeepsSpawnOrder()
    {
        FakePlayer a = new FakePlayer("a");
        FakePlayer b = new FakePlayer("b");
        FakePlayer c = new FakePlayer("c");

        _registry.Register(5, a, isLocal: false);
        _registry.Register(1, b, isLocal: false);
        _registry.Register(9, c, isLocal: false);
        _registry.Unregister(1, b);

        Assert.AreEqual(2, _registry.All.Count);
        Assert.AreSame(a, _registry.All[0]);
        Assert.AreSame(c, _registry.All[1]);
    }

    [Test]
    public void Register_Null_IsIgnored()
    {
        _registry.Register(0, null, isLocal: true);

        Assert.AreEqual(0, _registry.Count);
        Assert.IsNull(_registry.Local);
    }

    [Test]
    public void Clear_EmptiesEverything()
    {
        _registry.Register(0, new FakePlayer("host"), isLocal: true);
        _registry.Register(1, new FakePlayer("client"), isLocal: false);

        _registry.Clear();

        Assert.AreEqual(0, _registry.Count);
        Assert.IsFalse(_registry.Contains(0));
        Assert.IsNull(_registry.Local);
    }
}