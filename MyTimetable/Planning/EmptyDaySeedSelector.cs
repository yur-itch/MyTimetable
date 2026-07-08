using MyTimetable.Models;

namespace MyTimetable.Planning
{
    // Сеет по одному уроку в каждый ПУСТОЙ день (без единой занятой пары), в первую свободную пару дня.
    // Каждый пустой день трогается ровно один раз; если уроки в очереди кончились раньше — оставшиеся
    // дни не трогаются вовсе (база останавливается, когда очередь пуста).
    public sealed class EmptyDaySeedSelector : PlanningSelectorBase
    {
        private readonly IEnumerator<Slot> _seeds;
        private readonly RoundRobinPicker _picker;

        public EmptyDaySeedSelector(Dictionary<string, int> queue, List<Slot> fillable, int slotCount = 6)
            : base(queue, fillable)
        {
            _picker = new RoundRobinPicker(Queue);
            _seeds = DayLayout.GetDays(Fillable)
                .Where(d => DayLayout.FirstChunk(d.Open, slotCount) is null) // пустой день
                .Select(d => new Slot { Date = d.Date, Number = d.Open[0] })
                .GetEnumerator();
        }

        protected override Slot? TakeSlot() => _seeds.MoveNext() ? _seeds.Current : null;

        protected override string? PickSubject() => _picker.Pick();
    }
}
