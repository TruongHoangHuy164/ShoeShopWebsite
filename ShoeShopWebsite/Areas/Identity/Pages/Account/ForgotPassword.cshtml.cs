// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShoeShopWebsite.Models;

namespace ShoeShopWebsite.Areas.Identity.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ForgotPasswordModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            public string OTP { get; set; }

            [DataType(DataType.Password)]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            public string NewPassword { get; set; }

            [DataType(DataType.Password)]
            [Compare("NewPassword", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        public void OnGet()
        {
            // Không cần xử lý gì khi load trang
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostSendOTPAsync([FromForm] string email)
        {
            Console.WriteLine($"OnPostSendOTPAsync called with email: {email}");

            if (string.IsNullOrEmpty(email))
            {
                Console.WriteLine("Email is empty");
                return new JsonResult(new { success = false, message = "Please enter an email" });
            }

            Input = new InputModel { Email = email };
            string otp = new Random().Next(100000, 999999).ToString();
            TempData["OTP"] = otp;
            TempData["OTP_Email"] = Input.Email;
            TempData["OTP_Expiry"] = DateTime.Now.AddMinutes(1);
            Console.WriteLine($"Generated OTP: {otp} for {Input.Email}");

            try
            {
                using (var smtp = new SmtpClient
                {
                    Host = "smtp.gmail.com",
                    Port = 587,
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new System.Net.NetworkCredential("coi31052004@gmail.com", "crtv sqkz epfg hfwj")
                })
                {
                    var message = new MailMessage(
                        "coi31052004@gmail.com",
                        Input.Email,
                        "Password Reset OTP",
                        $"Your OTP is: {otp}. It will expire in 1 minute."
                    );
                    await smtp.SendMailAsync(message);
                    Console.WriteLine($"OTP email sent to {Input.Email}");
                }
                return new JsonResult(new { success = true, message = "OTP has been sent to your email" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SMTP Error: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return new JsonResult(new { success = false, message = $"Failed to send OTP: {ex.Message}" })
                {
                    StatusCode = 500
                };
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Console.WriteLine("OnPostAsync called");

            if (!ModelState.IsValid)
            {
                Console.WriteLine("ModelState is invalid");
                return Page();
            }

            string storedOTP = TempData["OTP"]?.ToString();
            string storedEmail = TempData["OTP_Email"]?.ToString();
            DateTime? expiry = TempData["OTP_Expiry"] as DateTime?;

            Console.WriteLine($"Stored OTP: {storedOTP}, Stored Email: {storedEmail}, Expiry: {expiry}");
            Console.WriteLine($"Input OTP: {Input.OTP}, Input Email: {Input.Email}");

            if (string.IsNullOrEmpty(storedOTP) || storedOTP != Input.OTP ||
                storedEmail != Input.Email || (expiry.HasValue && DateTime.Now > expiry.Value))
            {
                ModelState.AddModelError("Input.OTP", "Invalid or expired OTP");
                Console.WriteLine("OTP validation failed");
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                Console.WriteLine("User not found after OTP validation");
                return RedirectToPage("./Login");
            }

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, resetToken, Input.NewPassword);

            if (result.Succeeded)
            {
                Console.WriteLine("Password reset successful");
                return RedirectToPage("./Login");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
                Console.WriteLine($"Reset error: {error.Description}");
            }

            return Page();
        }
    }
}