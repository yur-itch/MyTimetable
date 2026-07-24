
namespace MyTimetable.Planning;

public sealed class TrailingChunkGrowthSlotter : ISlotter
{
    private readonly record struct State(DateOnly Date, int End, int Length);

    private readonly PriorityQueue<State, (int Length, DateOnly Date)> _heap = new();
    private readonly int _slotCount;

    public TrailingChunkGrowthSlotter(List<Slot> fillable, int slotCount = 6)
    {
        _slotCount = slotCount;
        foreach (var d in DayLayout.GetDays(fillable))
        {
            if (DayLayout.LastChunk(d.Open, slotCount) is { } c && c.End < slotCount)
            {
                _heap.Enqueue(
                    new State(d.Date, c.End, c.Length),
                    (c.Length, d.Date));
            }
        }
    }

    public Slot? Peek()
    {
        if (!_heap.TryDequeue(out var s, out _)) return null;

        int number = s.End + 1;
        if (number < _slotCount)
        {
            _heap.Enqueue(
                s with { End = number, Length = s.Length + 1 },
                (s.Length + 1, s.Date));
        }
        return new Slot { Date = s.Date, Number = number };
    }

    public void Commit(Slot slot) { }
}
