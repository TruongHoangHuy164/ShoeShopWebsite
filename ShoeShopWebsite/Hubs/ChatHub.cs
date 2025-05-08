using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using ShoeShopWebsite.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShoeShopWebsite.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private static readonly ConcurrentDictionary<string, string> _activeCustomers = new();
        private static readonly ConcurrentDictionary<string, string> _activeStaff = new();

        public ChatHub(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        }

        public override async Task OnConnectedAsync()
        {
            var user = await _userManager.GetUserAsync(Context.User);
            if (user == null)
            {
                throw new HubException("User not authenticated.");
            }

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault();

            if (role == SD.Role_Customer)
            {
                _activeCustomers.TryAdd(user.Id, Context.ConnectionId);
                await UpdateCustomerList();
            }
            else if (role == SD.Role_Employee || role == SD.Role_Admin)
            {
                _activeStaff.TryAdd(user.Id, Context.ConnectionId);
                await UpdateStaffList();
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var user = await _userManager.GetUserAsync(Context.User);
            if (user != null)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var role = roles.FirstOrDefault();

                if (role == SD.Role_Customer)
                {
                    _activeCustomers.TryRemove(user.Id, out _);
                    await UpdateCustomerList();
                }
                else if (role == SD.Role_Employee || role == SD.Role_Admin)
                {
                    _activeStaff.TryRemove(user.Id, out _);
                    await UpdateStaffList();
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendPrivateMessage(string receiverId, string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(receiverId)) throw new ArgumentException("Receiver ID is required");
                if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Message cannot be empty");

                var sender = await _userManager.GetUserAsync(Context.User);
                if (sender == null) throw new HubException("Sender not authenticated");

                var senderRoles = await _userManager.GetRolesAsync(sender);
                var senderRole = senderRoles.FirstOrDefault();

                var receiver = await _userManager.FindByIdAsync(receiverId);
                if (receiver == null) throw new HubException("Receiver not found");

                bool isValidReceiver = false;
                if (senderRole == SD.Role_Customer)
                {
                    isValidReceiver = await _userManager.IsInRoleAsync(receiver, SD.Role_Employee) ||
                                    await _userManager.IsInRoleAsync(receiver, SD.Role_Admin);
                }
                else if (senderRole == SD.Role_Employee || senderRole == SD.Role_Admin)
                {
                    isValidReceiver = await _userManager.IsInRoleAsync(receiver, SD.Role_Customer);
                }

                if (!isValidReceiver) throw new HubException("Invalid receiver role");

                // Send to receiver
                await Clients.User(receiverId).SendAsync("ReceivePrivateMessage",
                    sender.Id, sender.UserName, message);

                // Send back to sender for their own UI
                await Clients.Caller.SendAsync("ReceivePrivateMessage",
                    sender.Id, sender.UserName, message);

                // Log the message
                Console.WriteLine($"Message sent from {sender.UserName} to {receiver.UserName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendPrivateMessage: {ex}");
                await Clients.Caller.SendAsync("ReceiveError", ex.Message);
            }
        }

        public async Task GetActiveCustomers()
        {
            try
            {
                var user = await _userManager.GetUserAsync(Context.User);
                if (user == null) throw new HubException("User not authenticated");

                var roles = await _userManager.GetRolesAsync(user);
                var role = roles.FirstOrDefault();

                if (role != SD.Role_Employee && role != SD.Role_Admin)
                    throw new HubException("Unauthorized access to customer list");

                await UpdateCustomerList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetActiveCustomers: {ex}");
                await Clients.Caller.SendAsync("ReceiveError", ex.Message);
            }
        }

        public async Task GetActiveEmployees()
        {
            try
            {
                var user = await _userManager.GetUserAsync(Context.User);
                if (user == null) throw new HubException("User not authenticated");

                var roles = await _userManager.GetRolesAsync(user);
                var role = roles.FirstOrDefault();

                if (role != SD.Role_Customer)
                    throw new HubException("Unauthorized access to employee list");

                await UpdateStaffList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetActiveEmployees: {ex}");
                await Clients.Caller.SendAsync("ReceiveError", ex.Message);
            }
        }

        private async Task UpdateCustomerList()
        {
            var customers = await Task.WhenAll(_activeCustomers.Keys.Select(async customerId =>
            {
                var customer = await _userManager.FindByIdAsync(customerId);
                return customer != null ? new { id = customer.Id, userName = customer.UserName } : null;
            }));

            var customerList = customers.Where(c => c != null).ToList();
            var staffIds = _activeStaff.Keys.ToList();

            if (staffIds.Any())
            {
                await Clients.Users(staffIds).SendAsync("UpdateCustomerList", customerList);
            }
        }

        private async Task UpdateStaffList()
        {
            var employees = await Task.WhenAll(_activeStaff.Keys.Select(async staffId =>
            {
                var staff = await _userManager.FindByIdAsync(staffId);
                return staff != null ? new { id = staff.Id, userName = staff.UserName } : null;
            }));

            var employeeList = employees.Where(e => e != null).ToList();
            var customerIds = _activeCustomers.Keys.ToList();

            if (customerIds.Any())
            {
                await Clients.Users(customerIds).SendAsync("UpdateEmployeeList", employeeList);
            }
        }

        // Static method to access active staff
        public static IEnumerable<string> GetActiveStaffIds()
        {
            return _activeStaff.Keys.ToList();
        }
    }
}