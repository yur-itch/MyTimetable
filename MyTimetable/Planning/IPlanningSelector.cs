using MyTimetable.Models;

namespace MyTimetable.Planning
{
    // Стратегия планирования: по очереди предметов и пулу свободных слотов выдаёт размещения.
    public interface IPlanningSelector
    {
        IEnumerable<PlannedSlot> Plan();
    }

    public interface IPlanningSelectorFactory
    {
        IPlanningSelector Create(Dictionary<string, int> queue, List<Slot> fillable);
    }
}
