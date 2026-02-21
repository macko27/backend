using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Security.Claims;

namespace AGILE2024_BE
{
    public class NotificationHub : Hub
    {
        //Toto je SignalR Hub — teda real-time kanál medzi backendom a frontendom. Hub je ako:
        //„serverová chat miestnosť“ Frontend sa pripojí a server mu vie okamžite poslať správu bez refreshu.
        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, userId);
                Console.WriteLine($"User {userId} connected with ConnectionId: {Context.ConnectionId}");
            }
             
            await base.OnConnectedAsync();
        }
    }
}
