using MyTimetable.Models;

namespace MyTimetable.Planning
{
    // Сначала закрывает предметы, которых осталось меньше всего.
    public sealed class SmallestQueueFirstSelector : PlanningSelectorBase
    {
        public SmallestQueueFirstSelector(Dictionary<string, int> queue, List<Slot> fillable) : base(queue, fillable) { }

        // Пустой Available -> MinBy возвращает default(KeyValuePair), его Key == null -> остановка.
        protected override string? PickSubject() => Available.MinBy(kv => kv.Value).Key;
    }
}
