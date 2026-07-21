using MyTimetable.Models;

namespace MyTimetable.Planning
{
    // Всегда берёт предмет с самым большим остатком.
    public sealed class LargestQueueFirstSelector : PlanningSelectorBase
    {
        public LargestQueueFirstSelector(Dictionary<string, int> queue, List<Slot> fillable) : base(queue, fillable) { }

        // Пустой Available -> MaxBy возвращает default(KeyValuePair), его Key == null -> остановка.
        protected override string? PickSubject() => Available.MaxBy(kv => kv.Value).Key;
    }
}
