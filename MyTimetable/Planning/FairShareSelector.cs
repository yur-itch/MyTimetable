using MyTimetable.Models;

namespace MyTimetable.Planning;

public sealed class FairShareSelector : PlanningSelectorBase
{
    private readonly IPicker _picker;

    public FairShareSelector(Dictionary<string, int> queue, List<Slot> fillable,
                             IPicker? picker = null)
        : base(queue, fillable)
        => _picker = picker ?? new FairSharePicker(queue);

    protected override string? PickSubject() => _picker.Pick(Queue);
}
