using MyTimetable.Models;

namespace MyTimetable.Planning;

public sealed class RandomSelector : PlanningSelectorBase
{
    private readonly IPicker _picker;

    public RandomSelector(Dictionary<string, int> queue, List<Slot> fillable,
                          Random? random = null, IPicker? picker = null)
        : base(queue, fillable)
        => _picker = picker ?? new RandomPicker(random);

    protected override string? PickSubject() => _picker.Pick(Queue);
}
