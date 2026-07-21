using MyTimetable.Models;

namespace MyTimetable.Planning;

public sealed class WeightedRandomSelector : PlanningSelectorBase
{
    private readonly IPicker _picker;

    public WeightedRandomSelector(Dictionary<string, int> queue, List<Slot> fillable,
                                  Random? random = null, IPicker? picker = null)
        : base(queue, fillable)
        => _picker = picker ?? new WeightedRandomPicker(random);

    protected override string? PickSubject() => _picker.Pick(Queue);
}
