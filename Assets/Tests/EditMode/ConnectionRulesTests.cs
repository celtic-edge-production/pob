using NUnit.Framework;

public class ConnectionRulesTests
{
    [Test]
    public void RefusalReason_RoomLeftInLobby_IsNull()
    {
        Assert.IsNull(ConnectionRules.RefusalReason(connectedPlayers: 3, maxPlayers: 4, gameInProgress: false));
    }

    [Test]
    public void RefusalReason_LobbyFull_SaysSo()
    {
        Assert.AreEqual(ConnectionRules.LobbyFullReason(4),
            ConnectionRules.RefusalReason(connectedPlayers: 4, maxPlayers: 4, gameInProgress: false));
    }

    [Test]
    public void RefusalReason_GameInProgress_RefusedEvenWithRoomLeft()
    {
        Assert.AreEqual(ConnectionRules.GameInProgressReason,
            ConnectionRules.RefusalReason(connectedPlayers: 2, maxPlayers: 4, gameInProgress: true));
    }

    [Test]
    public void RefusalReason_GameInProgressAndFull_GivesTheGameReason()
    {
        Assert.AreEqual(ConnectionRules.GameInProgressReason,
            ConnectionRules.RefusalReason(connectedPlayers: 4, maxPlayers: 4, gameInProgress: true));
    }

    [Test]
    public void LobbyFullReason_ShowsTheLimit()
    {
        StringAssert.Contains("4", ConnectionRules.LobbyFullReason(4));
    }
}
