using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;

using MyTimetable;
using MyTimetable.Planning;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Источник «сейчас» для горизонта планирования (ScheduleBuilder/Planner/CacheRebuilder/AppController).
// Обычно системное время; Schedule:DateOffsetDays != 0 включает машину времени для отладки планировщика.
// Воркер и фетчер на это НЕ завязаны — они остаются на реальном времени.
int dateOffsetDays = builder.Configuration.GetValue<int>("Schedule:DateOffsetDays");
builder.Services.AddSingleton<TimeProvider>(dateOffsetDays == 0
    ? TimeProvider.System
    : new OffsetTimeProvider(TimeSpan.FromDays(dateOffsetDays)));

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IPlanningSelectorFactory>(new PlanningSelectorFactory((q, f) => new GapClosingSelector(q, f)));
builder.Services.AddSingleton<Planner>();
builder.Services.AddSingleton<ScheduleData>();
builder.Services.AddSingleton<ViewRenderer>();
builder.Services.AddSingleton<ScheduleBuilder>();
builder.Services.AddSingleton<ChangesetApplier>();
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
