using MyTimetable.Models;

namespace MyTimetable.Planning;

public sealed class WeightedRandomSelector : PlanningSelectorBase
{
    private readonly IPicker _picker;

    public WeightedRandomSelector(Dictionary<string, int> queue, List<Slot> fillable,
                                  Random? random = null, IPickerFactory? pickerFactory = null)
        : base(queue, fillable)
        => _picker = pickerFactory?.Create(Queue) ?? new WeightedRandomPicker(random);

    protected override string? PeekSubject() => _picker.Peek(Queue);
    protected override void CommitSubject(string title) => _picker.Commit(title);
}
