
namespace MyTimetable.Planning;

public sealed class EmptyDaySeedSlotter : ISlotter
{
    private readonly IEnumerator<Slot> _seeds;

    public EmptyDaySeedSlotter(List<Slot> fillable, int slotCount = 6)
    {
        _seeds = DayLayout.GetDays(fillable)
            .Where(d => DayLayout.FirstChunk(d.Open, slotCount) is null)
            .Select(d => new Slot { Date = d.Date, Number = d.Open[0] })
            .GetEnumerator();
    }

    public Slot? Peek() => _seeds.MoveNext() ? _seeds.Current : null;
    public void Commit(Slot slot) { }
}
