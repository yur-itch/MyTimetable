using MyTimetable.Models;

namespace MyTimetable.Planning
{
    // Всегда берёт предмет с самым большим остатком.
    public sealed class LargestQueueFirstSelector : PlanningSelectorBase
    {
        public LargestQueueFirstSelector(Dictionary<string, int> queue, List<Slot> fillable) : base(queue, fillable) { }

        protected override string? PickSubject()
        {
            var best = Available.MaxBy(kv => kv.Value);
            return best.Key; // null когда Available пуст
        }
    }
}
