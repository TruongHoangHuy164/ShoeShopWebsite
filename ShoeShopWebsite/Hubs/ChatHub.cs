using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using ShoeShopWebsite.Models;

namespace ShoeShopWebsite.Hubs
{
    [Authorize] // Yêu cầu đăng nhập
    public class ChatHub : Hub
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ChatHub(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task SendMessage(string message)
        {
            var sender = await _userManager.GetUserAsync(Context.User);
            if (sender == null)
            {
                throw new HubException("User not found or not authenticated.");
            }

            var senderRoles = await _userManager.GetRolesAsync(sender);
            var senderRole = senderRoles.FirstOrDefault();
            var allUsers = _userManager.Users.ToList();
            var targetUsers = new List<string>();

            if (senderRole == SD.Role_Customer)
            {
                targetUsers = allUsers
                    .Where(u => _userManager.IsInRoleAsync(u, SD.Role_Employee).Result ||
                               _userManager.IsInRoleAsync(u, SD.Role_Admin).Result)
                    .Select(u => u.Id)
                    .ToList();
            }
            else if (senderRole == SD.Role_Admin)
            {
                targetUsers = allUsers
                    .Where(u => _userManager.IsInRoleAsync(u, SD.Role_Employee).Result)
                    .Select(u => u.Id)
                    .ToList();
            }
            else if (senderRole == SD.Role_Employee)
            {
                targetUsers = allUsers
                    .Where(u => _userManager.IsInRoleAsync(u, SD.Role_Customer).Result)
                    .Select(u => u.Id)
                    .ToList();
            }

            // Gửi tin nhắn đến targetUsers và chính người gửi
            await Clients.Users(targetUsers).SendAsync("ReceiveMessage", sender.UserName, message);
            await Clients.Caller.SendAsync("ReceiveMessage", sender.UserName, message); // Gửi lại cho người gửi
        }
    }
}