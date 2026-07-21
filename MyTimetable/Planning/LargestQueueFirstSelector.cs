using MyTimetable.Models;

namespace MyTimetable.Planning;

public sealed class LargestQueueFirstSelector : PlanningSelectorBase
{
    private readonly IPicker _picker;

    public LargestQueueFirstSelector(Dictionary<string, int> queue, List<Slot> fillable,
                                     IPicker? picker = null)
        : base(queue, fillable)
        => _picker = picker ?? new LargestQueueFirstPicker();

    protected override string? PickSubject() => _picker.Pick(Queue);
}
