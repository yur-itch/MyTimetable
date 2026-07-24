namespace MyTimetable.Planning
{
    public sealed record PlanResult(List<DateOnly> Dates, Dictionary<string, int> RemainingQueue);
    public sealed record ResolveResult(List<DateOnly> Dates, Dictionary<string, int> FreedQueue);

    // Pure-function planner: receives titles, selector, and schedule on every call.
    // No mutable fields — all state flows in via arguments and out via return values.
    public sealed class Planner
    {
        public PlanResult Plan(
            IReadOnlyDictionary<string, int> titles,
            IPlanningSelectorFactory selectorFactory,
            CalendarSchedule schedule,
            ScheduleChangeset changeset)
        {
            var queue = new Dictionary<string, int>(titles);
            var tried = new HashSet<(Slot, string)>();
            var slots = GetFillableSlots(schedule).ToList();
            var selector = selectorFactory.Create(queue, slots);
            ProposeResult Accept(Slot slot, string title)
                => tried.Add((slot, title)) ? new(true, true) : new(false, false);
            HashSet<DateOnly> dates = new();
            foreach (PlannedSlot planned in selector.Plan(Accept))
            {
                SetCustom(schedule, planned, changeset);
                queue[planned.Lesson.Title]--;
                dates.Add(planned.Slot.Date);
            }
            return new PlanResult(dates.ToList(), queue);
        }

        public IEnumerable<Slot> GetConflictingSlots(CalendarSchedule days)
            => days.SelectMany(x => GetConflictingSlots(x));

        public ResolveResult ResolveConflicts(CalendarSchedule days, ScheduleChangeset changeset)
        {
            var queue = new Dictionary<string, int>();
            HashSet<DateOnly> dates = new();
            foreach (Slot slot in GetConflictingSlots(days).ToList())
            {
                // safe: конфликтный слот по определению содержит кастомный урок
                string title = days.SlotToCell(slot)!.CustomLesson!.Title; // читаем до очистки ячейки
                if (!queue.ContainsKey(title)) queue[title] = 0;
                queue[title]++;
                dates.Add(slot.Date);
                RemoveCustom(days, slot, changeset);
            }
            return new ResolveResult(dates.ToList(), queue);
        }

        private static IEnumerable<Slot> GetActiveSlots(DaySchedule day)
            => day.GetDefaultOccupiedSlots().Except(day.GetHiddenSlots());

        private static IEnumerable<Slot> GetFillableSlots(DaySchedule day)
            => day.GetSlots().Except(GetActiveSlots(day)).Except(day.GetCustomOccupiedSlots());

        private static IEnumerable<Slot> GetConflictingSlots(DaySchedule day)
            => GetActiveSlots(day).Intersect(day.GetCustomOccupiedSlots());

        private static IEnumerable<Slot> GetFillableSlots(CalendarSchedule days)
            => days.SelectMany(x => GetFillableSlots(x));

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
