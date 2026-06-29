using MyTimetable.Models;

namespace MyTimetable.Planning
{
    // Ходит по предметам по кругу, пропуская исчерпанные. Порядок обхода фиксируется в конструкторе.
    public sealed class RoundRobinSelector : PlanningSelectorBase
    {
        private readonly List<string> _order;
        private int _position;

        public RoundRobinSelector(Dictionary<string, int> queue, List<Slot> fillable) : base(queue, fillable)
            => _order = Queue.Keys.ToList();

        protected override string PickSubject()
        {
            for (int i = 0; i < _order.Count; i++)
            {
                int index = (_position + i) % _order.Count;
                string key = _order[index];
                if (Queue.TryGetValue(key, out int remaining) && remaining > 0)
                {
                    _position = (index + 1) % _order.Count;
                    return key;
                }
            }
            // недостижимо: Plan вызывает PickSubject только при наличии доступных
            throw new InvalidOperationException("No subjects with a remaining count to select.");
        }
    }
}
