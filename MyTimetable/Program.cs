using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;

using MyTimetable;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ScheduleData>();
builder.Services.AddSingleton<ViewRenderer>();
builder.Services.AddSingleton<ScheduleBuilder>();
builder.Services.AddSingleton<CacheRebuilder>();
//builder.Services.AddResponseCompression(options =>
//{
//    options.EnableForHttps = true;
//    options.Providers.Add<BrotliCompressionProvider>();
//    options.Providers.Add<GzipCompressionProvider>();
//});
//builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
//{
//    options.Level = CompressionLevel.Optimal;
//});
builder.Services.AddHostedService<CacheWorker>();
var app = builder.Build();

// Применяем миграции на старте: на чистой БД (свежий Postgres на Railway) это создаёт схему.
// Если упадёт здесь — значит БД недоступна или строка подключения неверна; смотри логи деплоя.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

//app.UseResponseCompression();
app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
