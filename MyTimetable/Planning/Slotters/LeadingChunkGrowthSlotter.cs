using MyTimetable.Models;

namespace MyTimetable.Planning;

public sealed class LeadingChunkGrowthSlotter : ISlotter
{
    private readonly record struct State(DateOnly Date, int Start, int Length);

    private readonly PriorityQueue<State, (int Length, DateOnly Date)> _heap = new();

    public LeadingChunkGrowthSlotter(List<Slot> fillable, int slotCount = 6)
    {
        foreach (var d in DayLayout.GetDays(fillable))
        {
            if (DayLayout.FirstChunk(d.Open, slotCount) is { Start: > 1 } c)
            {
                _heap.Enqueue(
                    new State(d.Date, c.Start, c.Length),
                    (c.Length, d.Date));
            }
        }
    }

    public Slot? Peek()
    {
        if (!_heap.TryDequeue(out var s, out _)) return null;

        int number = s.Start - 1;
        if (number > 1)
        {
            _heap.Enqueue(
                s with { Start = number, Length = s.Length + 1 },
                (s.Length + 1, s.Date));
        }
        return new Slot { Date = s.Date, Number = number };
    }

    public void Commit(Slot slot) { }
}
