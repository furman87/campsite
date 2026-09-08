using Campsite.Models;
namespace Campsite.ViewModels;
public sealed class HomeViewModel { public List<Activity> Activities { get; set; } = []; public List<MenuItem> Menu { get; set; } = []; public List<Campsite.Models.Campsite> Campsites { get; set; } = []; }
public sealed class BookingViewModel { public SearchInput Search { get; set; } = new(); public List<Campsite.Models.Campsite> Sites { get; set; } = []; }
public sealed class AdminViewModel { public List<Reservation> Reservations { get; set; } = []; public List<Customer> Customers { get; set; } = []; public int CustomerCount { get; set; } public decimal PendingRevenue { get; set; } public List<Activity> Activities { get; set; } = []; public List<MenuItem> Menu { get; set; } = []; public ThemeSettings Theme { get; set; } = new(); }
