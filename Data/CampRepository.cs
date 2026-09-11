using Campsite.Models;
using Dapper;
using Npgsql;

namespace Campsite.Data;

public interface ICampRepository
{
    Task<List<Campsite.Models.Campsite>> GetSitesAsync(DateOnly? checkIn = null, DateOnly? checkOut = null, int guests = 1);
    Task<List<Activity>> GetActivitiesAsync(); Task<List<MenuItem>> GetMenuAsync(); Task<List<Reservation>> GetReservationsAsync(); Task<List<Customer>> GetCustomersAsync();
    Task<int> CreateReservationAsync(BookingInput input); Task<Reservation?> GetReservationAsync(int id); Task UpdateReservationStatusAsync(int id, string status);
    Task<(int Customers, decimal PendingRevenue)> GetStatsAsync(); Task<ThemeSettings> GetThemeAsync(); Task SaveThemeAsync(ThemeSettings theme);
    Task AddActivityAsync(Activity item); Task DeleteActivityAsync(int id); Task AddMenuItemAsync(MenuItem item); Task UpdateMenuOrderAsync(int id, int sortOrder); Task ReorderMenuAsync(IEnumerable<MenuOrderUpdate> items); Task DeleteMenuItemAsync(int id);
    Task<(int Id, string Username, string PasswordHash)?> FindAdminAsync(string username); Task SetPaymentAsync(int reservationId, string method, string status, string? transactionId);
}

public sealed class CampRepository(NpgsqlDataSource dataSource) : ICampRepository
{
    public async Task<List<Campsite.Models.Campsite>> GetSitesAsync(DateOnly? checkIn = null, DateOnly? checkOut = null, int guests = 1)
    {
        const string sql = """SELECT id, name, site_type AS SiteType, description, nightly_rate AS NightlyRate, max_guests AS MaxGuests, has_electric AS HasElectric, is_active AS IsActive FROM campsites s WHERE is_active AND max_guests >= @guests AND (@checkIn::date IS NULL OR NOT EXISTS (SELECT 1 FROM reservations r WHERE r.campsite_id=s.id AND r.status <> 'Cancelled' AND r.check_in < @checkOut::date AND r.check_out > @checkIn::date)) ORDER BY nightly_rate""";
        await using var db = await dataSource.OpenConnectionAsync(); return (await db.QueryAsync<Campsite.Models.Campsite>(sql, new { checkIn, checkOut, guests })).AsList();
    }
    public async Task<List<Activity>> GetActivitiesAsync() { await using var db = await dataSource.OpenConnectionAsync(); return (await db.QueryAsync<Activity>("SELECT id,title,description,starts_at AS StartsAt,location FROM activities WHERE starts_at >= now() - interval '1 day' ORDER BY starts_at LIMIT 12")).AsList(); }
    public async Task<List<MenuItem>> GetMenuAsync() { await using var db = await dataSource.OpenConnectionAsync(); return (await db.QueryAsync<MenuItem>("SELECT id,category,name,description,price,sort_order AS SortOrder,is_available AS IsAvailable FROM menu_items WHERE is_available ORDER BY sort_order,category,name")).AsList(); }
    public async Task<List<Reservation>> GetReservationsAsync()
    {
        const string sql = "SELECT r.id,r.campsite_id AS CampsiteId,c.name AS CampsiteName,r.customer_id AS CustomerId,cu.name AS CustomerName,cu.email AS CustomerEmail,r.check_in AS CheckIn,r.check_out AS CheckOut,r.guests,r.total,r.status,r.payment_status AS PaymentStatus,r.payment_method AS PaymentMethod FROM reservations r JOIN campsites c ON c.id=r.campsite_id JOIN customers cu ON cu.id=r.customer_id ORDER BY r.check_in DESC";
        await using var db = await dataSource.OpenConnectionAsync(); return (await db.QueryAsync<Reservation>(sql)).AsList();
    }
    public async Task<List<Customer>> GetCustomersAsync() { await using var db = await dataSource.OpenConnectionAsync(); return (await db.QueryAsync<Customer>("SELECT id,name,email,phone,created_at AS CreatedAt FROM customers ORDER BY created_at DESC")).AsList(); }
    public async Task<Reservation?> GetReservationAsync(int id) => (await GetReservationsAsync()).FirstOrDefault(x => x.Id == id);
    public async Task<int> CreateReservationAsync(BookingInput input)
    {
        if (input.CheckIn is null || input.CheckOut is null || input.CheckOut <= input.CheckIn) throw new InvalidOperationException("Choose a valid arrival and departure date.");
        await using var db = await dataSource.OpenConnectionAsync(); await using var tx = await db.BeginTransactionAsync();
        var site = await db.QuerySingleOrDefaultAsync<Campsite.Models.Campsite>("SELECT id,name,site_type AS SiteType,description,nightly_rate AS NightlyRate,max_guests AS MaxGuests,has_electric AS HasElectric,is_active AS IsActive FROM campsites WHERE id=@id AND is_active FOR UPDATE", new { id = input.CampsiteId }, tx);
        if (site is null || site.MaxGuests < input.Guests) throw new InvalidOperationException("That campsite is not available for your party.");
        var conflict = await db.ExecuteScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM reservations WHERE campsite_id=@id AND status <> 'Cancelled' AND check_in < @outDate AND check_out > @inDate)", new { id = site.Id, inDate = input.CheckIn, outDate = input.CheckOut }, tx);
        if (conflict) throw new InvalidOperationException("That campsite was just booked. Please choose another one.");
        var customerId = await db.ExecuteScalarAsync<int>("INSERT INTO customers (name,email,phone) VALUES (@Name,@Email,@Phone) ON CONFLICT (email) DO UPDATE SET name=EXCLUDED.name, phone=EXCLUDED.phone RETURNING id", input, tx);
        var total = (input.CheckOut.Value.DayNumber - input.CheckIn.Value.DayNumber) * site.NightlyRate;
        var reservationId = await db.ExecuteScalarAsync<int>("INSERT INTO reservations (campsite_id,customer_id,check_in,check_out,guests,total,status,payment_status,payment_method) VALUES (@CampsiteId,@customerId,@checkIn,@checkOut,@Guests,@total,'Pending','Unpaid',@PaymentMethod) RETURNING id", new { input.CampsiteId, customerId, checkIn = input.CheckIn, checkOut = input.CheckOut, input.Guests, total, input.PaymentMethod }, tx);
        await tx.CommitAsync(); return reservationId;
    }
    public async Task UpdateReservationStatusAsync(int id, string status) { await using var db = await dataSource.OpenConnectionAsync(); await db.ExecuteAsync("UPDATE reservations SET status=@status WHERE id=@id", new { id, status }); }
    public async Task SetPaymentAsync(int reservationId, string method, string status, string? transactionId) { await using var db = await dataSource.OpenConnectionAsync(); await db.ExecuteAsync("UPDATE reservations SET payment_method=@method,payment_status=@status,payment_reference=@transactionId WHERE id=@reservationId", new { reservationId, method, status, transactionId }); }
    public async Task<(int Customers, decimal PendingRevenue)> GetStatsAsync() { await using var db = await dataSource.OpenConnectionAsync(); return await db.QuerySingleAsync<(int Customers, decimal PendingRevenue)>("SELECT (SELECT count(*)::int FROM customers) AS Customers, COALESCE((SELECT sum(total) FROM reservations WHERE payment_status='Unpaid' AND status <> 'Cancelled'),0) AS PendingRevenue"); }
    public async Task<ThemeSettings> GetThemeAsync()
    {
        await using var db = await dataSource.OpenConnectionAsync(); var values = (await db.QueryAsync<(string Key, string Value)>("SELECT key,value FROM settings WHERE key LIKE 'theme.%'")).ToDictionary(x => x.Key, x => x.Value);
        return new ThemeSettings { Primary = values.GetValueOrDefault("theme.primary", "#245b3a"), Forest = values.GetValueOrDefault("theme.forest", "#173c2a"), Earth = values.GetValueOrDefault("theme.earth", "#60452f"), Sand = values.GetValueOrDefault("theme.sand", "#f5eedc"), Ember = values.GetValueOrDefault("theme.ember", "#e77824") };
    }
    public async Task SaveThemeAsync(ThemeSettings theme) { await using var db = await dataSource.OpenConnectionAsync(); await db.ExecuteAsync("INSERT INTO settings(key,value) VALUES ('theme.primary',@Primary),('theme.forest',@Forest),('theme.earth',@Earth),('theme.sand',@Sand),('theme.ember',@Ember) ON CONFLICT(key) DO UPDATE SET value=EXCLUDED.value", theme); }
    public async Task AddActivityAsync(Activity item) { await using var db = await dataSource.OpenConnectionAsync(); await db.ExecuteAsync("INSERT INTO activities(title,description,starts_at,location) VALUES (@Title,@Description,@StartsAt,@Location)", item); }
    public async Task DeleteActivityAsync(int id) { await using var db = await dataSource.OpenConnectionAsync(); await db.ExecuteAsync("DELETE FROM activities WHERE id=@id", new { id }); }
    public async Task AddMenuItemAsync(MenuItem item) { await using var db = await dataSource.OpenConnectionAsync(); await db.ExecuteAsync("INSERT INTO menu_items(category,name,description,price,sort_order,is_available) VALUES (@Category,@Name,@Description,@Price,COALESCE(NULLIF(@SortOrder,0),(SELECT COALESCE(MAX(sort_order),0)+10 FROM menu_items)),true)", item); }
    public async Task UpdateMenuOrderAsync(int id, int sortOrder) { await using var db = await dataSource.OpenConnectionAsync(); await db.ExecuteAsync("UPDATE menu_items SET sort_order=@sortOrder WHERE id=@id", new { id, sortOrder }); }
    public async Task ReorderMenuAsync(IEnumerable<MenuOrderUpdate> items)
    {
        var orderedItems = items.ToList();
        await using var db = await dataSource.OpenConnectionAsync(); await using var transaction = await db.BeginTransactionAsync();
        await db.ExecuteAsync("UPDATE menu_items SET sort_order=@SortOrder WHERE id=@Id", orderedItems, transaction);
        await transaction.CommitAsync();
    }
    public async Task DeleteMenuItemAsync(int id) { await using var db = await dataSource.OpenConnectionAsync(); await db.ExecuteAsync("DELETE FROM menu_items WHERE id=@id", new { id }); }
    public async Task<(int Id, string Username, string PasswordHash)?> FindAdminAsync(string username) { await using var db = await dataSource.OpenConnectionAsync(); return await db.QuerySingleOrDefaultAsync<(int, string, string)>("SELECT id,username,password_hash FROM app_users WHERE username=@username", new { username }); }
}
