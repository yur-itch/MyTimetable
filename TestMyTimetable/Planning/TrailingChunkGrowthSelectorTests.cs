using MyTimetable.Models;
using MyTimetable.Planning;

namespace TestMyTimetable.Planning;

public class TestTrailingChunkGrowthSelector
{
    // ── Helpers ───────────────────────────────────────────────────────

    private static List<Slot> DaySlots(DateOnly date, params int[] open)
        => open.Select(n => new Slot { Date = date, Number = n }).ToList();

    // ── Edge & base cases ────────────────────────────────────────────

    [Fact]
    public void Plan_EmptyQueue_ReturnsNothing()
    {
        var fillable = DaySlots(new DateOnly(2024, 1, 1), 1, 2, 3, 4, 5, 6);
        var sel = new TrailingChunkGrowthSelector(new Dictionary<string, int>(), fillable);
        sel.Plan().Should().BeEmpty();
    }

    [Fact]
    public void Plan_EmptyFillable_ReturnsNothing()
    {
        var sel = new TrailingChunkGrowthSelector(new Dictionary<string, int> { ["A"] = 3 }, []);
        sel.Plan().Should().BeEmpty();
    }

    [Fact]
    public void Plan_FullyEmptyDay_NoTrailingChunk_ReturnsNothing()
    {
        var fillable = DaySlots(new DateOnly(2024, 1, 1), 1, 2, 3, 4, 5, 6);
        var sel = new TrailingChunkGrowthSelector(
            new Dictionary<string, int> { ["A"] = 3 }, fillable);
        sel.Plan().Should().BeEmpty();
    }

    [Fact]
    public void Plan_ChunkEndsAtMaxSlot_NoRoomToGrowRight()
    {
        // Занята пара 6 → LastChunk End=6, условие End < slotCount не выполняется
        var fillable = DaySlots(new DateOnly(2024, 1, 1), 1, 2, 3, 4, 5);
        var sel = new TrailingChunkGrowthSelector(
            new Dictionary<string, int> { ["A"] = 3 }, fillable);
        sel.Plan().Should().BeEmpty("последний кусок на 6, расширяться некуда");
    }

    // ── Single-day exhaustive: all 64 subsets ──────────────────────

    // Для одного дня проверяем первый выбранный слот (если есть).
    // Стратегия выбирает день, если LastChunk(open,6)?.End < 6.
    // Ставит в (End + 1), перекладывает с End++, Length++.
    // Если после этого End < 6, день возвращается в heap.
    public static IEnumerable<object[]> SingleDayFirstSlotCases()
    {
        for (int mask = 0; mask < 64; mask++)
        {
            var open = new List<int>(6);
            for (int i = 1; i <= 6; i++)
                if ((mask & (1 << (i - 1))) != 0) open.Add(i);

            var occupied = Enumerable.Range(1, 6).Except(open).ToArray();
            int? expectedNumber = null;
            if (occupied.Length > 0)
            {
                int lastEnd = occupied.Max();
                if (lastEnd < 6)
                    expectedNumber = lastEnd + 1;
                // также нужно проверить, что занятые непрерывны от lastEnd
                // но LastChunk уже гарантирует непрерывность последнего куска
            }

            yield return new object[]
            {
                open.ToArray(),
                expectedNumber,
            };
        }
    }

    [Theory]
    [MemberData(nameof(SingleDayFirstSlotCases))]
    public void Plan_SingleDay_FirstSlot(int[] open, int? expectedNumber)
    {
        var fillable = DaySlots(new DateOnly(2024, 1, 1), open);
        var sel = new TrailingChunkGrowthSelector(
            new Dictionary<string, int> { ["A"] = 10 }, fillable);
        var results = sel.Plan().ToList();

        if (expectedNumber is null)
        {
            results.Should().BeEmpty(
                $"open=[{string.Join(",", open)}] — нет куска с End < 6");
        }
        else
        {
            results.Should().NotBeEmpty(
                $"open=[{string.Join(",", open)}] — ожидался слот {expectedNumber}");
            results[0].Slot.Number.Should().Be(expectedNumber.Value);
            results[0].Slot.Date.Should().Be(new DateOnly(2024, 1, 1));
        }
    }

    // ── Multi-day heap behavior ──────────────────────────────────────

    [Fact]
    public void Plan_TwoDays_ChoosesSmallestChunkFirst()
    {
        // День 1: занята пара 4 → open=[1,2,3,5,6] → LastChunk End=4, Length=1
        //   растёт в слот 5
        // День 2: занята пара 5 → open=[1,2,3,4,6] → LastChunk End=5, Length=1
        //   растёт в слот 6
        // Оба Length=1, по дате → день 1 первый
        var fillable = new List<Slot>
        {
            new() { Date = new DateOnly(2024, 1, 1), Number = 1 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 2 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 3 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 5 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 6 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 1 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 2 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 3 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 4 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 6 },
        };
        var sel = new TrailingChunkGrowthSelector(
            new Dictionary<string, int> { ["A"] = 4 }, fillable);
        var results = sel.Plan().ToList();

        results.Should().HaveCount(2);
        results[0].Slot.Date.Should().Be(new DateOnly(2024, 1, 1));
        results[0].Slot.Number.Should().Be(5); // End=4 → +1=5
        results[1].Slot.Date.Should().Be(new DateOnly(2024, 1, 2));
        results[1].Slot.Number.Should().Be(6); // End=5 → +1=6
    }

    [Fact]
    public void Plan_ShortestTrailingChunkGetsPriority()
    {
        // День 1: занята 3 → open=[1,2,4,5,6] → LastChunk Start=3, End=3, Length=1
        //   растёт в слот 4
        // День 2: заняты 2,3 → open=[1,4,5,6] → LastChunk Start=2, End=3, Length=2
        //   растёт в слот 4
        // Length=1 < Length=2 → день 1 первый
        var fillable = new List<Slot>
        {
            new() { Date = new DateOnly(2024, 1, 1), Number = 1 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 2 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 4 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 5 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 6 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 1 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 4 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 5 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 6 },
        };
        var sel = new TrailingChunkGrowthSelector(
            new Dictionary<string, int> { ["A"] = 3 }, fillable);
        var results = sel.Plan().ToList();

        results.Should().HaveCount(2);
        results[0].Slot.Date.Should().Be(new DateOnly(2024, 1, 1));
        results[0].Slot.Number.Should().Be(4); // End=3 → +1=4
        results[1].Slot.Date.Should().Be(new DateOnly(2024, 1, 2));
        results[1].Slot.Number.Should().Be(4); // End=3 → +1=4
    }

    [Fact]
    public void Plan_ChunkGrowsUntilItReachesMaxSlot()
    {
        // Один день с занятой парой 3 → open=[1,2,4,5,6]
        // LastChunk Start=3, End=3, Length=1 → ставим в 4, End=4, Length=2
        // End=4 < 6 → ставим в 5, End=5, Length=3
        // End=5 < 6 → ставим в 6, End=6 → stop
        var fillable = DaySlots(new DateOnly(2024, 1, 1), 1, 2, 4, 5, 6);
        var sel = new TrailingChunkGrowthSelector(
            new Dictionary<string, int> { ["A"] = 3 }, fillable);
        var results = sel.Plan().ToList();

        results.Should().HaveCount(3);
        results[0].Slot.Number.Should().Be(4);
        results[1].Slot.Number.Should().Be(5);
        results[2].Slot.Number.Should().Be(6);
    }

    [Fact]
    public void Plan_StopsWhenQueueExhausted()
    {
        var fillable = DaySlots(new DateOnly(2024, 1, 1), 1, 2, 4, 5, 6);
        var sel = new TrailingChunkGrowthSelector(
            new Dictionary<string, int> { ["A"] = 2 }, fillable);
        sel.Plan().Should().HaveCount(2);
    }

    [Fact]
    public void Plan_UsesRoundRobinForSubjectSelection()
    {
        var fillable = DaySlots(new DateOnly(2024, 1, 1), 1, 2, 4, 5, 6);
        var queue = new Dictionary<string, int> { ["X"] = 2, ["Y"] = 2 };
        var sel = new TrailingChunkGrowthSelector(queue, fillable);
        var results = sel.Plan().ToList();

        // День занят с 3: Sequence: slot 4 (X), slot 5 (Y), slot 6 (X)
        // На 4-й итерации End=6 → не перекладываем → нет больше слотов
        results.Should().HaveCount(3);
        results[0].Lesson.Title.Should().Be("X");
        results[1].Lesson.Title.Should().Be("Y");
        results[2].Lesson.Title.Should().Be("X");
    }

    [Fact]
    public void Plan_OutputHasCorrectProperties()
    {
        var fillable = DaySlots(new DateOnly(2024, 5, 10), 1, 2, 4, 5, 6);
        var result = new TrailingChunkGrowthSelector(
            new Dictionary<string, int> { ["A"] = 1 }, fillable).Plan().ToList();

        result.Should().HaveCount(1);
        result[0].Slot.Date.Should().Be(new DateOnly(2024, 5, 10));
        result[0].Slot.Number.Should().Be(4);
        result[0].Lesson.Title.Should().Be("A");
        result[0].Lesson.LessonType.Should().Be("PRACTICE");
    }
}
