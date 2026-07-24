namespace MyTimetable
{
    // Единый источник сборки модели расписания из БД: дефолтные уроки + отметка скрытых слотов
    // (Deactivations). Используется и фоновым воркером для кэша, и контроллером для рендера на лету.
    public sealed class ScheduleBuilder
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeProvider _time;

        public ScheduleBuilder(IServiceProvider serviceProvider, TimeProvider time)
        {
            _serviceProvider = serviceProvider;
            _time = time;
        }

        // Границы учебного года — реальные даты вместо DateOnly.Min/Max. Запрошенный диапазон LoadFromDb
        // благодаря им всегда конечен, поэтому from/to можно честно использовать как края календаря,
        // не плодя пустые недели до бесконечности.
        // NB: даты-заглушка — 1 сентября .. 30 июня того учебного года, в который попадает сегодня.
        // Поставь реальные начало/конец, если период другой.
        public DateOnly YearStart
        {
            get
            {
                var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
                int startYear = today.Month >= 9 ? today.Year : today.Year - 1;
                return new DateOnly(startYear, 9, 1);
            }
        }

        public DateOnly YearEnd => new DateOnly(YearStart.Year + 1, 6, 30);

        public async Task<CalendarSchedule> LoadFromDb(AppDbContext db)
            => await LoadFromDb(db, YearStart, YearEnd);

        public async Task<CalendarSchedule> LoadFromDb(AppDbContext db, DateOnly from, DateOnly to)
        {
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
                        var cells = DaySchedule.Empty(group.Key).Cells;
                        foreach (var item in group)
                        {
                            if (item.Slot.Number >= 1 && item.Slot.Number <= DaySchedule.DefaultSlotCount)
                            {
                                cells[item.Slot.Number - 1] = item.Cell;
                            }
                        }
                        return cells;
                    });

            if (cellsByDate.Count == 0)
            {
                return new CalendarSchedule([]);
            }

            // Запрошенный диапазон задаёт ГРАНИЦЫ календаря, а не только фильтр выборки. Данные уже
            // внутри [from, to] (отфильтрованы запросом), поэтому пустые дни-границы лишь растягивают
            // сетку до запрошенных краёв — пустые from/to тоже попадают в календарь.
            cellsByDate.TryAdd(from, DaySchedule.Empty(from).Cells);
            cellsByDate.TryAdd(to, DaySchedule.Empty(to).Cells);

            // Непрерывный календарь от первой до последней даты: дни без пар получают пустые ячейки,
            // чтобы в сетке не было разрывов (выходные/окна рисуются пустыми).
            return new CalendarSchedule(
                cellsByDate.Select(x => new DaySchedule
                {
                    Date = x.Key,
                    Cells = x.Value
                }));
        }
    }
}
