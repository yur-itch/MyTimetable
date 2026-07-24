
namespace MyTimetable.Planning
{
    public sealed class CompositePlanningSelector : IPlanningSelector
    {
        private readonly Dictionary<string, int> _queue;
        private readonly List<Slot> _fillable;
        private readonly IEnumerable<IPlanningSelectorFactory> _factories;

        public CompositePlanningSelector(Dictionary<string, int> queue, List<Slot> fillable,
                                         IEnumerable<IPlanningSelectorFactory> factories)
        {
            _queue = new(queue);
            _fillable = new(fillable);
            _factories = factories;
        }

        public IEnumerable<PlannedSlot> Plan(Func<Slot, string, ProposeResult> accept)
        {
            foreach (var factory in _factories)
            {
                foreach (var placed in factory.Create(_queue, _fillable).Plan(accept))
                {
                    _queue[placed.Lesson.Title]--;
                    _fillable.Remove(placed.Slot);
                    yield return placed;
                }
            }
        }
    }
}
