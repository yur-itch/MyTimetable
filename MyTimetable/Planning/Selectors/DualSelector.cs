using MyTimetable.Models;

namespace MyTimetable.Planning;

/// <summary>
/// General-purpose dual selector: picker + slotter composed from factories.
/// Used when the user picks components from the UI; the controller constructs one
/// of these instead of a concrete dual selector (GapClosingSelector, etc.).
/// </summary>
public sealed class DualSelector : PlanningSelectorBase
{
    private readonly ISlotter _slotter;
    private readonly IPicker _picker;

    public DualSelector(Dictionary<string, int> queue, List<Slot> fillable,
                        IPickerFactory pickerFactory, ISlotterFactory slotterFactory)
        : base(queue, fillable)
    {
        _slotter = slotterFactory.Create(Fillable);
        _picker = pickerFactory.Create(Queue);
    }

    protected override Slot? PeekSlot() => _slotter.Peek();
    protected override void CommitSlot(Slot slot) => _slotter.Commit(slot);
    protected override string? PeekSubject() => _picker.Peek(Queue);
    protected override void CommitSubject(string title) => _picker.Commit(title);
}
