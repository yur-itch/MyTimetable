using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;

using MyTimetable;
using MyTimetable.Models;
using MyTimetable.Planning;
using MyTimetable.Entities;
using MyTimetable.Security;
using MyTimetable.TsuInTime;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

// Источник «сейчас» для горизонта планирования (ScheduleBuilder/Planner/CacheRebuilder/AppController).
// Обычно системное время; Schedule:DateOffsetDays != 0 включает машину времени для отладки планировщика.
// Воркер на это НЕ завязан — он остается на реальном времени.
int dateOffsetDays = builder.Configuration.GetValue<int>("Schedule:DateOffsetDays");
builder.Services.AddSingleton<TimeProvider>(dateOffsetDays == 0
    ? TimeProvider.System
    : new OffsetTimeProvider(TimeSpan.FromDays(dateOffsetDays)));

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IReadOnlyDictionary<string, IPlanningSelectorFactory>>(_ =>
    new Dictionary<string, IPlanningSelectorFactory>(StringComparer.OrdinalIgnoreCase)
    {
        ["gap"] = new PlanningSelectorFactory((q, s) => new GapClosingSelector(q, s, DaySchedule.DefaultSlotCount)),
        ["emptyseed"] = new PlanningSelectorFactory((q, s) => new EmptyDaySeedSelector(q, s, DaySchedule.DefaultSlotCount)),
        ["leading"] = new PlanningSelectorFactory((q, s) => new LeadingChunkGrowthSelector(q, s, DaySchedule.DefaultSlotCount)),
        ["trailing"] = new PlanningSelectorFactory((q, s) => new TrailingChunkGrowthSelector(q, s, DaySchedule.DefaultSlotCount)),
        ["roundrobin"] = new PlanningSelectorFactory((q, s) => new RoundRobinSelector(q, s)),
        ["fairshare"] = new PlanningSelectorFactory((q, s) => new FairShareSelector(q, s)),
        ["largest"] = new PlanningSelectorFactory((q, s) => new LargestQueueFirstSelector(q, s)),
        ["smallest"] = new PlanningSelectorFactory((q, s) => new SmallestQueueFirstSelector(q, s)),
        ["random"] = new PlanningSelectorFactory((q, s) => new RandomSelector(q, s)),
        ["weighted"] = new PlanningSelectorFactory((q, s) => new WeightedRandomSelector(q, s)),
    });
builder.Services.AddSingleton<Planner>();
builder.Services.AddSingleton<PlanPage>();
builder.Services.AddSingleton<ScheduleData>();
builder.Services.AddSingleton<CliRenderer>();
builder.Services.AddSingleton<SessionIdProvider>();
builder.Services.AddSingleton<AuthProvider>();
builder.Services.AddSingleton<ViewRenderer>();
builder.Services.AddSingleton<ScheduleBuilder>();
builder.Services.AddSingleton<ChangesetApplier>();
builder.Services.AddSingleton<TsuInTimeFetcher>();
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
    var sp = scope.ServiceProvider;
    sp.GetRequiredService<AppDbContext>().Database.Migrate();

    // Seed default users
    var db = sp.GetRequiredService<AppDbContext>();
    var auth = sp.GetRequiredService<AuthProvider>();
    await auth.Register(db, "admin", "123456Qq!", isViewer: true, isEditor: true);
    await auth.Register(db, "user", "123456Qq!", isViewer: true, isEditor: false);

    // Пререндерим страницу планирования из текущей (на старте пустой) очереди. Дальше её пересобирают
    // эндпоинты планирования и снятия конфликтов при каждом изменении очереди (PlanPage.Rebuild).
    await sp.GetRequiredService<PlanPage>().Rebuild(sp.GetRequiredService<Planner>().Queue);
}

//app.UseResponseCompression();
app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
