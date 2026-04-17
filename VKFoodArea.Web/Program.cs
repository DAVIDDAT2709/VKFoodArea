using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using VKFoodArea.Web.Data;
using VKFoodArea.Web.Models;
using VKFoodArea.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "VKFoodArea.Cms";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AdminRoleNames.AdminOnly, policy => policy.RequireRole(AdminRoleNames.Admin));
    options.AddPolicy(
        AdminRoleNames.AdminOrRestaurantOwner,
        policy => policy.RequireRole(AdminRoleNames.Admin, AdminRoleNames.RestaurantOwner));
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options
        .UseSqlite(
            "Data Source=vkfoodarea_web.db",
            sqlite => sqlite.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
        .ConfigureWarnings(warnings =>
        {
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning);
            warnings.Ignore(RelationalEventId.MultipleCollectionIncludeWarning);
        }));

builder.Services.AddScoped<IHomeService, HomeService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IPoiService, PoiService>();
builder.Services.AddScoped<ITourService, TourService>();
builder.Services.AddScoped<IQrResolveService, QrResolveService>();
builder.Services.AddScoped<IPoiImageStorageService, PoiImageStorageService>();
builder.Services.AddScoped<IPoiAudioStorageService, PoiAudioStorageService>();
builder.Services.AddScoped<IQrCodeImageStorageService, QrCodeImageStorageService>();
builder.Services.AddScoped<IQrCodeItemService, QrCodeItemService>();
builder.Services.AddScoped<INarrationHistoryService, NarrationHistoryService>();
builder.Services.AddScoped<IAdminUserService, AdminUserService>();
builder.Services.AddScoped<ICurrentAdminService, CurrentAdminService>();
builder.Services.AddScoped<IAppUserAccountService, AppUserAccountService>();
builder.Services.AddScoped<IUserMovementLogService, UserMovementLogService>();
builder.Services.AddScoped<IAppDevicePresenceService, AppDevicePresenceService>();
builder.Services.AddHttpClient<ITtsTranslationService, TtsTranslationService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(12);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("VKFoodArea.Web/1.0");
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await WebDataInitializer.InitializeAsync(db, app.Environment, app.Environment.IsDevelopment());
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
