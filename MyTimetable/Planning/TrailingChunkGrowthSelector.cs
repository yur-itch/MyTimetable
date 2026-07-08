using MyTimetable.Models;

namespace MyTimetable.Planning
{
    // Зеркало LeadingChunkGrowthSelector на конец дня. Рассматривает только непустые дни, у которых после
    // ПОСЛЕДНЕГО куска занятых пар есть свободная пара. Куча по (длина последнего куска, дата): берёт день
    // с наименьшим последним куском, ставит урок в пару прямо ПОСЛЕ него (кусок растёт к концу дня) и
    // возвращает день в кучу с обновлённой длиной, пока после куска остаётся место.
    public sealed class TrailingChunkGrowthSelector : PlanningSelectorBase
    {
        private readonly record struct State
        {
            public required DateOnly Date { get; init; }
            public required int End { get; init; }    // конец последнего куска
            public required int Length { get; init; } // его длина (ключ кучи)
        }

        private readonly PriorityQueue<State, (int Length, DateOnly Date)> _heap = new();
        private readonly RoundRobinPicker _picker;
        private readonly int _slotCount;

        public TrailingChunkGrowthSelector(Dictionary<string, int> queue, List<Slot> fillable, int slotCount = 6)
            : base(queue, fillable)
        {
            _slotCount = slotCount;
            _picker = new RoundRobinPicker(Queue);
            foreach (Day d in DayLayout.GetDays(Fillable))
            {
                if (DayLayout.LastChunk(d.Open, slotCount) is { } c && c.End < slotCount)
                {
                    _heap.Enqueue(
                        new State { Date = d.Date, End = c.End, Length = c.Length },
                        (c.Length, d.Date));
                }
            }
        }

        protected override Slot? TakeSlot()
        {
            if (!_heap.TryDequeue(out State s, out _)) return null;

            Slot slot = new() { Date = s.Date, Number = s.End + 1 };
            int end = s.End + 1;
            int length = s.Length + 1;
            if (end < _slotCount)
            {
                _heap.Enqueue(s with { End = end, Length = length }, (length, s.Date));
            }
            return slot;
        }

        protected override string? PickSubject() => _picker.Pick();
    }
}
