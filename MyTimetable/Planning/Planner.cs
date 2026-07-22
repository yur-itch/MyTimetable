using MyTimetable.Models;

namespace MyTimetable.Planning
{
    public sealed class Planner
    {
        // Селектор замыкается на ЭТУ ссылку в конструкторе и держит её всю жизнь. Поэтому словарь
        // нельзя подменять извне (новая ссылка рассинхронизировала бы селектор с очередью) — только
        // переписывать содержимое на месте через LoadQueue. Наружу отдаём read-only вид.
        private readonly Dictionary<string, int> _queue = new();

        public IReadOnlyDictionary<string, int> Queue => _queue;

        // Заполняет очередь предметов для планирования, сохраняя ссылку, на которую замкнут селектор:
        // чистим и переписываем содержимое, а не заводим новый словарь.
        public void LoadQueue(IReadOnlyDictionary<string, int> titles)
        {
            _queue.Clear();
            foreach ((string title, int count) in titles)
            {
                _queue[title] = count;
            }
        }

        private static IEnumerable<Slot> GetActiveSlots(DaySchedule day)
            => day.GetDefaultOccupiedSlots().Except(day.GetHiddenSlots());

        private static IEnumerable<Slot> GetFillableSlots(DaySchedule day)
            => day.GetSlots().Except(GetActiveSlots(day)).Except(day.GetCustomOccupiedSlots());

        private static IEnumerable<Slot> GetConflictingSlots(DaySchedule day)
            => GetActiveSlots(day).Intersect(day.GetCustomOccupiedSlots());

        public IEnumerable<Slot> GetConflictingSlots(CalendarSchedule days)
            => days.SelectMany(x => GetConflictingSlots(x));

        private static IEnumerable<Slot> GetFillableSlots(CalendarSchedule days)
            => days.SelectMany(x => GetFillableSlots(x));

        // Конфликт = активный дефолтный урок и кастомный урок в одном слоте. Дефолтный — настоящая пара,
        // поэтому убираем кастомный, а его предмет возвращаем в очередь, чтобы перепланировать в другой слот.
        // Сначала материализуем слоты: SetCustom мутирует ячейки, а GetConflictingSlots читает их лениво —
        // иначе перечисление видело бы собственные правки.
        public List<DateOnly> ResolveConflicts(CalendarSchedule days, ScheduleChangeset changeset)
        {
            HashSet<DateOnly> dates = new();
            foreach (Slot slot in GetConflictingSlots(days).ToList())
            {
                // safe: конфликтный слот по определению содержит кастомный урок
                string title = days.SlotToCell(slot)!.CustomLesson!.Title; // читаем до очистки ячейки
                if (!_queue.ContainsKey(title)) _queue[title] = 0;
                _queue[title]++;
                dates.Add(slot.Date);
                RemoveCustom(days, slot, changeset);
            }
            return dates.ToList();
        }

        public List<DateOnly> Plan(IPlanningSelectorFactory selectorFactory, CalendarSchedule schedule, ScheduleChangeset changeset)
        {
            var tried = new HashSet<(Slot, string)>();
            var slots = GetFillableSlots(schedule).ToList();
            var selector = selectorFactory.Create(_queue, slots);
            ProposeResult Accept(Slot slot, string title)
                => tried.Add((slot, title)) ? new(true, true) : new(false, false);
            HashSet<DateOnly> dates = new();
            foreach (PlannedSlot planned in selector.Plan(Accept))
            {
                SetCustom(schedule, planned, changeset);
                _queue[planned.Lesson.Title]--;
                dates.Add(planned.Slot.Date);
            }
            return dates.ToList();
        }

        // Единственная точка изменения кастомного урока: разом обновляет рабочую модель (чтобы следующий
        // расчёт fillable/conflict видел уже принятое решение) и changeset (для последующего сохранения).
        // Оба буфера меняются только здесь — рассинхрон между моделью и changeset невозможен.
        // null — снять кастомный урок со слота.
        private static void SetCustom(CalendarSchedule days, PlannedSlot planned, ScheduleChangeset changeset)
        {
            days.SlotToCell(planned.Slot)!.CustomLesson = planned.Lesson;
            changeset.SetCustomLesson(planned);
        }

        private static void RemoveCustom(CalendarSchedule days, Slot slot, ScheduleChangeset changeset)
        {
            days.SlotToCell(slot)!.CustomLesson = null;
            changeset.RemoveCustomLesson(slot);
        }
    }
}
