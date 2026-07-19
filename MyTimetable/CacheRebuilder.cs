using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using MyTimetable.Models;

namespace MyTimetable
{
    // Полная пересборка кэша страницы (предсжатый блоб всей страницы + партиалы по дням) из текущего
    // состояния БД. Зовётся и фоновым воркером каждый тик, и контроллером сразу после Hide/Unhide —
    // кэш всегда тёплый, поэтому время отдачи страницы держится на нуле (сознательный размен:
    // дороже на запись/скрытие ради мгновенного чтения).
    public sealed class CacheRebuilder
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ViewRenderer _renderer;
        private readonly CliRenderer _cliRenderer;
        private readonly ScheduleBuilder _builder;
        private readonly ScheduleData _data;
        private readonly TimeProvider _time;

        public CacheRebuilder(IServiceProvider serviceProvider, ViewRenderer renderer, ScheduleBuilder builder, ScheduleData data, TimeProvider time, CliRenderer cliRenderer)
        {
            _serviceProvider = serviceProvider;
            _renderer = renderer;
            _builder = builder;
            _data = data;
            _time = time;
            _cliRenderer = cliRenderer;
        }

        // true — кэш пересобран и валиден; false — в БД нет данных, кэш помечен невалидным.
        public async Task<bool> Rebuild(AppDbContext db, List<DateOnly>? dates = null)
        {
            CalendarSchedule schedule = await _builder.LoadFromDb(db);
            if (!schedule.Any())
            {
                _data.StateValid = false;
                return false;
            }

            if (dates == null)
            {
                dates = Enumerable
                    .Range(_builder.YearStart.DayNumber, _builder.YearEnd.DayNumber - _builder.YearStart.DayNumber + 1)
                    .Select(x => DateOnly.FromDayNumber(x))
                    .ToList();
            } else if (!dates.Any())
            {
                _data.StateValid = true;
                return true;
            }

            using var scope = _serviceProvider.CreateScope();
            var httpContext = new DefaultHttpContext
            {
                RequestServices = scope.ServiceProvider,
            };
            var routeData = new RouteData();
            routeData.Values["controller"] = "App";
            var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());

            foreach (DaySchedule day in dates.Select(x => schedule.DateToDaySchedule(x)!))
            {
                _data.PartialViewResult[day.Date] = await _renderer.RenderViewToStringAsync("GetOne", day, actionContext, true);
            }

            // Упорядоченный снимок кэшированных партиалов: страница собирается из всех кусочков
            // (а не только что перерендренных), снимок — чтобы не перечислять живой словарь во время рендера.
            var ordered = _data.PartialViewResult.OrderBy(kv => kv.Key).ToList();

            var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
            int currentIdx = ordered.Count > 0 ? Math.Max(0, ordered.FindLastIndex(kv => kv.Key <= today)) : -1;
            string scrollTarget = currentIdx >= 0 ? "day-" + ordered[currentIdx].Key.ToString("yyyy-MM-dd") : "";

            var view = new PageView
            {
                SlotCount = schedule.Max(d => d.Cells.Length),
                ScrollTarget = scrollTarget,
                Days = ordered.Select(kv => kv.Value).ToList()
            };

            string html = await _renderer.RenderViewToStringAsync("Get", view, actionContext);
            _data.ViewResult = Compression.Brotli(html);

            // CLI view: prerendered table, brotli-compressed for GET /Cli
            int cliScrollTarget = currentIdx >= 0 ? currentIdx : 0;
            string cliJson = _cliRenderer.Render(view.SlotCount, cliScrollTarget, schedule);
            _data.CliViewResult = Compression.Brotli(cliJson);

            _data.StateValid = true;
            return true;
        }
    }
}
