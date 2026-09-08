using System.Security.Claims;
using Campsite.Data;
using Campsite.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace Campsite.Controllers;
public sealed class AccountController(ICampRepository camp, PasswordService passwords) : Controller
{
    [HttpGet] public IActionResult Login(string? returnUrl) { ViewBag.ReturnUrl = returnUrl; return View(); }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Login(string username, string password, string? returnUrl)
    {
        var user = await camp.FindAdminAsync(username); if (user is null || !passwords.Verify(password, user.Value.PasswordHash)) { ModelState.AddModelError("", "Invalid username or password."); ViewBag.ReturnUrl = returnUrl; return View(); }
        var identity = new ClaimsIdentity([new(ClaimTypes.Name, user.Value.Username), new(ClaimTypes.Role, "Administrator")], CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity)); return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? "/Admin" : returnUrl);
    }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Logout() { await HttpContext.SignOutAsync(); return RedirectToAction("Index", "Home"); }
}
