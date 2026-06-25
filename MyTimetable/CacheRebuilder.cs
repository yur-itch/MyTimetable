using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using MyTimetable.Models;

namespace MyTimetable
{
    // Полная пересборка кэша страницы (предсжатый блоб всей страницы + партиалы по дням) из текущего
    // состояния БД. Зовётся и фоновым воркером каждый тик, и контроллером сразу после Hide/Unhide —
    // кэш всегда тёплый, поэтому время отдачи страницы держится на нуле (сознательный размен:
    // дороже на запись/скрытие ради мгновенного чтения).
    public class CacheRebuilder
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ViewRenderer _renderer;
        private readonly ScheduleBuilder _builder;
        private readonly ScheduleData _data;

        public CacheRebuilder(IServiceProvider serviceProvider, ViewRenderer renderer, ScheduleBuilder builder, ScheduleData data)
        {
            _serviceProvider = serviceProvider;
            _renderer = renderer;
            _builder = builder;
            _data = data;
        }

        // true — кэш пересобран и валиден; false — в БД нет данных, кэш помечен невалидным.
        public async Task<bool> Rebuild()
        {
            List<DaySchedule> schedule = await _builder.LoadFromDb();
            if (!schedule.Any())
            {
                _data.StateValid = false;
                return false;
            }
            MarkProfessorVisibility(schedule);

            using var scope = _serviceProvider.CreateScope();
            var httpContext = new DefaultHttpContext
            {
                RequestServices = scope.ServiceProvider,
            };
            var routeData = new RouteData();
            routeData.Values["controller"] = "App";
            var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());

            string html = await _renderer.RenderViewToStringAsync("Get", schedule, actionContext);
            _data.ViewResult = Compression.Brotli(html);

            foreach (DaySchedule day in schedule)
            {
                _data.PartialViewResult[day.Date] = await _renderer.RenderViewToStringAsync("GetOne", day, actionContext, true);
            }

            _data.StateValid = true;
            return true;
        }

        // Помечаем препода к показу только у тех предметов, что за год вели ≥2 разных преподавателя.
        // Пустого препода не считаем за отдельного (это просто незаполненные данные, а не другой человек).
        private static void MarkProfessorVisibility(List<DaySchedule> schedule)
        {
            List<Lesson> all = schedule.SelectMany(d => d.Lessons).OfType<Lesson>().ToList();
            HashSet<string> multiProfTitles = all
                .Where(l => l.Professor.Length > 0)
                .GroupBy(l => l.Title)
                .Where(g => g.Select(l => l.Professor).Distinct().Count() > 1)
                .Select(g => g.Key)
                .ToHashSet();
            foreach (Lesson l in all)
            {
                l.ShowProfessor = multiProfTitles.Contains(l.Title);
            }
        }
    }
}
