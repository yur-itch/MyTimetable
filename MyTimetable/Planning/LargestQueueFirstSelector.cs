using MyTimetable.Models;

namespace MyTimetable.Planning
{
    // Всегда берёт предмет с самым большим остатком.
    public sealed class LargestQueueFirstSelector : PlanningSelectorBase
    {
        public LargestQueueFirstSelector(Dictionary<string, int> queue, List<Slot> fillable) : base(queue, fillable) { }

        // Пустой Available -> MaxBy не находит элементов; возвращаем null -> остановка.
        protected override string? PickSubject()
        {
            var available = Available.ToList();
            if (available.Count == 0) return null;
            return available.MaxBy(kv => kv.Value).Key;
        }
    }
}
