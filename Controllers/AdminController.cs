using Campsite.Data;
using Campsite.Models;
using Campsite.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Campsite.Controllers;
[Authorize(Roles = "Administrator")]
public sealed class AdminController(ICampRepository camp) : Controller
{
    public async Task<IActionResult> Index() { var stats = await camp.GetStatsAsync(); return View(new AdminViewModel { Reservations = await camp.GetReservationsAsync(), Customers = await camp.GetCustomersAsync(), CustomerCount = stats.Customers, PendingRevenue = stats.PendingRevenue, Activities = await camp.GetActivitiesAsync(), Menu = await camp.GetMenuAsync(), Theme = await camp.GetThemeAsync() }); }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Status(int id, string status) { if (status is "Pending" or "Confirmed" or "Cancelled") await camp.UpdateReservationStatusAsync(id, status); return RedirectToAction(nameof(Index)); }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> AddActivity(Activity item) { if (!string.IsNullOrWhiteSpace(item.Title) && item.StartsAt > DateTime.UtcNow) await camp.AddActivityAsync(item); return RedirectToAction(nameof(Index)); }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> DeleteActivity(int id) { await camp.DeleteActivityAsync(id); return RedirectToAction(nameof(Index)); }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> AddMenu(MenuItem item) { if (!string.IsNullOrWhiteSpace(item.Name) && item.Price >= 0) await camp.AddMenuItemAsync(item); return RedirectToAction(nameof(Index)); }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> UpdateMenuOrder(int id, int sortOrder) { if (sortOrder > 0) await camp.UpdateMenuOrderAsync(id, sortOrder); return RedirectToAction(nameof(Index)); }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> ReorderMenu([FromBody] List<MenuOrderUpdate> items)
    {
        if (items.Count == 0 || items.Count > 500 || items.Any(x => x.Id <= 0 || x.SortOrder <= 0) || items.Select(x => x.Id).Distinct().Count() != items.Count) return BadRequest();
        await camp.ReorderMenuAsync(items); return Ok();
    }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> DeleteMenu(int id) { await camp.DeleteMenuItemAsync(id); return RedirectToAction(nameof(Index)); }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Theme(ThemeSettings theme) { if (new[] { theme.Primary, theme.Forest, theme.Earth, theme.Sand, theme.Ember }.All(x => System.Text.RegularExpressions.Regex.IsMatch(x ?? "", "^#[0-9a-fA-F]{6}$"))) await camp.SaveThemeAsync(theme); return RedirectToAction(nameof(Index)); }
}
