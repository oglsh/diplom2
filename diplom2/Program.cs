using LoadTestingApp.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddControllersWithViews(); 

builder.Services.AddSpaStaticFiles(config => {
    config.RootPath = "wwwroot";
});

builder.Services.AddHttpClient("LoadTestClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<LoadTestService>();
builder.Services.AddMemoryCache();

//builder.Services.AddMvc();


builder.Services.AddSingleton<LoadTestService>(serviceProvider =>
{
    var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
    var logger = serviceProvider.GetService<ILogger<LoadTestService>>();

    return new LoadTestService(httpClientFactory, logger);
});


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod() 
              .AllowAnyHeader();
    });
});


builder.Services.AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
builder.Services.AddMetrics();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "LoadTestApi");
    });
}


app.UseStaticFiles(); 
app.UseSpaStaticFiles();
app.UseRouting();
app.MapFallbackToFile("index.html");
app.UseCors("AllowFrontend");
app.UseRouting();
app.UseAuthorization();
app.MapControllers();

app.Run();