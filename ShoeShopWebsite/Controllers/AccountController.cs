using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeShopWebsite.Models;
using System.Threading.Tasks;

namespace ShoeShopWebsite.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly NikeShopDbContext _context;

        public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, NikeShopDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByNameAsync(model.Username);
                if (user != null && await _userManager.CheckPasswordAsync(user, model.Password))
                {
                    var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: false);
                    if (result.Succeeded)
                    {
                        // Không cần đồng bộ giỏ hàng với UserId nữa
                        // Giữ nguyên SessionId hiện tại để quản lý giỏ hàng
                        var currentSessionId = HttpContext.Session.GetString("CartSessionId");
                        if (string.IsNullOrEmpty(currentSessionId))
                        {
                            // Nếu chưa có SessionId, tạo mới
                            currentSessionId = Guid.NewGuid().ToString();
                            HttpContext.Session.SetString("CartSessionId", currentSessionId);
                        }

                        // Không cần cập nhật Cart với UserId, chỉ cần giữ SessionId
                        Console.WriteLine($"User {model.Username} logged in with SessionId: {currentSessionId}");

                        return RedirectToAction("Index", "Home");
                    }
                }
                ModelState.AddModelError("", "Đăng nhập thất bại.");
            }
            return View(model);
        }

        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }


        public class LoginViewModel
        {
            public string Username { get; set; }
            public string Password { get; set; }
            public bool RememberMe { get; set; }
        }
    }
}