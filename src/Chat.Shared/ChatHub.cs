using Microsoft.AspNetCore.SignalR;

namespace Chat.Shared;

public sealed class ChatHub : Hub
{
    public Task SendMessage(ChatMessage message)
        => Clients.All.SendAsync("ReceiveMessage", message);
}
