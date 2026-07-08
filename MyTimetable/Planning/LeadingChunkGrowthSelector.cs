using MyTimetable.Models;

namespace MyTimetable.Planning
{
    // Наращивает САМЫЙ КОРОТКИЙ первый кусок занятых пар. Рассматривает только непустые дни, у которых
    // перед первым куском есть свободная пара. Куча по (длина первого куска, дата): на каждом шаге берёт
    // день с наименьшим первым куском, ставит урок в пару прямо ПЕРЕД ним (кусок растёт к началу дня) и
    // возвращает день в кучу с обновлённой длиной, пока перед куском остаётся место.
    public sealed class LeadingChunkGrowthSelector : PlanningSelectorBase
    {
        private readonly record struct State
        {
            public required DateOnly Date { get; init; }
            public required int Start { get; init; }  // начало первого куска
            public required int Length { get; init; } // его длина (ключ кучи)
        }

        private readonly PriorityQueue<State, (int Length, DateOnly Date)> _heap = new();
        private readonly RoundRobinPicker _picker;

        public LeadingChunkGrowthSelector(Dictionary<string, int> queue, List<Slot> fillable, int slotCount = 6)
            : base(queue, fillable)
        {
            _picker = new RoundRobinPicker(Queue);
            foreach (Day d in DayLayout.GetDays(Fillable))
            {
                if (DayLayout.FirstChunk(d.Open, slotCount) is { Start: > 1 } c)
                {
                    _heap.Enqueue(
                        new State { Date = d.Date, Start = c.Start, Length = c.Length },
                        (c.Length, d.Date));
                }
            }
        }

        protected override Slot? TakeSlot()
        {
            if (!_heap.TryDequeue(out State s, out _)) return null;

            Slot slot = new() { Date = s.Date, Number = s.Start - 1 };
            int start = s.Start - 1;
            int length = s.Length + 1;
            if (start > 1)
            {
                _heap.Enqueue(s with { Start = start, Length = length }, (length, s.Date));
            }
            return slot;
        }

        protected override string? PickSubject() => _picker.Pick();
    }
}
