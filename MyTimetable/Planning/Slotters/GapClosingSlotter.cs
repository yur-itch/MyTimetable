
namespace MyTimetable.Planning;

internal readonly record struct GapRange(int Start, int Size);

internal sealed record SlotRange(Slot First, int Size)
{
    public IEnumerable<Slot> Iterate()
    {
        for (int i = 0; i < Size; i++)
        {
            yield return new Slot { Date = First.Date, Number = First.Number + i };
        }
    }
}

public sealed class GapClosingSlotter : ISlotter
{
    private readonly IEnumerator<Slot> _gapSlots;

    public GapClosingSlotter(List<Slot> fillable, int slotCount = 6)
    {
        _gapSlots = DayLayout.GetDays(fillable)
            .SelectMany(day =>
            {
                var ranges = GetGapsInDay(day.Open, slotCount);
                return ranges.Select(r => new SlotRange(
                    new Slot { Date = day.Date, Number = r.Start }, r.Size));
            })
            .OrderBy(x => x.Size)
            .ThenBy(x => x.First.Date)
            .ThenBy(x => x.First.Number)
            .SelectMany(x => x.Iterate())
            .GetEnumerator();
    }

    public Slot? Peek() => _gapSlots.MoveNext() ? _gapSlots.Current : null;
    public void Commit(Slot slot) { }

    internal static IEnumerable<GapRange> GetGapsInDay(List<int> day, int slotCount)
    {
        FirstAndLastOccupied(day, slotCount, out int? firstTaken, out int? lastTaken);

        if (firstTaken == lastTaken || day.Count == 0) yield break;

        int start = 0;
        for (; start < day.Count; start++)
        {
            if (day[start] > firstTaken) break;
        }

        if (start >= day.Count || day[start] >= lastTaken) yield break;

        int prevVal = day[start];
        int startGap = day[start];
        for (int i = start + 1; i < day.Count; i++)
        {
            int val = day[i];
            if (val >= lastTaken)
            {
                yield return new GapRange(startGap, prevVal - startGap + 1);
                yield break;
            }
            if (prevVal < val - 1)
            {
                yield return new GapRange(startGap, prevVal - startGap + 1);
                startGap = val;
            }
            prevVal = val;
        }
        if (prevVal < lastTaken)
        {
            yield return new GapRange(startGap, prevVal - startGap + 1);
        }
    }

    internal static void FirstAndLastOccupied(List<int> day, int slotCount,
        out int? firstTaken, out int? lastTaken)
    {
        firstTaken = null;
        lastTaken = null;

        if (day.Count == 0)
        {
            firstTaken = 1;
            lastTaken = slotCount;
            return;
        }
        if (day.Count == slotCount) return;

        if (day[0] > 1)
        {
            firstTaken = 1;
            lastTaken = day[0] - 1;
            if (day[^1] < slotCount)
                lastTaken = slotCount;
        }
        else if (day[^1] < slotCount)
        {
            lastTaken = slotCount;
            int gap = 1;
            foreach (int v in day)
            {
                if (v == gap) gap++;
                else break;
            }
            firstTaken = gap;
        }

        if (lastTaken != slotCount || firstTaken != 1)
        {
            int dayIndex = 1;
            for (int i = day[0] + 1; i < slotCount; i++)
            {
                if (dayIndex == day.Count) break;
                if (day[dayIndex] > i)
                {
                    firstTaken ??= i;
                    if (lastTaken != slotCount)
                        lastTaken = i;
                }
                else dayIndex++;
            }
        }
    }
}
