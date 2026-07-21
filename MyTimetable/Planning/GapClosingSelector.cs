using MyTimetable.Models;

namespace MyTimetable.Planning;

public sealed class GapClosingSelector : PlanningSelectorBase
{
    private readonly ISlotter _slotter;
    private readonly IPicker _picker;

    public GapClosingSelector(Dictionary<string, int> queue, List<Slot> fillable,
                              int slotCount = 6, IPicker? picker = null, ISlotter? slotter = null)
        : base(queue, fillable)
    {
        _slotter = slotter ?? new GapClosingSlotter(Fillable, slotCount);
        _picker = picker ?? new RoundRobinPicker(queue);
    }

    protected override Slot? PeekSlot() => _slotter.Peek();
    protected override void CommitSlot(Slot slot) => _slotter.Commit(slot);
    protected override string? PeekSubject() => _picker.Peek(Queue);
    protected override void CommitSubject(string title) => _picker.Commit(title);
}
