using MyTimetable.Models;

namespace MyTimetable.Planning;

public sealed class FairShareSelector : PlanningSelectorBase
{
    private readonly IPicker _picker;

    public FairShareSelector(Dictionary<string, int> queue, List<Slot> fillable,
                             IPickerFactory? pickerFactory = null)
        : base(queue, fillable)
        => _picker = pickerFactory?.Create(Queue) ?? new FairSharePicker(Queue);

    protected override string? PeekSubject() => _picker.Peek(Queue);
    protected override void CommitSubject(string title) => _picker.Commit(title);
}
