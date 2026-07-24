
namespace MyTimetable.Planning;

public sealed class LeadingChunkGrowthSelector : PlanningSelectorBase
{
    private readonly ISlotter _slotter;
    private readonly IPicker _picker;

    public LeadingChunkGrowthSelector(Dictionary<string, int> queue, List<Slot> fillable,
                                      int slotCount = 6,
                                      IPickerFactory? pickerFactory = null,
                                      ISlotterFactory? slotterFactory = null)
        : base(queue, fillable)
    {
        _slotter = slotterFactory?.Create(Fillable) ?? new LeadingChunkGrowthSlotter(Fillable, slotCount);
        _picker = pickerFactory?.Create(Queue) ?? new RoundRobinPicker(Queue);
    }

    protected override Slot? PeekSlot() => _slotter.Peek();
    protected override void CommitSlot(Slot slot) => _slotter.Commit(slot);
    protected override string? PeekSubject() => _picker.Peek(Queue);
    protected override void CommitSubject(string title) => _picker.Commit(title);
}
