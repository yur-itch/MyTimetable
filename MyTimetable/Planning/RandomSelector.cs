using MyTimetable.Models;

namespace MyTimetable.Planning
{
    // Равновероятный выбор среди доступных предметов.
    public sealed class RandomSelector : PlanningSelectorBase
    {
        private readonly Random _random;

        public RandomSelector(Dictionary<string, int> queue, List<Slot> fillable, Random? random = null) : base(queue, fillable)
            => _random = random ?? Random.Shared;

        protected override string PickSubject()
        {
            var available = Available.Select(kv => kv.Key).ToList();
            return available[_random.Next(available.Count)];
        }
    }
}
