using MyTimetable.Models;

namespace MyTimetable.Planning;

public sealed class RoundRobinSelector : PlanningSelectorBase
{
    private readonly IPicker _picker;

    public RoundRobinSelector(Dictionary<string, int> queue, List<Slot> fillable,
                              IPicker? picker = null)
        : base(queue, fillable)
        => _picker = picker ?? new RoundRobinPicker(queue);

    protected override string? PickSubject() => _picker.Pick(Queue);
}
