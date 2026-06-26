using Microsoft.EntityFrameworkCore;
using MyTimetable.Models;
using MyTimetable.Entities;
using System.Collections.Immutable;

namespace MyTimetable
{
    // Единый источник сборки модели расписания из БД: дефолтные уроки + отметка скрытых слотов
    // (Deactivations). Используется и фоновым воркером для кэша, и контроллером для рендера на лету.
    public sealed class ScheduleBuilder
    {
        private readonly IServiceProvider _serviceProvider;

        public ScheduleBuilder(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<List<DaySchedule>> LoadFromDb()
            => await LoadFromDb(DateOnly.MinValue, DateOnly.MaxValue);

        public async Task<List<DaySchedule>> LoadFromDb(DateOnly from, DateOnly to)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var defaultLessonsList = await db.DefaultLessons
                .Where(x => x.Date >= from && x.Date <= to)
                .ToListAsync();

            // Препода показываем только у предметов (Title), что за год вели ≥2 разных преподавателя.
            // Группируем по Title, а не LessonType: иначе "у всех лекций >1 препода" → показываем почти всегда.
            var uniqueProfs = defaultLessonsList
                .GroupBy(x => x.Title)
                .Select(x => (
                    Title: x.Key,
                    Professors: x
                        .DistinctBy(y => y.Professor)
                        .Where(y => !string.IsNullOrEmpty(y.Professor))
                        .ToList()
                ))
                .Where(x => x.Professors.Count > 1)
                .SelectMany(x => x.Professors.Select(y => (y.Professor, x.Title)))
                .GroupBy(x => x.Professor)
                .ToDictionary(x => x.Key, x => x.Select(y => y.Title).ToHashSet());

            var defaultLessons = defaultLessonsList
                .ToDictionary(
                    x => new Slot { Date = x.Date, Number = x.LessonNumber },
                    x => new DefaultLesson
                    {
                        Title = x.Title,
                        LessonType = x.LessonType,
                        Professor = new Professor
                        {
                            Name = x.Professor,
                            IsShown = uniqueProfs.TryGetValue(x.Professor, out var types)
                                   && types.Contains(x.Title)
                        },
                        Room = x.Room
                    }
                );

            var customLessons = await db.CustomLessons
                .Where(x => x.Date >= from && x.Date <= to)
                .ToDictionaryAsync(
                    x => new Slot { Date = x.Date, Number = x.LessonNumber },
                    x => new CustomLesson { Title = x.Title, LessonType = x.LessonType }
                );

            var deactivated = await db.Deactivations
                .Where(x => x.Date >= from && x.Date <= to)
                .Select(d => new Slot { Date = d.Date, Number = d.Number })
                .ToHashSetAsync();

            var allSlots = defaultLessons.Keys.Union(customLessons.Keys);

            // Пустая строка дня: 6 пустых ячеек. Нужна и для дырок внутри дня, и для целиком пустых дней.
            static Cell[] EmptyRow() => Enumerable.Range(0, 6)
                .Select(_ => new Cell { DefaultLesson = null, CustomLesson = null, Hidden = false })
                .ToArray();

            // Занятые дни: дата -> позиционный Cell[6] (индекс = номер пары - 1). Группируем по дате
            // один раз — это и есть замена перебора "для каждого дня ищем его ячейки" (O(n) вместо O(n^2)).
            var cellsByDate = allSlots
                .Select(slot => (
                    Slot: slot,
                    Cell: new Cell
                    {
                        DefaultLesson = defaultLessons.GetValueOrDefault(slot),
                        CustomLesson = customLessons.GetValueOrDefault(slot),
                        Hidden = deactivated.Contains(slot)
                    }
                ))
                .GroupBy(x => x.Slot.Date)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        var cells = EmptyRow();
                        foreach (var item in group)
                        {
                            if (item.Slot.Number >= 1 && item.Slot.Number <= 6)
                            {
                                cells[item.Slot.Number - 1] = item.Cell;
                            }
                        }
                        return cells;
                    });

            if (cellsByDate.Count == 0)
            {
                return new();
            }

            // Непрерывный календарь от первой до последней даты: дни без пар получают пустые ячейки,
            // чтобы в сетке не было разрывов (выходные/окна рисуются пустыми).
            DateOnly firstDate = cellsByDate.Keys.Min();
            DateOnly lastDate = cellsByDate.Keys.Max();

            return Enumerable.Range(0, lastDate.DayNumber - firstDate.DayNumber + 1)
                .Select(offset => firstDate.AddDays(offset))
                .Select(date => new DaySchedule
                {
                    Date = date,
                    Cells = cellsByDate.TryGetValue(date, out var cells) ? cells : EmptyRow()
                })
                .ToList();
        }
    }
}
