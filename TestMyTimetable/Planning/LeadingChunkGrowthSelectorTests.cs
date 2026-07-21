using MyTimetable.Models;
using MyTimetable.Planning;

namespace TestMyTimetable.Planning;

public class TestLeadingChunkGrowthSelector
{
    private static List<Slot> DaySlots(DateOnly date, params int[] open)
        => open.Select(n => new Slot { Date = date, Number = n }).ToList();

    [Fact]
    public void Plan_EmptyQueue_ReturnsNothing()
    {
        var fillable = DaySlots(new DateOnly(2024, 1, 1), 1, 2, 3, 4, 5, 6);
        var sel = new LeadingChunkGrowthSelector(new Dictionary<string, int>(), fillable);
        sel.Plan().Should().BeEmpty();
    }

    [Fact]
    public void Plan_EmptyFillable_ReturnsNothing()
    {
        var sel = new LeadingChunkGrowthSelector(new Dictionary<string, int> { ["A"] = 3 }, []);
        sel.Plan().Should().BeEmpty();
    }

    [Fact]
    public void Plan_FullyEmptyDay_NoLeadingChunk_ReturnsNothing()
    {
        var fillable = DaySlots(new DateOnly(2024, 1, 1), 1, 2, 3, 4, 5, 6);
        var sel = new LeadingChunkGrowthSelector(
            new Dictionary<string, int> { ["A"] = 3 }, fillable);
        sel.Plan().Should().BeEmpty();
    }

    [Fact]
    public void Plan_ChunkStartsAtOne_NoRoomToGrowLeft()
    {
        var fillable = DaySlots(new DateOnly(2024, 1, 1), 2, 3, 4, 5, 6);
        var sel = new LeadingChunkGrowthSelector(
            new Dictionary<string, int> { ["A"] = 3 }, fillable);
        sel.Plan().Should().BeEmpty("первый кусок начинается с 1, расширяться некуда");
    }

    // ── Single-day exhaustive: all 64 subsets ──────────────────────

    public static IEnumerable<object[]> SingleDayFirstSlotCases()
    {
        for (int mask = 0; mask < 64; mask++)
        {
            var open = new List<int>(6);
            for (int i = 1; i <= 6; i++)
                if ((mask & (1 << (i - 1))) != 0) open.Add(i);

            int occupiedFrom = 1;
            while (open.Contains(occupiedFrom)) occupiedFrom++;
            bool canGrow = occupiedFrom <= 6 && occupiedFrom > 1;
            int? expectedNumber = canGrow ? occupiedFrom - 1 : null;

            yield return new object[] { open.ToArray(), expectedNumber };
        }
    }

    [Theory]
    [MemberData(nameof(SingleDayFirstSlotCases))]
    public void Plan_SingleDay_FirstSlot(int[] open, int? expectedNumber)
    {
        var fillable = DaySlots(new DateOnly(2024, 1, 1), open);
        var sel = new LeadingChunkGrowthSelector(
            new Dictionary<string, int> { ["A"] = 10 }, fillable);
        var results = sel.Plan().ToList();

        if (expectedNumber is null)
        {
            results.Should().BeEmpty($"open=[{string.Join(",", open)}] — нет куска с Start > 1");
        }
        else
        {
            results.Should().NotBeEmpty($"open=[{string.Join(",", open)}] — ожидался слот {expectedNumber}");
            results[0].Slot.Number.Should().Be(expectedNumber.Value);
            results[0].Slot.Date.Should().Be(new DateOnly(2024, 1, 1));
        }
    }

    // ── Multi-day heap behavior ──────────────────────────────────────

    [Fact]
    public void Plan_TwoDaysSameLength_FirstByDate()
    {
        // Day1: занят слот 3 → open=[1,2,4,5,6] → FirstChunk Start=3, Length=1
        // Day2: занят слот 2 → open=[1,3,4,5,6] → FirstChunk Start=2, Length=1
        // Оба Length=1 → выбирается Jan1 (раньше по дате)
        var fillable = new List<Slot>
        {
            new() { Date = new DateOnly(2024, 1, 1), Number = 1 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 2 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 4 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 5 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 6 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 1 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 3 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 4 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 5 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 6 },
        };
        var sel = new LeadingChunkGrowthSelector(
            new Dictionary<string, int> { ["A"] = 4 }, fillable);
        var results = sel.Plan().ToList();

        results.Should().NotBeEmpty();
        results[0].Slot.Date.Should().Be(new DateOnly(2024, 1, 1));
        results[0].Slot.Number.Should().Be(2); // Start=3 → -1=2
    }

    [Fact]
    public void Plan_ShortestChunkGetsPriority()
    {
        // Day1: занят слот 4 → open=[1,2,3,5,6] → FirstChunk Start=4, Length=1
        // Day2: заняты 2,3 → open=[1,4,5,6] → FirstChunk Start=2, Length=2
        // Length=1 < Length=2 → первый placement из Day1
        var fillable = new List<Slot>
        {
            new() { Date = new DateOnly(2024, 1, 1), Number = 1 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 2 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 3 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 5 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 6 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 1 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 4 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 5 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 6 },
        };
        var sel = new LeadingChunkGrowthSelector(
            new Dictionary<string, int> { ["A"] = 3 }, fillable);
        var results = sel.Plan().ToList();

        results.Should().NotBeEmpty();
        results[0].Slot.Date.Should().Be(new DateOnly(2024, 1, 1)); // Length=1, first
        results[0].Slot.Number.Should().Be(3); // Start=4 → -1=3
    }

    [Fact]
    public void Plan_ChunkGrowsUntilItReachesSlotOne()
    {
        var fillable = DaySlots(new DateOnly(2024, 1, 1), 1, 2, 3, 5, 6);
        var sel = new LeadingChunkGrowthSelector(
            new Dictionary<string, int> { ["A"] = 3 }, fillable);
        var results = sel.Plan().ToList();

        results.Should().HaveCount(3);
        results[0].Slot.Number.Should().Be(3);
        results[1].Slot.Number.Should().Be(2);
        results[2].Slot.Number.Should().Be(1);
    }

    [Fact]
    public void Plan_StopsWhenQueueExhausted()
    {
        var fillable = DaySlots(new DateOnly(2024, 1, 1), 1, 2, 3, 5, 6);
        var sel = new LeadingChunkGrowthSelector(
            new Dictionary<string, int> { ["A"] = 2 }, fillable);
        sel.Plan().Should().HaveCount(2);
    }

    [Fact]
    public void Plan_UsesRoundRobinForSubjectSelection()
    {
        var fillable = DaySlots(new DateOnly(2024, 1, 1), 1, 2, 3, 5, 6);
        var queue = new Dictionary<string, int> { ["X"] = 2, ["Y"] = 2 };
        var sel = new LeadingChunkGrowthSelector(queue, fillable);
        var results = sel.Plan().ToList();

        results.Should().HaveCount(3); // slot 3, 2, 1
        results[0].Lesson.Title.Should().Be("X");
        results[1].Lesson.Title.Should().Be("Y");
        results[2].Lesson.Title.Should().Be("X");
    }

    [Fact]
    public void Plan_OutputHasCorrectProperties()
    {
        var fillable = DaySlots(new DateOnly(2024, 5, 10), 1, 2, 3, 5, 6);
        var result = new LeadingChunkGrowthSelector(
            new Dictionary<string, int> { ["A"] = 1 }, fillable).Plan().ToList();

        result.Should().HaveCount(1);
        result[0].Slot.Date.Should().Be(new DateOnly(2024, 5, 10));
        result[0].Slot.Number.Should().Be(3);
        result[0].Lesson.Title.Should().Be("A");
        result[0].Lesson.LessonType.Should().Be("PRACTICE");
    }
}
