using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace EnergyApp.API.Hubs
{
    [Authorize]
    public class MaintenanceHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var uid = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? Context.User?.FindFirstValue("sub")
                      ?? string.Empty;
            var role = (Context.User?.FindFirstValue(ClaimTypes.Role) ?? string.Empty).Trim().ToLowerInvariant();
            var departmentId = Context.User?.FindFirstValue("DepartmentId") ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(uid))
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{uid}");

            if (!string.IsNullOrWhiteSpace(role))
                await Groups.AddToGroupAsync(Context.ConnectionId, $"role:{role}");

            if (!string.IsNullOrWhiteSpace(departmentId))
                await Groups.AddToGroupAsync(Context.ConnectionId, $"department:{departmentId}");

            await base.OnConnectedAsync();
        }

        public Task JoinRequestGroup(string requestId)
        {
            return Groups.AddToGroupAsync(Context.ConnectionId, $"request:{requestId}");
        }

        public Task LeaveRequestGroup(string requestId)
        {
            return Groups.RemoveFromGroupAsync(Context.ConnectionId, $"request:{requestId}");
        }
    }
}
