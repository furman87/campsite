using Campsite.Data;
using Campsite.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Campsite.Controllers;
public sealed class HomeController(ICampRepository camp) : Controller
{
    public async Task<IActionResult> Index() => View(new HomeViewModel { Activities = await camp.GetActivitiesAsync(), Menu = await camp.GetMenuAsync(), Campsites = await camp.GetSitesAsync() });
    public IActionResult Rules() => View();
}
