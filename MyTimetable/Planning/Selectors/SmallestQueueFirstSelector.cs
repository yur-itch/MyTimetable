
namespace MyTimetable.Planning;

public sealed class SmallestQueueFirstSelector : PlanningSelectorBase
{
    private readonly IPicker _picker;

    public SmallestQueueFirstSelector(Dictionary<string, int> queue, List<Slot> fillable,
                                      IPickerFactory? pickerFactory = null)
        : base(queue, fillable)
        => _picker = pickerFactory?.Create(Queue) ?? new SmallestQueueFirstPicker();

    protected override string? PeekSubject() => _picker.Peek(Queue);
    protected override void CommitSubject(string title) => _picker.Commit(title);
}
