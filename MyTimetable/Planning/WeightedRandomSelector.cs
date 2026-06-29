using MyTimetable.Models;

namespace MyTimetable.Planning
{
    // Случайный выбор, но вероятность предмета пропорциональна его остатку в Queue (рулетка).
    public sealed class WeightedRandomSelector : PlanningSelectorBase
    {
        private readonly Random _random;

        public WeightedRandomSelector(Dictionary<string, int> queue, List<Slot> fillable, Random? random = null) : base(queue, fillable)
            => _random = random ?? Random.Shared;

        protected override string PickSubject()
        {
            var available = Available.ToList();
            int total = 0;
            foreach (var kv in available) total += kv.Value;

            int roll = _random.Next(total);
            int accumulated = 0;
            foreach (var kv in available)
            {
                accumulated += kv.Value;
                if (roll < accumulated) return kv.Key;
            }
            return available[^1].Key; // страховка от погрешности — не должно достигаться
        }
    }
}
