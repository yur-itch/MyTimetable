
namespace MyTimetable.Planning
{
    // Обобщённая фабрика: одна реализация на все стратегии, конструктор замыкается на нужную.
    public sealed class PlanningSelectorFactory : IPlanningSelectorFactory
    {
        private readonly Func<Dictionary<string, int>, List<Slot>, IPlanningSelector> _create;

        public PlanningSelectorFactory(Func<Dictionary<string, int>, List<Slot>, IPlanningSelector> create)
            => _create = create;

        public IPlanningSelector Create(Dictionary<string, int> queue, List<Slot> fillable) => _create(queue, fillable);
    }
}
