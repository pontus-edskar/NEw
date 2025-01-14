using Microsoft.AspNetCore.SignalR;
using System.Text.RegularExpressions;

public class ReadyStatusHub : Hub
{
    // Track users connected to a particular game session
    private static Dictionary<int, List<string>> gameSessions = new Dictionary<int, List<string>>();

    // Notify when a user joins a game session
    public async Task JoinGameBoard(int gameSessionId)
    {
        // Ensure the game session exists in the dictionary
        if (!gameSessions.ContainsKey(gameSessionId))
        {
            gameSessions[gameSessionId] = new List<string>();
        }

        // Add the current user's connection to the session
        gameSessions[gameSessionId].Add(Context.ConnectionId);

        // Notify other users in the same session
        await Clients.Group(gameSessionId.ToString()).SendAsync("UserJoined", gameSessions[gameSessionId]);

        // Add the user to the group
        await Groups.AddToGroupAsync(Context.ConnectionId, gameSessionId.ToString());
    }

    // Notify when a user leaves the game session
    public async Task LeaveGameBoard(int gameSessionId)
    {
        if (gameSessions.ContainsKey(gameSessionId))
        {
            gameSessions[gameSessionId].Remove(Context.ConnectionId);
            await Clients.Group(gameSessionId.ToString()).SendAsync("UserLeft", gameSessions[gameSessionId]);
        }
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, gameSessionId.ToString());
    }

    // Notify the first turn assignment
    public async Task NotifyFirstTurnAssigned(int firstPlayerId)
    {
        await Clients.Group(firstPlayerId.ToString()).SendAsync("FirstTurnAssigned", firstPlayerId);
    }

    // Notify card flipped event
    public async Task NotifyCardFlipped(int cardId, bool isFirstCard)
    {
        await Clients.Group(Context.ConnectionId).SendAsync("CardFlipped", cardId, isFirstCard);
    }

    // Notify match result
    public async Task NotifyMatchResult(bool isMatch)
    {
        await Clients.Group(Context.ConnectionId).SendAsync("MatchResult", isMatch);
    }
}
