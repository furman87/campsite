using Campsite.Data;
using Campsite.Services;
using Dapper;
using Microsoft.AspNetCore.Authentication.Cookies;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
builder.Services.AddControllersWithViews();
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");
builder.Services.AddHttpClient();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
    options.Cookie.Name = "Lakeside.Admin";
    options.ExpireTimeSpan = TimeSpan.FromHours(12);
});
builder.Services.AddAuthorization();
builder.Services.AddSingleton<NpgsqlDataSource>(_ => NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("Campground") ?? throw new InvalidOperationException("ConnectionStrings:Campground is required.")));
builder.Services.AddScoped<ICampRepository, CampRepository>();
builder.Services.AddScoped<DatabaseInitializer>();
builder.Services.AddScoped<PasswordService>();
builder.Services.AddScoped<IPaymentGateway, PaymentGateway>();
var app = builder.Build();
using (var scope = app.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Home/Error");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();
