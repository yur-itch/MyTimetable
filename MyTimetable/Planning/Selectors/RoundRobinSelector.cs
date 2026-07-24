
namespace MyTimetable.Planning;

public sealed class RoundRobinSelector : PlanningSelectorBase
{
    private readonly IPicker _picker;

    public RoundRobinSelector(Dictionary<string, int> queue, List<Slot> fillable,
                              IPickerFactory? pickerFactory = null)
        : base(queue, fillable)
        => _picker = pickerFactory?.Create(Queue) ?? new RoundRobinPicker(Queue);

    protected override string? PeekSubject() => _picker.Peek(Queue);
    protected override void CommitSubject(string title) => _picker.Commit(title);
}
