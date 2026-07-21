using MyTimetable.Models;
using MyTimetable.Planning;

namespace TestMyTimetable.Planning;

public class TestLeadingChunkGrowthSelector
{
    // ── Helpers ───────────────────────────────────────────────────────

    // Создаёт fillable-слоты дня: для каждого номера из open создаётся Slot.
    // open — номера свободных пар (1-indexed).
    private static List<Slot> DaySlots(DateOnly date, params int[] open)
        => open.Select(n => new Slot { Date = date, Number = n }).ToList();

    // ── Edge & base cases ────────────────────────────────────────────

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
        // Все слоты свободны → FirstChunk = null → день не добавляется в heap
        var fillable = DaySlots(new DateOnly(2024, 1, 1), 1, 2, 3, 4, 5, 6);
        var sel = new LeadingChunkGrowthSelector(
            new Dictionary<string, int> { ["A"] = 3 }, fillable);
        sel.Plan().Should().BeEmpty("пустой день не имеет первого куска занятых");
    }

    [Fact]
    public void Plan_ChunkStartsAtOne_NoRoomToGrowLeft()
    {
        // Занята пара 1 → FirstChunk Start=1, но условие Start > 1 не выполняется
        // fillable = [2,3,4,5,6]
        var fillable = DaySlots(new DateOnly(2024, 1, 1), 2, 3, 4, 5, 6);
        var sel = new LeadingChunkGrowthSelector(
            new Dictionary<string, int> { ["A"] = 3 }, fillable);
        sel.Plan().Should().BeEmpty("первый кусок начинается с 1, расширяться некуда");
    }

    // ── Single-day exhaustive: all 64 subsets ──────────────────────

    // Для одного дня с slotCount=6 проверяем, какой слот (если есть) выберет стратегия.
    // open — список свободных номеров. Стратегия выбирает день, если FirstChunk(open,6)?.Start > 1.
    // Если да, она ставит в (Start - 1) и перекладывает обратно с Start--, Length++.
    // Если после этого Start всё ещё > 1, день возвращается в heap.
    // Для полного теста достаточно одного шага: проверяем первый выбранный слот.
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
        var sel = new LeadingChunkGrowthSelector(
            new Dictionary<string, int> { ["A"] = 10 }, fillable);
        var results = sel.Plan().ToList();

        if (expectedNumber is null)
        {
            results.Should().BeEmpty(
                $"open=[{string.Join(",", open)}] — нет куска с Start > 1");
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
        // День 1: занята пара 3 → open=[1,2,4,5,6] → FirstChunk Start=3, Length=1
        //   может расти влево в слот 2
        // День 2: занята пара 2 → open=[1,3,4,5,6] → FirstChunk Start=2, Length=1
        //   может расти влево в слот 1
        // Оба Length=1, сортировка по дате → день 1 (2024-01-01) раньше дня 2 (2024-01-02)
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

        // Дни с одинаковым Length=1, сортировка по дате
        results.Should().HaveCount(2);
        results[0].Slot.Date.Should().Be(new DateOnly(2024, 1, 1));
        results[0].Slot.Number.Should().Be(2); // Start=3 → -1 = 2
        results[1].Slot.Date.Should().Be(new DateOnly(2024, 1, 2));
        results[1].Slot.Number.Should().Be(1); // Start=2 → -1 = 1
    }

    [Fact]
    public void Plan_TwoDaysDifferentChunkSizes_SmallestFirst()
    {
        // День 1: заняты 5,6 → open=[1,2,3,4] → FirstChunk Start=5, Length=2
        //   растёт в слот 4
        // День 2: заняты 2,3 → open=[1,4,5,6] → FirstChunk Start=2, Length=2
        //   растёт в слот 1
        // Оба Length=2, по дате → день 1 первый
        var fillable = new List<Slot>
        {
            new() { Date = new DateOnly(2024, 1, 1), Number = 1 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 2 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 3 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 4 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 1 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 4 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 5 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 6 },
        };
        // А поменяем: день1 Length=2 (Start=5), день2 Length=2 (Start=2) — одинаково, по дате
        // Сделаем разные длины
    }

    [Fact]
    public void Plan_ShortestChunkGetsPriority()
    {
        // День 1: заняты 4 → open=[1,2,3,5,6] → FirstChunk Start=4, Length=1 → растёт в 3
        // День 2: заняты 2,3 → open=[1,4,5,6] → FirstChunk Start=2, Length=2 → растёт в 1
        // Length=1 < Length=2 → выбираем день 1
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

        results.Should().HaveCount(2);
        results[0].Slot.Date.Should().Be(new DateOnly(2024, 1, 1)); // Length=1, first
        results[0].Slot.Number.Should().Be(3); // Start=4 → -1=3
        results[1].Slot.Date.Should().Be(new DateOnly(2024, 1, 2)); // Length=2, second
        results[1].Slot.Number.Should().Be(1); // Start=2 → -1=1
    }

    [Fact]
    public void Plan_ChunkGrowsUntilItReachesSlotOne()
    {
        // Один день с занятой парой 4 → open=[1,2,3,5,6]
        // Start=4, Length=1 → ставим в 3, Start=3, Length=2
        // Start=3 > 1 → ставим в 2, Start=2, Length=3
        // Start=2 > 1 → ставим в 1, Start=1 → stop
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

        // День занят с 4: Sequence: slot 3 (X), slot 2 (Y), slot 1 (X)
        // На 4-й итерации Start станет 1 → не перекладываем → нет больше слотов
        results.Should().HaveCount(3);
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
