using MyTimetable.Models;

namespace MyTimetable.Planning;

public sealed class EmptyDaySeedSelector : PlanningSelectorBase
{
    private readonly ISlotter _slotter;
    private readonly IPicker _picker;

    public EmptyDaySeedSelector(Dictionary<string, int> queue, List<Slot> fillable,
                                int slotCount = 6, IPicker? picker = null, ISlotter? slotter = null)
        : base(queue, fillable)
    {
        _slotter = slotter ?? new EmptyDaySeedSlotter(Fillable, slotCount);
        _picker = picker ?? new RoundRobinPicker(queue);
    }

    protected override Slot? TakeSlot() => _slotter.NextSlot();
    protected override string? PickSubject() => _picker.Pick(Queue);
}
