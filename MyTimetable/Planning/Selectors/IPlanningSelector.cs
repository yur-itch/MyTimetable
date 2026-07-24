
namespace MyTimetable.Planning
{
    // Стратегия планирования: по очереди предметов и пулу свободных слотов выдаёт размещения.
    // accept вызывается для каждого предложенного (slot, subject).
    // Возвращает два независимых бита — OK ли слот, OK ли предмет — которые селектор
    // интерпретирует механически, не зная конкретных правил планировщика.
    public interface IPlanningSelector
    {
        IEnumerable<PlannedSlot> Plan(Func<Slot, string, ProposeResult> accept);
    }

    public interface IPlanningSelectorFactory
    {
        IPlanningSelector Create(Dictionary<string, int> queue, List<Slot> fillable);
    }
}
