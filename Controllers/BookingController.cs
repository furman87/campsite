using Campsite.Data;
using Campsite.Models;
using Campsite.Services;
using Campsite.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Campsite.Controllers;
public sealed class BookingController(ICampRepository camp, IPaymentGateway payments) : Controller
{
    [HttpGet] public async Task<IActionResult> Search(SearchInput search)
    {
        if (search.CheckIn is not null && search.CheckOut is not null && search.CheckOut <= search.CheckIn) ModelState.AddModelError("", "Departure must be after arrival.");
        return View(new BookingViewModel { Search = search, Sites = ModelState.IsValid && search.CheckIn is not null ? await camp.GetSitesAsync(search.CheckIn, search.CheckOut, search.Guests) : await camp.GetSitesAsync() });
    }
    [HttpGet] public async Task<IActionResult> Reserve(int siteId, DateOnly? checkIn, DateOnly? checkOut, int guests = 2)
    {
        var site = (await camp.GetSitesAsync()).FirstOrDefault(x => x.Id == siteId); if (site is null) return NotFound();
        ViewBag.Site = site; return View(new BookingInput { CampsiteId = siteId, CheckIn = checkIn, CheckOut = checkOut, Guests = guests });
    }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Reserve(BookingInput input)
    {
        if (input.PaymentMethod is not ("stripe" or "paypal")) ModelState.AddModelError("PaymentMethod", "Choose card or PayPal.");
        if (!ModelState.IsValid) { ViewBag.Site = (await camp.GetSitesAsync()).FirstOrDefault(x => x.Id == input.CampsiteId); return View(input); }
        try
        {
            var id = await camp.CreateReservationAsync(input); var reservation = await camp.GetReservationAsync(id) ?? throw new InvalidOperationException();
            var paymentReturn = Url.Action(nameof(Complete), "Booking", new { id, provider = input.PaymentMethod }, Request.Scheme)!;
            var success = input.PaymentMethod == "stripe" ? paymentReturn + "&session_id={CHECKOUT_SESSION_ID}" : paymentReturn;
            var cancel = Url.Action(nameof(Cancelled), "Booking", new { id }, Request.Scheme)!;
            return Redirect(await payments.StartAsync(reservation, success, cancel));
        }
        catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); ViewBag.Site = (await camp.GetSitesAsync()).FirstOrDefault(x => x.Id == input.CampsiteId); return View(input); }
    }
    [HttpGet] public async Task<IActionResult> Complete(int id, string provider, string? session_id, string? token)
    {
        var paid = provider == "stripe" ? await payments.VerifyStripeAsync(session_id ?? "", id) : provider == "paypal" && !string.IsNullOrWhiteSpace(token) && await payments.CapturePayPalAsync(token);
        if (paid) await camp.SetPaymentAsync(id, provider, "Paid", session_id ?? token);
        ViewBag.Paid = paid; return View(await camp.GetReservationAsync(id));
    }
    [HttpGet] public IActionResult Cancelled(int id) { ViewBag.ReservationId = id; return View(); }
}
