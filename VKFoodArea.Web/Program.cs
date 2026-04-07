using Microsoft.EntityFrameworkCore;
using VKFoodArea.Web.Data;
using VKFoodArea.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=vkfoodarea_web.db"));

builder.Services.AddScoped<IHomeService, HomeService>();
builder.Services.AddScoped<IPoiService, PoiService>();
builder.Services.AddScoped<IPoiImageStorageService, PoiImageStorageService>();
builder.Services.AddScoped<IQrCodeItemService, QrCodeItemService>();
builder.Services.AddScoped<INarrationHistoryService, NarrationHistoryService>();
builder.Services.AddHttpClient<ITtsTranslationService, TtsTranslationService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(12);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("VKFoodArea.Web/1.0");
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await WebDataInitializer.InitializeAsync(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllers();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
