using LoadTestingApp.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Добавьте эти сервисы
builder.Services.AddControllersWithViews(); // Для MVC + API

builder.Services.AddSpaStaticFiles(config => {
    config.RootPath = "wwwroot";
});

builder.Services.AddHttpClient("LoadTestClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<LoadTestService>();
builder.Services.AddMemoryCache();


builder.Services.AddSingleton<LoadTestService>(serviceProvider =>
{
    var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
    var logger = serviceProvider.GetService<ILogger<LoadTestService>>();

    return new LoadTestService(httpClientFactory, logger);
});

// Настройка CORS (раскомментируйте и измените)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod() // Разрешаем POST
              .AllowAnyHeader();
    });
});


builder.Services.AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
builder.Services.AddMetrics();

var app = builder.Build();



app.UseStaticFiles(); // Для wwwroot
app.UseSpaStaticFiles();
app.UseRouting();
//builder.Services.AddHttpClient();

//app.MapControllerRoute(
//    name: "default",
//    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapFallbackToFile("index.html");

app.UseCors("AllowFrontend");


app.UseRouting();
app.UseAuthorization();

app.MapControllers();

app.Run();