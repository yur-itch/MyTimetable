using MyTimetable.Models;

namespace MyTimetable.Planning
{
    // На каждом шаге берёт предмет так, чтобы доли УЖЕ ПОСТАВЛЕННЫХ предметов после этого размещения
    // были как можно ближе к исходным пропорциям очереди (снятым в конструкторе). Перебирает всех
    // кандидатов и выбирает того, кто минимизирует сумму квадратов отклонений долей (в духе метода
    // наибольших остатков).
    public sealed class FairShareSelector : PlanningSelectorBase
    {
        private readonly Dictionary<string, double> _desired;
        private readonly Dictionary<string, int> _placed = new();
        private int _placedTotal;

        public FairShareSelector(Dictionary<string, int> queue, List<Slot> fillable) : base(queue, fillable)
            => _desired = CaptureDesired();

        protected override string PickSubject()
        {
            string best = Available.First().Key;
            double bestDistance = double.PositiveInfinity;
            foreach (string candidate in Available.Select(kv => kv.Key))
            {
                double distance = DistanceAfterPlacing(candidate);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            _placed[best] = _placed.GetValueOrDefault(best) + 1;
            _placedTotal++;
            return best;
        }

        // Исходные (желаемые) пропорции — снимок очереди на старте планирования.
        private Dictionary<string, double> CaptureDesired()
        {
            int total = Queue.Values.Sum();
            return total == 0
                ? Queue.ToDictionary(kv => kv.Key, _ => 0.0)
                : Queue.ToDictionary(kv => kv.Key, kv => (double)kv.Value / total);
        }

        private double DistanceAfterPlacing(string candidate)
        {
            int total = _placedTotal + 1; // после размещения одного урока
            double distance = 0;
            foreach (var (key, desired) in _desired)
            {
                int placed = _placed.GetValueOrDefault(key);
                if (key == candidate) placed++;
                double proportion = (double)placed / total;
                double diff = proportion - desired;
                distance += diff * diff;
            }
            return distance;
        }
    }
}
