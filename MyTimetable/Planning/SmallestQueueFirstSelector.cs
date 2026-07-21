using MyTimetable.Models;

namespace MyTimetable.Planning
{
    // Сначала закрывает предметы, которых осталось меньше всего.
    public sealed class SmallestQueueFirstSelector : PlanningSelectorBase
    {
        public SmallestQueueFirstSelector(Dictionary<string, int> queue, List<Slot> fillable) : base(queue, fillable) { }

        // Пустой Available -> MinBy не находит элементов; возвращаем null -> остановка.
        protected override string? PickSubject()
        {
            var available = Available.ToList();
            if (available.Count == 0) return null;
            return available.MinBy(kv => kv.Value).Key;
        }
    }
}
